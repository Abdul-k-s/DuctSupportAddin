using Autodesk.Revit.DB;
using DuctSupportAddin.Core.Analysis;
using DuctSupportAddin.Core.Hosting;
using DuctSupportAddin.Core.Spacing;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Placement;

/// <summary>
/// Calculates support placement positions and parameters.
/// </summary>
public class PlacementCalculator
{
    private readonly Document _doc;
    private readonly Configuration _config;
    private readonly SpacingCalculator _spacingCalculator;
    private readonly FittingDetector _fittingDetector;
    private readonly HostDetector _hostDetector;
    private readonly WallProximityChecker _wallChecker;
    private readonly InsulationDetector _insulationDetector;
    
    // Cache elements for faster lookup
    private List<Element>? _cachedFloors;
    private List<Element>? _cachedCeilings;
    private List<Element>? _cachedBeams;
    private List<Element>? _cachedStructuralFloors;
    
    /// <summary>
    /// Standard vertical spacing for vertical duct supports (2 meters in feet).
    /// </summary>
    private const double VerticalSupportSpacingFeet = 6.5617; // 2 meters
    
    /// <summary>
    /// Offset above floor for floor-based vertical duct support (50mm in feet).
    /// </summary>
    private double FloorOffsetFeet => _config.VerticalDuctFloorOffsetFeet;
    
    /// <summary>
    /// Diagnostic info for last raycast (for debugging).
    /// </summary>
    public static string LastDiagnostic { get; private set; } = "";
    
    public PlacementCalculator(Document doc, Configuration config)
    {
        _doc = doc;
        _config = config;
        _spacingCalculator = new SpacingCalculator(config.SpacingStandard, config.SpacingAdjustmentPercent, config.CustomSpacingRules);
        _fittingDetector = new FittingDetector(doc);
        _hostDetector = new HostDetector(doc);
        _wallChecker = new WallProximityChecker(doc, config.WallProximityFeet);
        _insulationDetector = new InsulationDetector(doc, config.DefaultInsulationType, config.CustomInsulationDensity);
        
        // Pre-cache elements
        CacheElements();
    }
    
