using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Hosting;

/// <summary>
/// Detects host elements (ceiling, beam, floor) for support placement.
/// </summary>
public class HostDetector
{
    private readonly Document _doc;
    private readonly View3D? _view3D;
    
    public HostDetector(Document doc)
    {
        _doc = doc;
        _view3D = Get3DView();
    }
    
    /// <summary>
    /// Detect appropriate host for a support at the given location.
    /// </summary>
    public HostInfo DetectHost(XYZ point, double ductBottomElevation, DuctOrientation orientation)
    {
        if (orientation == DuctOrientation.Vertical)
        {
            // Vertical ducts need wall hosting
            return new HostInfo
            {
                SupportType = SupportType.Vertical,
                HostCategory = "Wall"
            };
        }
        
        // For horizontal ducts, first try to find ceiling/beam above
        var ceilingHost = FindCeilingOrBeamAbove(point, ductBottomElevation);
        if (ceilingHost.IsValid)
        {
            return ceilingHost;
        }
        
        // No ceiling found, use ground support
        var floorHost = FindFloorBelow(point, ductBottomElevation);
        return floorHost;
    }
    
    /// <summary>
    /// Find ceiling or beam above the point using ray casting.
    /// </summary>
    private HostInfo FindCeilingOrBeamAbove(XYZ point, double ductBottomElevation)
    {
        if (_view3D == null)
            return FindCeilingOrBeamAboveByCollector(point, ductBottomElevation);
        
        try
        {
            // Create filter for ceilings and structural framing (beams)
            var categoryFilter = new ElementMulticategoryFilter(new List<BuiltInCategory>
            {
                BuiltInCategory.OST_Ceilings,
                BuiltInCategory.OST_StructuralFraming
            });
            
            var intersector = new ReferenceIntersector(categoryFilter, FindReferenceTarget.Face, _view3D);
            intersector.FindReferencesInRevitLinks = true;
            
            // Cast ray upward from duct bottom
            XYZ rayStart = new XYZ(point.X, point.Y, ductBottomElevation + 0.1);
            XYZ rayDirection = XYZ.BasisZ;
            
            var result = intersector.FindNearest(rayStart, rayDirection);
            
            if (result != null)
            {
                Element? hostElement = _doc.GetElement(result.GetReference());
                if (hostElement != null)
                {
                    double hostBottomZ = GetElementBottomElevation(hostElement);
                    double rodLength = hostBottomZ - ductBottomElevation;
                    
                    return new HostInfo
                    {
                        SupportType = SupportType.Ceiling,
                        HostId = hostElement.Id,
                        HostFace = result.GetReference(),
                        Distance = result.Proximity,
                        HostBottomElevation = hostBottomZ,
                        HostCategory = hostElement.Category?.Name ?? "Unknown",
                        HostName = hostElement.Name
                    };
                }
            }
        }
        catch
        {
            // Fall back to collector method
            return FindCeilingOrBeamAboveByCollector(point, ductBottomElevation);
        }
        
        return new HostInfo { SupportType = SupportType.Ground };
    }
    
    /// <summary>
    /// Find ceiling or beam using bounding box intersection (fallback method).
    /// </summary>
    private HostInfo FindCeilingOrBeamAboveByCollector(XYZ point, double ductBottomElevation)
    {
        double searchHeight = 50; // Search up to 50 feet above
        
        // Create outline for intersection
        var min = new XYZ(point.X - 0.5, point.Y - 0.5, ductBottomElevation);
        var max = new XYZ(point.X + 0.5, point.Y + 0.5, ductBottomElevation + searchHeight);
        var outline = new Outline(min, max);
        
        var bbFilter = new BoundingBoxIntersectsFilter(outline);
        
        // Search for ceilings
        var ceilings = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Ceilings)
            .WherePasses(bbFilter)
            .WhereElementIsNotElementType()
            .ToElements();
        
