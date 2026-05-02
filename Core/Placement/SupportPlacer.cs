using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using DuctSupportAddin.Families;
using DuctSupportAddin.Models;
using System.IO;

namespace DuctSupportAddin.Core.Placement;

/// <summary>
/// Places support family instances in the Revit model.
/// </summary>
public class SupportPlacer
{
    private readonly Document _doc;
    private readonly FamilyLoader _familyLoader;
    private readonly ParameterSetter _parameterSetter;
    private readonly Configuration _config;
    
    // Family symbols for each support type
    private FamilySymbol? _ceilingSymbol;    // RecDuctSupport.rfa
    private FamilySymbol? _groundSymbol;     // GroundDuctSupport.rfa
    private FamilySymbol? _wallSymbol;       // WallSupportDuct.rfa
    private FamilySymbol? _verticalSymbol;   // VerticalWallSupportDuct.rfa
    private FamilySymbol? _verticalFloorSymbol; // VerticalFloorSupportDuct.rfa
    
    public SupportPlacer(Document doc, Configuration config)
    {
        _doc = doc;
        _config = config;
        _familyLoader = new FamilyLoader(doc);
        _parameterSetter = new ParameterSetter();
        
        LoadFamilies(config);
    }
    
    /// <summary>
    /// Load all required support families.
    /// </summary>
    private void LoadFamilies(Configuration config)
    {
        _ceilingSymbol = _familyLoader.LoadFamily(config.HorizontalCeilingSupportFamilyPath);
        _groundSymbol = _familyLoader.LoadFamily(config.HorizontalGroundSupportFamilyPath);
        _wallSymbol = _familyLoader.LoadFamily(config.HorizontalWallSupportFamilyPath);
        _verticalSymbol = _familyLoader.LoadFamily(config.VerticalWallSupportFamilyPath);
        _verticalFloorSymbol = _familyLoader.LoadFamily(config.VerticalFloorSupportFamilyPath);
    }
    
    /// <summary>
    /// Get appropriate family symbol for support type.
    /// </summary>
    private FamilySymbol? GetSymbol(SupportType type)
    {
        return type switch
        {
            SupportType.Ceiling => _ceilingSymbol,
            SupportType.Ground => _groundSymbol,
            SupportType.Wall => _wallSymbol,
            SupportType.Vertical => _verticalSymbol,
            SupportType.VerticalFloor => _verticalFloorSymbol,
            _ => null
        };
    }
    
    /// <summary>
    /// Check if all required families are loaded.
    /// </summary>
    public (bool success, List<string> missing, string searchPath) ValidateFamilies()
    {
        var missing = new List<string>();
        string familiesFolder = Configuration.FamiliesFolder;
        
        if (_ceilingSymbol == null)
        {
            string path = _config.HorizontalCeilingSupportFamilyPath;
            bool exists = File.Exists(path);
            missing.Add($"Ceiling Support ({_config.HorizontalCeilingSupportFamily}) - File exists: {exists}");
        }
        if (_groundSymbol == null)
        {
            string path = _config.HorizontalGroundSupportFamilyPath;
            bool exists = File.Exists(path);
            missing.Add($"Ground Support ({_config.HorizontalGroundSupportFamily}) - File exists: {exists}");
        }
        if (_wallSymbol == null)
        {
            string path = _config.HorizontalWallSupportFamilyPath;
            bool exists = File.Exists(path);
            missing.Add($"Wall Support ({_config.HorizontalWallSupportFamily}) - File exists: {exists}");
        }
        if (_verticalSymbol == null)
        {
            string path = _config.VerticalWallSupportFamilyPath;
            bool exists = File.Exists(path);
            missing.Add($"Vertical Wall Support ({_config.VerticalWallSupportFamily}) - File exists: {exists}");
        }
        if (_verticalFloorSymbol == null)
        {
            string path = _config.VerticalFloorSupportFamilyPath;
            bool exists = File.Exists(path);
            missing.Add($"Vertical Floor Support ({_config.VerticalFloorSupportFamily}) - File exists: {exists}");
        }
        
        return (missing.Count == 0, missing, familiesFolder);
    }
    