    /// <summary>
    /// Pre-cache elements for faster raycast lookup.
    /// </summary>
    private void CacheElements()
    {
        // Standard floors
        _cachedFloors = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        // Structural foundations/slabs
        _cachedStructuralFloors = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        // Also check for structural columns that might be used as slabs
        var structuralColumns = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        _cachedStructuralFloors.AddRange(structuralColumns);
        
        // Generic models (sometimes slabs are modeled as generic)
        var genericModels = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_GenericModel)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        _cachedCeilings = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Ceilings)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        _cachedBeams = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => !(e is BeamSystem)) // Exclude BeamSystem elements
            .ToList();
        
        // Store diagnostic info
        LastDiagnostic = $"Cached: Floors={_cachedFloors.Count}, StructuralFoundation={_cachedStructuralFloors.Count}, Generic={genericModels.Count}, Ceilings={_cachedCeilings.Count}, Beams={_cachedBeams.Count}";
        
        // Add generic models to structural floors list for checking
        _cachedStructuralFloors.AddRange(genericModels);
    }
    
    /// <summary>
    /// Calculate all support placements for a duct.
    /// </summary>
    public List<SupportPlacement> CalculatePlacements(DuctInfo duct)
    {
        var placements = new List<SupportPlacement>();
        
        // Get insulation info
        var insulation = _insulationDetector.GetInsulation(duct.ElementId);
        double effectiveInsulationThickness = _config.ConsiderInsulation ? insulation.Thickness : 0;
        
        // Calculate support width with insulation
        double supportWidth = duct.Width + (effectiveInsulationThickness * 2) + _config.ClearanceFeet;
        
        // Use last 4 digits of duct ID for unique mark prefix
        string ductPrefix = (duct.ElementId.IntegerValue % 10000).ToString("D4");
        
        if (duct.Orientation == DuctOrientation.Vertical)
        {
            // Special handling for vertical ducts
            placements.AddRange(CalculateVerticalDuctPlacements(duct, supportWidth, insulation, ductPrefix));
        }
        else
        {
            // Horizontal duct - determine support positions using standard spacing
            var positions = _spacingCalculator.CalculateSupportPositions(duct);
            
            positions = _fittingDetector.FilterPositions(duct, positions);
            
            int markIndex = 1;
            foreach (double param in positions)
            {
                XYZ position = duct.GetPointAt(param);
                var placement = CalculateHorizontalPlacement(duct, position, supportWidth, insulation);
                
                if (placement != null)
                {
                    placement.Mark = $"D{ductPrefix}-{markIndex:D3}";
                    placement.TributaryLength = _spacingCalculator.GetSpacing(duct);
                    placements.Add(placement);
                    markIndex++;
                }
            }
        }
        
        return placements;
    }
    
    /// <summary>
    /// Calculate placements for a vertical duct.
    /// 1. Place VerticalFloorSupportDuct at each floor the duct passes through
    /// 2. For gaps > 1.8m between floor supports, add VerticalWallSupportDuct using uniform partitioning
    /// </summary>
    private List<SupportPlacement> CalculateVerticalDuctPlacements(DuctInfo duct, double supportWidth, InsulationInfo insulation, string ductPrefix)
    {
        var placements = new List<SupportPlacement>();
        
        // Get duct center XY (same for all elevations in a vertical duct)
        double ductCenterX = (duct.StartPoint.X + duct.EndPoint.X) / 2;
        double ductCenterY = (duct.StartPoint.Y + duct.EndPoint.Y) / 2;
        
        // Determine top and bottom of vertical duct
        double topZ = Math.Max(duct.StartPoint.Z, duct.EndPoint.Z);
        double bottomZ = Math.Min(duct.StartPoint.Z, duct.EndPoint.Z);
        
        // 50mm offset above floor = ~0.164 feet
        const double floorOffsetFeet = 0.164;
        // 1.8m max gap = ~5.91 feet
        const double maxGapFeet = 5.90551;
        
        // Step 1: Find all floors the duct passes through
        var floorsInRange = _hostDetector.FindAllFloorsInRange(ductCenterX, ductCenterY, bottomZ, topZ);
        
        // Create list of floor support elevations (at floor top + offset)
        var floorSupportElevations = new List<double>();
        
        foreach (var floor in floorsInRange)
        {
            double supportZ = floor.FloorTopElevation + floorOffsetFeet;
            
            // Ensure support is within duct bounds
            if (supportZ >= bottomZ && supportZ <= topZ)
            {
                floorSupportElevations.Add(supportZ);
                
                // Create floor-based support placement
                var placement = CreateFloorBasedVerticalSupportPlacement(duct, supportZ, floor, supportWidth);
                if (placement != null)
                {
                    placement.TributaryLength = maxGapFeet; // Will be adjusted
                    placements.Add(placement);
                }
            }
        }
        
        // Step 2: Fill gaps > 1.8m with wall supports using uniform partitioning
        // Create boundary points including duct bottom and top
        var allElevations = new List<double> { bottomZ };
        allElevations.AddRange(floorSupportElevations);
        allElevations.Add(topZ);
        allElevations = allElevations.Distinct().OrderBy(z => z).ToList();
        
        // Check each gap between consecutive elevations
        for (int i = 0; i < allElevations.Count - 1; i++)
        {
            double gapStart = allElevations[i];
            double gapEnd = allElevations[i + 1];
            double gapLength = gapEnd - gapStart;
            
            // If gap > 1.8m, add wall supports using uniform partitioning
            if (gapLength > maxGapFeet)
            {
                // Calculate number of divisions needed so each segment ≤ 1.8m
                int divisions = (int)Math.Ceiling(gapLength / maxGapFeet);
                double segmentLength = gapLength / divisions;
                
                // Place supports at segment boundaries (not at gapStart or gapEnd)
                for (int j = 1; j < divisions; j++)
                {
                    double supportZ = gapStart + (j * segmentLength);
                    
                    // Ensure within duct bounds
                    if (supportZ <= bottomZ || supportZ >= topZ)
                        continue;
                    
                    // Check for wall at this elevation
                    XYZ pointAtElevation = new XYZ(ductCenterX, ductCenterY, supportZ);
                    var wallResult = _wallChecker.FindNearestWall(pointAtElevation, _config.VerticalDuctWallProximityFeet);
                    
                    if (wallResult.IsNearWall)
                    {
                        // Wall exists - create wall-based support
                        var placement = CreateVerticalSupportPlacement(duct, supportZ, wallResult, supportWidth);
                        if (placement != null)
                        {
                            placement.TributaryLength = segmentLength;
                            placements.Add(placement);
                        }
                    }
                    else
                    {
                        // No wall found - try ceiling-based support (RecDuctSupport) as fallback
                        // Raycast upward to find ceiling/slab/beam above
                        XYZ raycastStart = new XYZ(ductCenterX, ductCenterY, supportZ);
                        if (RaycastUpward(raycastStart, out double distanceToCeiling, out ElementId hostId))
                        {
                            // Found ceiling/slab/beam - create ceiling-based support for vertical duct
                            var level = GetLevelForElevation(supportZ);
                            var placement = new SupportPlacement
                            {
                                DuctId = duct.ElementId,
                                SupportType = SupportType.Ceiling,
                                Location = new XYZ(ductCenterX, ductCenterY, supportZ),
                                Rotation = 0, // No rotation needed for vertical duct
                                LevelId = level?.Id ?? ElementId.InvalidElementId,
                                LevelName = level?.Name ?? "Unknown",
                                HostId = hostId,
                                // RecDuctSupport parameters
                                DuctHeight = distanceToCeiling,  // Distance to ceiling/slab/beam
                                DuctWidth = Math.Max(duct.Width, duct.Height),  // Use larger dimension
                                SupportWidth = supportWidth,
                                TributaryLength = segmentLength
                            };
                            placements.Add(placement);
                        }
                        // If no ceiling either, skip this support point (no host available)
                    }
                }
            }
        }
        
        // Sort placements by elevation for consistent ordering
        placements = placements.OrderBy(p => p.Location.Z).ToList();
        
        // Set marks after sorting (only once to avoid duplicates)
        // Include ductPrefix for uniqueness across all ducts
        for (int i = 0; i < placements.Count; i++)
        {
            placements[i].Mark = $"V{ductPrefix}-{i + 1:D3}";
        }
        
        return placements;
    }
    
    /// <summary>
    /// Get vertical support spacing based on duct size per SMACNA/ASHRAE standards.
    /// </summary>
    /// <param name="ductSize">Largest duct dimension in feet</param>
    /// <returns>Spacing in feet</returns>
    private double GetVerticalSupportSpacing(double ductSize)
    {
        // Convert duct size to mm for comparison
        double ductSizeMm = ductSize * 304.8;
        
        // SMACNA spacing guidelines for vertical ducts:
        // ≤ 750mm (30"): 3000mm (10') max spacing
        // 750-1500mm (30-60"): 2400mm (8') max spacing
        // > 1500mm (60"): 1800mm (6') max spacing
        
        if (ductSizeMm <= 750)
            return 9.84252; // 3000mm = 3m ≈ 9.84 feet
        else if (ductSizeMm <= 1500)
            return 7.87402; // 2400mm = 2.4m ≈ 7.87 feet
        else
            return 5.90551; // 1800mm = 1.8m ≈ 5.91 feet
    }
    
    /// <summary>
    /// Create a floor-based vertical duct support placement (VerticalFloorSupportDuct.rfa).
    /// 
    /// Family info:
    /// - Floor-based family, rectangular support surrounding vertical duct
    /// - Parameters: X = duct dimension along X axis, Y = duct dimension along Y axis
    /// </summary>
    private SupportPlacement? CreateFloorBasedVerticalSupportPlacement(
        DuctInfo duct, 
        double elevationZ, 
        FloorProximityResult floorResult, 
        double supportWidth)
    {
        var level = GetLevelForElevation(elevationZ);
        
        // Get duct center position at this elevation
        XYZ ductCenter = new XYZ(
            (duct.StartPoint.X + duct.EndPoint.X) / 2,
            (duct.StartPoint.Y + duct.EndPoint.Y) / 2,
            elevationZ
        );
        
        // Determine which duct dimension is along X axis and which is along Y axis
        // For vertical ducts, WidthDirection and HeightDirection are in the XY plane
        double floorSupportX;
        double floorSupportY;
        
        // Compare WidthDirection with global X axis (BasisX)
        double widthDotX = Math.Abs(duct.WidthDirection.DotProduct(XYZ.BasisX));
        double widthDotY = Math.Abs(duct.WidthDirection.DotProduct(XYZ.BasisY));
        
        if (widthDotX > widthDotY)
        {
            // WidthDirection is more aligned with X axis
            // Width goes along X, Height goes along Y
            floorSupportX = duct.Width;
            floorSupportY = duct.Height;
        }
        else
        {
            // WidthDirection is more aligned with Y axis
            // Width goes along Y, Height goes along X
            floorSupportX = duct.Height;
            floorSupportY = duct.Width;
        }
        
        return new SupportPlacement
        {
            DuctId = duct.ElementId,
            SupportType = SupportType.VerticalFloor,
            Location = ductCenter,
            Direction = XYZ.BasisX,
            Rotation = 0,
            LevelId = level?.Id ?? ElementId.InvalidElementId,
            LevelName = level?.Name ?? "Unknown",
            HostId = floorResult.FloorId,
            HostFace = floorResult.FloorFace,
            // VerticalFloorSupportDuct parameters
            FloorSupportX = floorSupportX,
            FloorSupportY = floorSupportY,
            SupportWidth = supportWidth,
            // Additional info
            BottomOfDuct = elevationZ,
            SupportHeight = elevationZ - floorResult.FloorTopElevation
        };
    }

    /// <summary>
    /// Create a vertical duct support placement (VerticalWallSupportDuct.rfa).
    /// 
    /// Family info:
    /// - Wall-based family, U-shaped bracket surrounding vertical duct
    /// - Origin at wall face (family placed on wall)
    /// - Arm extends from wall toward duct
    /// 
    /// Parameters:
    /// - Clamp_Size = duct face parallel to wall (width of the clamp)
    /// - Arm_Length = distance from wall face to far edge of duct ("end of duct" reference plane)
    ///              = (distance from wall to duct center) + (half of duct dimension perpendicular to wall)
    /// </summary>
    private SupportPlacement? CreateVerticalSupportPlacement(
    DuctInfo duct,
    double elevationZ,
    WallProximityChecker.WallProximityResult wallResult,
    double supportWidth)
    {
        var level = GetLevelForElevation(elevationZ);

        // -------------------------------
        // 1. Duct center at this elevation
        // -------------------------------
        double ductCenterX = (duct.StartPoint.X + duct.EndPoint.X) / 2;
        double ductCenterY = (duct.StartPoint.Y + duct.EndPoint.Y) / 2;

        XYZ ductCenter = new XYZ(ductCenterX, ductCenterY, elevationZ);

        // -------------------------------
        // 2. Place EXACTLY on wall face
        // -------------------------------
        // wallResult.Distance = distance from duct center → wall
        // wallResult.NormalToWall = direction from wall → duct

        // Move from duct center toward wall
        // small epsilon keeps it on correct face (Revit stability)
        double epsilon = 0.01; // ~3 mm

        XYZ placementPosition = ductCenter -
            wallResult.NormalToWall * (wallResult.Distance - epsilon);

        // -------------------------------
        // 3. Direction (wall → duct)
        // -------------------------------
        XYZ directionTowardDuct = wallResult.NormalToWall;

        double rotation = 0;

        // -------------------------------
        // 4. Determine duct orientation relative to wall
        // -------------------------------
        XYZ wallNormal = wallResult.NormalToWall;

        double widthDot = Math.Abs(duct.WidthDirection.DotProduct(wallNormal));
        double heightDot = Math.Abs(duct.HeightDirection.DotProduct(wallNormal));

        double ductDimParallelToWall;
        double ductDimPerpToWall;

        if (widthDot > heightDot)
        {
            // Width faces wall → perpendicular
            ductDimPerpToWall = duct.Width;
            ductDimParallelToWall = duct.Height;
        }
        else
        {
            // Height faces wall → perpendicular
            ductDimPerpToWall = duct.Height;
            ductDimParallelToWall = duct.Width;
        }

        // -------------------------------
        // 5. CORRECT arm length
        // -------------------------------
        // Distance = center → wall
        // Need to reach far edge of duct

        double verticalSupportLength =
            wallResult.Distance + (ductDimPerpToWall / 2);

        if (verticalSupportLength < 0.01)
            verticalSupportLength = 0.01;

        // -------------------------------
        // 6. Create placement
        // -------------------------------
        return new SupportPlacement
        {
            DuctId = duct.ElementId,
            SupportType = SupportType.Vertical,

            Location = placementPosition,        // ✅ wall face
            Direction = directionTowardDuct,     // ✅ toward duct
            Rotation = rotation,

            LevelId = level?.Id ?? ElementId.InvalidElementId,
            LevelName = level?.Name ?? "Unknown",
            HostId = wallResult.WallId,

            // Family parameters
            VerticalSupportWidth = ductDimParallelToWall,   // Clamp size
            VerticalSupportLength = verticalSupportLength,  // Arm length
            SupportWidth = supportWidth
        };
    }

    /// <summary>
    /// Calculate placement for horizontal duct support.
    /// Priority: Wall support (if near wall) > Ceiling support (if ceiling found) > Ground support
    /// </summary>
    private SupportPlacement? CalculateHorizontalPlacement(DuctInfo duct, XYZ position, double supportWidth, InsulationInfo insulation)
    {
        // Check wall proximity first
        if (_config.PreferWallSupports)
        {
            var wallResult = _wallChecker.CheckProximityWithDuctDirection(position, duct.Direction, duct.Width);
            if (wallResult.IsNearWall)
            {
                return CreateWallSupportPlacement(duct, position, wallResult);
            }
        }
        
        // Get level for placement
        var level = GetLevelForElevation(duct.BottomElevation);
        
        // Raycast upward from duct bottom to find ceiling, slab, or beam
        double ductBottomZ = duct.BottomElevation;
        XYZ raycastStart = new XYZ(position.X, position.Y, ductBottomZ);
        var ceilingBeamResult = RaycastUpward(raycastStart, out double distanceToCeiling, out ElementId hostId);
        
        if (ceilingBeamResult)
        {
            // Found ceiling, slab, or beam - use RecDuctSupport family
            // Support is placed at duct bottom elevation, perpendicular to duct direction
            return new SupportPlacement
            {
                DuctId = duct.ElementId,
                SupportType = SupportType.Ceiling,
                Location = new XYZ(position.X, position.Y, ductBottomZ),
                Rotation = GetPerpendicularRotation(duct.Direction),
                LevelId = level?.Id ?? ElementId.InvalidElementId,
                LevelName = level?.Name ?? "Unknown",
                HostId = hostId,
                // RecDuctSupport parameters
                DuctHeight = distanceToCeiling,  // Distance from duct bottom to ceiling/slab/beam
                DuctWidth = duct.Width,           // Duct width
                SupportWidth = supportWidth
            };
        }
        else
        {
            // No ceiling above - use GroundDuctSupport family
            // Raycast downward to find the nearest floor below the duct
            var floorResult = RaycastDownward(raycastStart, out double distanceToFloor, out ElementId floorId);
            
            // Support_Height = distance from floor surface to duct bottom
            double supportHeight = floorResult ? distanceToFloor : ductBottomZ;
            
            // Family info:
            // - Generic family, center at duct center
            // - Elevation = bottom of duct
            // - Support_Height = distance from nearest floor below to duct bottom
            // - Duct_Width = duct width
            
            return new SupportPlacement
            {
                DuctId = duct.ElementId,
                SupportType = SupportType.Ground,
                Location = new XYZ(position.X, position.Y, ductBottomZ),
                Rotation = GetPerpendicularRotation(duct.Direction),
                LevelId = level?.Id ?? ElementId.InvalidElementId,
                LevelName = level?.Name ?? "Unknown",
                HostId = floorId,
                // GroundDuctSupport parameters
                SupportHeight = supportHeight,    // Distance from floor to duct bottom
                DuctWidth = duct.Width,           // Duct width
                SupportWidth = supportWidth
            };
        }
    }
    
    /// <summary>
    /// Create wall support placement for horizontal duct (WallSupportDuct.rfa).
    ///  
    /// Family info:
    /// - Generic model, base at wall face
    /// - Elevation = bottom of duct (set by placement Z coordinate)
    /// - Support is perpendicular to wall, extending toward duct
    /// 
    /// Parameters:
    /// - Support_Length = duct width + 30mm + offset from nearest duct face to wall
    /// </summary>
    private SupportPlacement CreateWallSupportPlacement(DuctInfo duct, XYZ position, 
        WallProximityChecker.WallProximityResult wallResult)
    {
        var level = GetLevelForElevation(duct.BottomElevation);
        
        // wallResult.Distance is now the distance from nearest duct face to wall
        // (updated in WallProximityChecker.CheckProximityWithDuctDirection)
        double offsetFromDuctToWall = wallResult.Distance;
        
        // 30mm = 0.0984252 feet
        const double extraClearance = 0.0984252;
        
        // Support_Length = duct width + 30mm + offset from nearest duct face to wall
        double wallSupportLength = duct.Width + extraClearance + offsetFromDuctToWall;
        
        // Place family at wall face position
        // Move from duct center toward wall by the distance to wall face
        double distanceFromCenterToWall = offsetFromDuctToWall + (duct.Width / 2);
        XYZ wallPosition = position + wallResult.NormalToWall.Negate() * distanceFromCenterToWall;
        
        // Rotation: perpendicular to wall, support extends toward duct
        // Family's Support_Length extends along -Y axis by default (toward back)
        // NormalToWall points from wall toward duct
        double rotation = Math.Atan2(wallResult.NormalToWall.X, wallResult.NormalToWall.Y);
        
        // For Y-axis walls (normal points along X-axis), add 180° to face toward duct
        if (Math.Abs(wallResult.NormalToWall.X) > Math.Abs(wallResult.NormalToWall.Y))
        {
            rotation += Math.PI;
        }
        
        return new SupportPlacement
        {
            DuctId = duct.ElementId,
            SupportType = SupportType.Wall,
            Location = new XYZ(wallPosition.X, wallPosition.Y, duct.BottomElevation),
            Direction = wallResult.NormalToWall,
            Rotation = rotation,
            LevelId = level?.Id ?? ElementId.InvalidElementId,
            LevelName = level?.Name ?? "Unknown",
            HostId = wallResult.WallId,
            HostFace = wallResult.WallFace,
            // WallSupportDuct parameters
            WallSupportLength = wallSupportLength,  // Support_Length
            DuctWidth = duct.Width,
            SupportWidth = duct.Width + _config.ClearanceFeet
        };
    }
    
    /// <summary>
    /// Raycast upward from a point to find ceiling, slab, or beam.
    /// Uses multiple detection methods for reliability.
    /// </summary>
    private bool RaycastUpward(XYZ startPoint, out double distance, out ElementId hostId)
    {
        distance = 0;
        hostId = ElementId.InvalidElementId;
        
        double minDistance = double.MaxValue;
        string foundElement = "None";
        var diagnosticLines = new List<string>();
        
        diagnosticLines.Add($"Raycast from Z={startPoint.Z:F2}ft ({startPoint.Z * 304.8:F0}mm)");
        diagnosticLines.Add($"XY=({startPoint.X:F2}, {startPoint.Y:F2})");
        
        // Check all potential ceiling/slab elements
        var allElements = new List<(Element element, string type)>();
        
        if (_cachedFloors != null)
            allElements.AddRange(_cachedFloors.Select(e => (e, "Floor")));
        if (_cachedStructuralFloors != null)
            allElements.AddRange(_cachedStructuralFloors.Select(e => (e, "StructuralFloor")));
        if (_cachedCeilings != null)
            allElements.AddRange(_cachedCeilings.Select(e => (e, "Ceiling")));
        if (_cachedBeams != null)
            allElements.AddRange(_cachedBeams.Select(e => (e, "Beam")));
        
        diagnosticLines.Add($"Total elements to check: {allElements.Count}");
        
        foreach (var (element, type) in allElements)
        {
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) continue;
            
            // Large tolerance for testing
            const double tolerance = 2.0; // 2 feet tolerance
            
            bool inXYBounds = startPoint.X >= bbox.Min.X - tolerance && startPoint.X <= bbox.Max.X + tolerance &&
                              startPoint.Y >= bbox.Min.Y - tolerance && startPoint.Y <= bbox.Max.Y + tolerance;
            
            bool isAbove = bbox.Min.Z > startPoint.Z;
            
            if (inXYBounds && isAbove)
            {
                double d = bbox.Min.Z - startPoint.Z;
                diagnosticLines.Add($"  Found {type} id={element.Id} at Z={bbox.Min.Z:F2}ft, dist={d:F2}ft");
                
                if (d < minDistance && d > 0.01)
                {
                    minDistance = d;
                    hostId = element.Id;
                    foundElement = $"{type} id={element.Id}";
                }
            }
        }
        
        // Method 2: Try ReferenceIntersector as fallback
        if (minDistance == double.MaxValue)
        {
            diagnosticLines.Add("Trying ReferenceIntersector...");
            try
            {
                var view3D = Get3DView();
                if (view3D != null)
                {
                    var filter = new ElementMulticategoryFilter(new[]
                    {
                        BuiltInCategory.OST_Floors,
                        BuiltInCategory.OST_Ceilings,
                        BuiltInCategory.OST_StructuralFraming,
                        BuiltInCategory.OST_StructuralFoundation
                    });
                    
                    var refIntersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, view3D);
                    refIntersector.FindReferencesInRevitLinks = true;
                    
                    var results = refIntersector.Find(startPoint, XYZ.BasisZ);
                    
                    if (results != null && results.Count > 0)
                    {
                        var closest = results.OrderBy(r => r.Proximity).First();
                        if (closest.Proximity > 0.01)
                        {
                            minDistance = closest.Proximity;
                            var reference = closest.GetReference();
                            hostId = reference.ElementId;
                            foundElement = $"RaycastHit id={hostId}";
                            diagnosticLines.Add($"  ReferenceIntersector found element at dist={closest.Proximity:F2}ft");
                        }
                    }
                    else
                    {
                        diagnosticLines.Add("  ReferenceIntersector found nothing");
                    }
                }
                else
                {
                    diagnosticLines.Add("  No 3D view available");
                }
            }
            catch (Exception ex)
            {
                diagnosticLines.Add($"  ReferenceIntersector error: {ex.Message}");
            }
        }
        
        diagnosticLines.Add($"Result: {(minDistance < double.MaxValue ? $"Found {foundElement} at {minDistance:F2}ft" : "Nothing found")}");
        LastDiagnostic = string.Join("\n", diagnosticLines);
        
        if (minDistance < double.MaxValue && minDistance < 100) // Max 100 feet to ceiling
        {
            distance = minDistance;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Raycast downward from a point to find floor or roof below.
    /// Used for ground supports to find the surface to place the support on.
    /// </summary>
    private bool RaycastDownward(XYZ startPoint, out double distance, out ElementId hostId)
    {
        distance = 0;
        hostId = ElementId.InvalidElementId;
        
        double minDistance = double.MaxValue;
        
        // Check floors and structural elements below the start point
        var elementsToCheck = new List<Element>();
        
        if (_cachedFloors != null)
            elementsToCheck.AddRange(_cachedFloors);
        if (_cachedStructuralFloors != null)
            elementsToCheck.AddRange(_cachedStructuralFloors);
        
        foreach (var element in elementsToCheck)
        {
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) continue;
            
            const double tolerance = 2.0; // 2 feet tolerance
            
            bool inXYBounds = startPoint.X >= bbox.Min.X - tolerance && startPoint.X <= bbox.Max.X + tolerance &&
                              startPoint.Y >= bbox.Min.Y - tolerance && startPoint.Y <= bbox.Max.Y + tolerance;
            
            // Element is below start point (top of element is below start)
            bool isBelow = bbox.Max.Z < startPoint.Z;
            
            if (inXYBounds && isBelow)
            {
                // Distance from start point down to top of element
                double d = startPoint.Z - bbox.Max.Z;
                
                if (d < minDistance && d > 0.01)
                {
                    minDistance = d;
                    hostId = element.Id;
                }
            }
        }
        
        // Try ReferenceIntersector as fallback
        if (minDistance == double.MaxValue)
        {
            try
            {
                var view3D = Get3DView();
                if (view3D != null)
                {
                    var filter = new ElementMulticategoryFilter(new[]
                    {
                        BuiltInCategory.OST_Floors,
                        BuiltInCategory.OST_StructuralFoundation,
                        BuiltInCategory.OST_Roofs
                    });
                    
                    var refIntersector = new ReferenceIntersector(filter, FindReferenceTarget.Face, view3D);
                    refIntersector.FindReferencesInRevitLinks = true;
                    
                    // Raycast downward (negative Z)
                    var results = refIntersector.Find(startPoint, XYZ.BasisZ.Negate());
                    
                    if (results != null && results.Count > 0)
                    {
                        var closest = results.OrderBy(r => r.Proximity).First();
                        if (closest.Proximity > 0.01)
                        {
                            minDistance = closest.Proximity;
                            var reference = closest.GetReference();
                            hostId = reference.ElementId;
                        }
                    }
                }
            }
            catch
            {
                // Raycast failed, will use ductBottomZ as fallback
            }
        }
        
        if (minDistance < double.MaxValue && minDistance < 200) // Max 200 feet to floor
        {
            distance = minDistance;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get a 3D view for raycasting.
    /// </summary>
    private View3D? Get3DView()
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate);
    }
    
    /// <summary>
    /// Get rotation angle from direction vector.
    /// </summary>
    private double GetRotationAngle(XYZ direction)
    {
        return Math.Atan2(direction.Y, direction.X);
    }
    
    /// <summary>
    /// Get perpendicular rotation to duct direction (for supports that cross the duct).
    /// </summary>
    private double GetPerpendicularRotation(XYZ ductDirection)
    {
        // Rotate 90 degrees from duct direction
        return Math.Atan2(ductDirection.Y, ductDirection.X) + Math.PI / 2;
    }
    
    /// <summary>
    /// Get level for a given elevation.
    /// </summary>
    private Level? GetLevelForElevation(double elevation)
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Where(l => l.Elevation <= elevation)
            .OrderByDescending(l => l.Elevation)
            .FirstOrDefault();
    }
}