        // Search for beams (excluding BeamSystem elements)
        var beams = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WherePasses(bbFilter)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => !(e is BeamSystem));
        
        // Combine and find closest above
        var allElements = ceilings.Concat(beams).ToList();
        Element? closest = null;
        double closestDist = double.MaxValue;
        
        foreach (var element in allElements)
        {
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) continue;
            
            double elementBottomZ = bbox.Min.Z;
            if (elementBottomZ > ductBottomElevation)
            {
                double dist = elementBottomZ - ductBottomElevation;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = element;
                }
            }
        }
        
        if (closest != null)
        {
            double hostBottomZ = GetElementBottomElevation(closest);
            return new HostInfo
            {
                SupportType = SupportType.Ceiling,
                HostId = closest.Id,
                Distance = closestDist,
                HostBottomElevation = hostBottomZ,
                HostCategory = closest.Category?.Name ?? "Unknown",
                HostName = closest.Name
            };
        }
        
        return new HostInfo { SupportType = SupportType.Ground };
    }
    
    /// <summary>
    /// Find floor below the point.
    /// </summary>
    private HostInfo FindFloorBelow(XYZ point, double ductBottomElevation)
    {
        // Find floor level below duct
        var levels = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Where(l => l.Elevation < ductBottomElevation)
            .OrderByDescending(l => l.Elevation)
            .ToList();
        
        Level? floorLevel = levels.FirstOrDefault();
        double floorElevation = floorLevel?.Elevation ?? 0;
        
        // Try to find actual floor element
        var outline = new Outline(
            new XYZ(point.X - 1, point.Y - 1, floorElevation - 1),
            new XYZ(point.X + 1, point.Y + 1, floorElevation + 1));
        
        var bbFilter = new BoundingBoxIntersectsFilter(outline);
        
        var floors = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WherePasses(bbFilter)
            .WhereElementIsNotElementType()
            .ToElements();
        
        var floor = floors.FirstOrDefault();
        
        if (floor != null)
        {
            var bbox = floor.get_BoundingBox(null);
            floorElevation = bbox?.Max.Z ?? floorElevation;
        }
        
        double supportHeight = ductBottomElevation - floorElevation;
        
        return new HostInfo
        {
            SupportType = SupportType.Ground,
            HostId = floor?.Id ?? ElementId.InvalidElementId,
            HostTopElevation = floorElevation,
            Distance = supportHeight,
            HostCategory = "Floor",
            HostName = floor?.Name ?? floorLevel?.Name ?? "Ground"
        };
    }
    
    /// <summary>
    /// Get bottom elevation of an element.
    /// </summary>
    private double GetElementBottomElevation(Element element)
    {
        var bbox = element.get_BoundingBox(null);
        return bbox?.Min.Z ?? 0;
    }
    
    /// <summary>
    /// Get or create a 3D view for ray casting.
    /// </summary>
    private View3D? Get3DView()
    {
        try
        {
            // Try to find existing 3D view
            var view3D = new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => !v.IsTemplate && v.Name.Contains("{3D}"));
            
            return view3D;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Find nearest floor below a point for vertical duct support fallback.
    /// Used when no wall is found within proximity for vertical ducts.
    /// </summary>
    /// <param name="point">The point to search from</param>
    /// <returns>Floor info with face reference for placement</returns>
    public FloorProximityResult FindNearestFloorBelow(XYZ point)
    {
        try
        {
            // First try raycast if 3D view available
            if (_view3D != null)
            {
                var categoryFilter = new ElementMulticategoryFilter(new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_Floors,
                    BuiltInCategory.OST_StructuralFoundation
                });
                
                var intersector = new ReferenceIntersector(categoryFilter, FindReferenceTarget.Face, _view3D);
                intersector.FindReferencesInRevitLinks = true;
                
                // Cast ray downward
                XYZ rayStart = new XYZ(point.X, point.Y, point.Z - 0.1);
                XYZ rayDirection = -XYZ.BasisZ;
                
                var result = intersector.FindNearest(rayStart, rayDirection);
                
                if (result != null)
                {
                    Element? floorElement = _doc.GetElement(result.GetReference());
                    if (floorElement != null)
                    {
                        var bbox = floorElement.get_BoundingBox(null);
                        double floorTopZ = bbox?.Max.Z ?? point.Z - result.Proximity;
                        
                        return new FloorProximityResult
                        {
                            IsFloorFound = true,
                            FloorId = floorElement.Id,
                            FloorFace = result.GetReference(),
                            FloorTopElevation = floorTopZ,
                            Distance = result.Proximity,
                            FloorName = floorElement.Name
                        };
                    }
                }
            }
            
            // Fallback: use bounding box intersection
            return FindNearestFloorBelowByCollector(point);
        }
        catch
        {
            return FindNearestFloorBelowByCollector(point);
        }
    }
    
    /// <summary>
    /// Find nearest floor below using collector (fallback method).
    /// </summary>
    private FloorProximityResult FindNearestFloorBelowByCollector(XYZ point)
    {
        double searchDepth = 100; // Search down 100 feet
        
        var min = new XYZ(point.X - 1, point.Y - 1, point.Z - searchDepth);
        var max = new XYZ(point.X + 1, point.Y + 1, point.Z);
        var outline = new Outline(min, max);
        
        var bbFilter = new BoundingBoxIntersectsFilter(outline);
        
        // Search for floors
        var floors = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_Floors)
            .WherePasses(bbFilter)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        // Also check structural foundations
        var foundations = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralFoundation)
            .WherePasses(bbFilter)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
        
        var allFloors = floors.Concat(foundations).ToList();
        
        Element? nearestFloor = null;
        double nearestTopZ = double.MinValue;
        
        foreach (var floor in allFloors)
        {
            var bbox = floor.get_BoundingBox(null);
            if (bbox == null) continue;
            
            double floorTopZ = bbox.Max.Z;
            
            // Floor must be below the point
            if (floorTopZ < point.Z && floorTopZ > nearestTopZ)
            {
                nearestTopZ = floorTopZ;
                nearestFloor = floor;
            }
        }
        
        if (nearestFloor != null)
        {
            return new FloorProximityResult
            {
                IsFloorFound = true,
                FloorId = nearestFloor.Id,
                FloorTopElevation = nearestTopZ,
                Distance = point.Z - nearestTopZ,
                FloorName = nearestFloor.Name,
                FloorFace = GetFloorTopFaceReference(nearestFloor)
            };
        }
        
        // Last resort: use lowest level
        var levels = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Where(l => l.Elevation < point.Z)
            .OrderByDescending(l => l.Elevation)
            .ToList();
        
        var level = levels.FirstOrDefault();
        if (level != null)
        {
            return new FloorProximityResult
            {
                IsFloorFound = true,
                FloorTopElevation = level.Elevation,
                Distance = point.Z - level.Elevation,
                FloorName = level.Name
            };
        }
        
        return new FloorProximityResult { IsFloorFound = false };
    }
    
    /// <summary>
    /// Get the top face reference from a floor element.
    /// </summary>
    private Reference? GetFloorTopFaceReference(Element floor)
    {
        try
        {
            var options = new Options { ComputeReferences = true };
            var geometry = floor.get_Geometry(options);
            if (geometry == null)
                return null;
            
            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    // Find the top face (normal pointing up)
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            XYZ normal = planarFace.FaceNormal;
                            // Top face has normal pointing up (Z > 0.9)
                            if (normal.Z > 0.9)
                            {
                                return face.Reference;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Return null if face reference cannot be obtained
        }
        
        return null;
    }
    
    /// <summary>
    /// Find all floors that a vertical duct passes through within a Z range.
    /// Returns floors ordered by elevation (lowest to highest).
    /// </summary>
    /// <param name="centerX">Duct center X coordinate</param>
    /// <param name="centerY">Duct center Y coordinate</param>
    /// <param name="bottomZ">Bottom of vertical duct</param>
    /// <param name="topZ">Top of vertical duct</param>
    /// <returns>List of floor elevations (top surface Z) where duct passes through</returns>
    public List<FloorProximityResult> FindAllFloorsInRange(double centerX, double centerY, double bottomZ, double topZ)
    {
        var results = new List<FloorProximityResult>();
        
        try
        {
            // Search area around the duct center
            double searchRadius = 5.0; // 5 feet radius
            var min = new XYZ(centerX - searchRadius, centerY - searchRadius, bottomZ - 1);
            var max = new XYZ(centerX + searchRadius, centerY + searchRadius, topZ + 1);
            var outline = new Outline(min, max);
            
            var bbFilter = new BoundingBoxIntersectsFilter(outline);
            
            // Find all floors in the bounding box
            var floors = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WherePasses(bbFilter)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
            
            // Also check structural foundations
            var foundations = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_StructuralFoundation)
                .WherePasses(bbFilter)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
            
            var allFloors = floors.Concat(foundations).ToList();
            
            foreach (var floor in allFloors)
            {
                var bbox = floor.get_BoundingBox(null);
                if (bbox == null) continue;
                
                // Check if floor is within XY proximity of duct center
                double floorMinX = bbox.Min.X;
                double floorMaxX = bbox.Max.X;
                double floorMinY = bbox.Min.Y;
                double floorMaxY = bbox.Max.Y;
                
                // Duct must be within floor XY bounds (with small tolerance)
                const double tolerance = 0.5; // 0.5 feet tolerance
                if (centerX < floorMinX - tolerance || centerX > floorMaxX + tolerance ||
                    centerY < floorMinY - tolerance || centerY > floorMaxY + tolerance)
                {
                    continue;
                }
                
                // Get floor top elevation
                double floorTopZ = bbox.Max.Z;
                double floorBottomZ = bbox.Min.Z;
                
                // Floor must be within the duct's Z range
                // The floor top should be above duct bottom and below duct top
                if (floorTopZ > bottomZ && floorTopZ < topZ)
                {
                    results.Add(new FloorProximityResult
                    {
                        IsFloorFound = true,
                        FloorId = floor.Id,
                        FloorTopElevation = floorTopZ,
                        Distance = 0,
                        FloorName = floor.Name,
                        FloorFace = GetFloorTopFaceReference(floor)
                    });
                }
            }
            
            // Sort by elevation (lowest to highest)
            results = results.OrderBy(r => r.FloorTopElevation).ToList();
        }
        catch
        {
            // Return empty list on error
        }
        
        return results;
    }
}

/// <summary>
/// Result of floor proximity check for vertical duct support fallback.
/// </summary>
public class FloorProximityResult
{
    public bool IsFloorFound { get; init; }
    public ElementId FloorId { get; init; } = ElementId.InvalidElementId;
    public Reference? FloorFace { get; init; }
    public double FloorTopElevation { get; init; }
    public double Distance { get; init; }
    public string FloorName { get; init; } = string.Empty;
}