    /// <summary>
    /// Place a single support instance.
    /// </summary>
    public ElementId? PlaceSupport(SupportPlacement placement)
    {
        var symbol = GetSymbol(placement.SupportType);
        if (symbol == null)
            return null;
        
        // Ensure symbol is active
        if (!symbol.IsActive)
        {
            symbol.Activate();
            _doc.Regenerate();
        }
        
        // Get level
        Level? level = null;
        if (placement.LevelId != ElementId.InvalidElementId)
        {
            level = _doc.GetElement(placement.LevelId) as Level;
        }
        
        level ??= GetDefaultLevel();
        if (level == null)
            return null;
        
        FamilyInstance? instance = null;
        
        try
        {
            instance = placement.SupportType switch
            {
                SupportType.Ceiling => PlaceCeilingSupport(symbol, placement, level),
                SupportType.Ground => PlaceGroundSupport(symbol, placement, level),
                SupportType.Wall => PlaceWallSupport(symbol, placement, level),
                SupportType.Vertical => PlaceVerticalSupport(symbol, placement, level),
                SupportType.VerticalFloor => PlaceVerticalFloorSupport(symbol, placement, level),
                _ => null
            };
            
            if (instance != null)
            {
                // Set parameters
                _parameterSetter.SetParameters(instance, placement);
                
                // Set mark if specified
                if (!string.IsNullOrEmpty(placement.Mark))
                {
                    var markParam = instance.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    markParam?.Set(placement.Mark);
                }
                
                return instance.Id;
            }
        }
        catch
        {
            // Placement failed
        }
        
        return null;
    }
    
    /// <summary>
    /// Place ceiling/beam mounted support (RecDuctSupport.rfa).
    /// Perpendicular to duct direction.
    /// </summary>
    private FamilyInstance? PlaceCeilingSupport(FamilySymbol symbol, SupportPlacement placement, Level level)
    {
        // Place at duct bottom location
        var instance = _doc.Create.NewFamilyInstance(
            placement.Location,
            symbol,
            level,
            StructuralType.NonStructural);
        
        // Rotate to be perpendicular to duct
        if (Math.Abs(placement.Rotation) > 0.01)
        {
            var axis = Line.CreateBound(
                placement.Location,
                placement.Location + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, instance.Id, axis, placement.Rotation);
        }
        
        return instance;
    }
    
    /// <summary>
    /// Place ground/floor mounted support (GroundDuctSupport.rfa).
    /// Family info:
    /// - Generic family, center at duct center
    /// - Elevation = bottom of duct
    /// - Support_Height = distance from floor to duct bottom (family extends downward)
    /// </summary>
    private FamilyInstance? PlaceGroundSupport(FamilySymbol symbol, SupportPlacement placement, Level level)
    {
        // Place at XY location on level, then set offset for Z
        var locationOnLevel = new XYZ(placement.Location.X, placement.Location.Y, level.Elevation);
        
        var instance = _doc.Create.NewFamilyInstance(
            locationOnLevel,
            symbol,
            level,
            StructuralType.NonStructural);
        
        // Set elevation offset from level to duct bottom
        double offsetFromLevel = placement.Location.Z - level.Elevation;
        instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)?.Set(offsetFromLevel);
        
        // Rotate to be perpendicular to duct
        if (Math.Abs(placement.Rotation) > 0.01)
        {
            var axis = Line.CreateBound(
                placement.Location,
                placement.Location + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, instance.Id, axis, placement.Rotation);
        }
        
        return instance;
    }
    
    /// <summary>
    /// Place wall mounted support for horizontal duct (WallSupportDuct.rfa).
    /// Perpendicular to duct direction.
    /// </summary>
    private FamilyInstance? PlaceWallSupport(FamilySymbol symbol, SupportPlacement placement, Level level)
    {
        // Simple non-hosted placement - most reliable
        var instance = _doc.Create.NewFamilyInstance(
            placement.Location,
            symbol,
            level,
            StructuralType.NonStructural);
        
        // Rotate to be perpendicular to duct
        if (Math.Abs(placement.Rotation) > 0.01)
        {
            var axis = Line.CreateBound(
                placement.Location,
                placement.Location + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(_doc, instance.Id, axis, placement.Rotation);
        }
        
        return instance;
    }

    /// <summary>
    /// Place wall mounted support for vertical duct (VerticalWallSupportDuct.rfa).
    /// This is a wall-based family - must be placed on a wall face.
    /// The family arm extends from wall toward duct, controlled by Arm_Length parameter.
    /// </summary>
    private FamilyInstance? PlaceVerticalSupport(FamilySymbol symbol, SupportPlacement placement, Level level)
    {
        try
        {
            // -------------------------------
            // 0. Validate host
            // -------------------------------
            if (placement.HostId == ElementId.InvalidElementId)
                throw new Exception("Invalid Wall Host");

            Wall? wall = _doc.GetElement(placement.HostId) as Wall;
            if (wall == null)
                throw new Exception("Wall not found");

            // -------------------------------
            // 1. Validate direction
            // -------------------------------
            if (placement.Direction.GetLength() < 0.001)
                throw new Exception("Invalid direction vector");

            XYZ desiredDirection = placement.Direction.Normalize();

            // -------------------------------
            // 2. Get correct wall face
            // -------------------------------
            var (faceRef, faceOrigin, faceNormal) =
                GetWallFaceTowardDuct(wall, desiredDirection);

            if (faceOrigin == XYZ.Zero)
                throw new Exception("No valid wall face found");

            // -------------------------------
            // 3. Project point onto wall face
            // -------------------------------
            XYZ projectedPoint =
                ProjectPointOntoFace(placement.Location, faceOrigin, faceNormal);

            // 🔥 CRITICAL: push slightly INSIDE the wall so hosting works reliably
            double epsilon = 0.01; // ~3 mm
            projectedPoint = projectedPoint - faceNormal * epsilon;

            // -------------------------------
            // 4. Place instance (WALL-BASED)
            // -------------------------------
            var instance = _doc.Create.NewFamilyInstance(
                projectedPoint,
                symbol,
                wall,
                level,
                StructuralType.NonStructural);

            if (instance == null)
                throw new Exception("Instance creation failed");

            // -------------------------------
            // 5. Fix facing (toward duct)
            // -------------------------------
            XYZ facing = instance.FacingOrientation;

            if (facing.DotProduct(desiredDirection) < 0)
            {
                if (instance.CanFlipFacing)
                    instance.flipFacing();
            }

            // -------------------------------
            // 6. Rotate precisely toward duct
            // -------------------------------
            XYZ currentFacing = instance.FacingOrientation;
            XYZ targetFacing = new XYZ(desiredDirection.X, desiredDirection.Y, 0).Normalize();

            double angle = currentFacing.AngleTo(targetFacing);

            var cross = currentFacing.CrossProduct(targetFacing);
            double sign = cross.Z < 0 ? -1 : 1;

            double rotation = angle * sign;

            if (Math.Abs(rotation) > 0.0001)
            {
                var axis = Line.CreateBound(
                    projectedPoint,
                    projectedPoint + XYZ.BasisZ);

                ElementTransformUtils.RotateElement(_doc, instance.Id, axis, rotation);
            }

            // -------------------------------
            // 7. Set vertical offset (Z)
            // -------------------------------
            double offsetFromLevel = placement.Location.Z - level.Elevation;

            var offsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
            offsetParam?.Set(offsetFromLevel);

            return instance;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Vertical Support Error", ex.Message);
            return null;
        }
    }



    /// <summary>
    /// Get the wall face that faces toward the duct (in the given direction).
    /// Returns the face reference, origin point, and normal.
    /// 
    /// For wall-based families, we need the face on the duct side of the wall.
    /// This is the face whose normal points TOWARD the duct (same direction as directionTowardDuct).
    /// When a wall-based family is placed on this face, it extends outward from the face (toward the duct).
    /// </summary>
    private (Reference? faceRef, XYZ origin, XYZ normal) GetWallFaceTowardDuct(Wall wall, XYZ directionTowardDuct)
    {
        try
        {
            var options = new Options { ComputeReferences = true };
            var geometry = wall.get_Geometry(options);
            
            if (geometry == null)
                return (null, XYZ.Zero, XYZ.Zero);
            
            Reference? bestFaceRef = null;
            XYZ bestOrigin = XYZ.Zero;
            XYZ bestNormal = XYZ.Zero;
            double bestAlignment = double.MinValue;
            
            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            // Check if face is vertical (normal is horizontal)
                            XYZ normal = planarFace.FaceNormal;
                            if (Math.Abs(normal.Z) > 0.1)
                                continue; // Skip non-vertical faces
                            
                            // We want the face on the OPPOSITE side from the duct.
                            // When we place a wall-based family on this face, it will extend
                            // FROM the face TOWARD the duct (in the direction of the face normal).
                            // So we need the face whose normal points AWAY from the duct
                            // (negative alignment with directionTowardDuct).
                            // This way the family hosts to the wall face away from duct,
                            // and its arm extends toward the duct.
                            double alignment = -normal.DotProduct(directionTowardDuct);
                            
                            if (alignment > bestAlignment)
                            {
                                bestAlignment = alignment;
                                bestFaceRef = face.Reference;
                                bestOrigin = planarFace.Origin;
                                bestNormal = normal;
                            }
                        }
                    }
                }
            }
            
            return (bestFaceRef, bestOrigin, bestNormal);
        }
        catch
        {
            return (null, XYZ.Zero, XYZ.Zero);
        }
    }
    
    /// <summary>
    /// Project a point onto a plane defined by origin and normal.
    /// </summary>
    private XYZ ProjectPointOntoFace(XYZ point, XYZ planeOrigin, XYZ planeNormal)
    {
        // Vector from plane origin to the point
        XYZ toPoint = point - planeOrigin;
        
        // Distance from point to plane along normal
        double distance = toPoint.DotProduct(planeNormal);
        
        // Projected point = point - (distance * normal)
        return point - (distance * planeNormal);
    }
    
    /// <summary>
    /// Place floor-based support for vertical duct (VerticalFloorSupportDuct.rfa).
    /// This is a floor-based family that surrounds the duct at floor penetrations.
    /// </summary>
    private FamilyInstance? PlaceVerticalFloorSupport(FamilySymbol symbol, SupportPlacement placement, Level level)
    {
        // Place at XY location, with Z offset calculated from level elevation
        var locationXY = new XYZ(placement.Location.X, placement.Location.Y, level.Elevation);
        
        var instance = _doc.Create.NewFamilyInstance(
            locationXY,
            symbol,
            level,
            StructuralType.NonStructural);
        
        // Set elevation offset from level to get the correct Z position
        double offsetFromLevel = placement.Location.Z - level.Elevation;
        instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)?.Set(offsetFromLevel);
        
        // No rotation needed - family surrounds duct and has X/Y parameters
        
        return instance;
    }
    
    /// <summary>
    /// Place multiple supports in batch.
    /// </summary>
    public PlacementResults PlaceSupports(IEnumerable<SupportPlacement> placements, IProgress<(int current, int total, string message)>? progress = null)
    {
        var results = new PlacementResults();
        var placementList = placements.ToList();
        int total = placementList.Count;
        int current = 0;
        
        foreach (var placement in placementList)
        {
            current++;
            progress?.Report((current, total, $"Placing support {current} of {total}"));
            
            if (placement.Clash?.Resolution == ClashResolution.Skip)
            {
                results.SkippedDueToClash++;
                continue;
            }
            
            var instanceId = PlaceSupport(placement);
            
            if (instanceId != null)
            {
                placement.IsPlaced = true;
                placement.PlacedInstanceId = instanceId;
                results.TotalSupportsPlaced++;
                results.TotalLoadKg += placement.LoadKg;
                
                switch (placement.SupportType)
                {
                    case SupportType.Ceiling: results.CeilingSupports++; break;
                    case SupportType.Ground: results.GroundSupports++; break;
                    case SupportType.Wall: results.WallSupports++; break;
                    case SupportType.Vertical: 
                    case SupportType.VerticalFloor: 
                        results.VerticalSupports++; break;
                }
            }
            else
            {
                results.Errors.Add($"Failed to place support at {placement.Location}");
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Get default level from document.
    /// </summary>
    private Level? GetDefaultLevel()
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .FirstOrDefault();
    }
}
