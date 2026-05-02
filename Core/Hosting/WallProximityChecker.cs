using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Hosting;

/// <summary>
/// Checks proximity of duct to walls for wall-based support placement.
/// </summary>
public class WallProximityChecker
{
    private readonly Document _doc;
    private readonly List<WallInfo> _walls;
    private readonly double _proximityThreshold;
    
    public WallProximityChecker(Document doc, double proximityThresholdFeet)
    {
        _doc = doc;
        _proximityThreshold = proximityThresholdFeet;
        _walls = CollectWalls();
    }
    
    /// <summary>
    /// Collected wall information.
    /// </summary>
    private class WallInfo
    {
        public ElementId WallId { get; init; } = ElementId.InvalidElementId;
        public Line? CenterLine { get; init; }
        public XYZ Direction { get; init; } = XYZ.BasisX;
        public double Thickness { get; init; }
        public BoundingBoxXYZ? BoundingBox { get; init; }
        public Reference? ExteriorFace { get; init; }
    }
    
    /// <summary>
    /// Result of wall proximity check.
    /// </summary>
    public class WallProximityResult
    {
        public bool IsNearWall { get; init; }
        public ElementId WallId { get; init; } = ElementId.InvalidElementId;
        public Reference? WallFace { get; init; }
        /// <summary>
        /// Distance from nearest duct face (parallel to wall) to the wall face.
        /// </summary>
        public double Distance { get; init; }
        public XYZ WallDirection { get; init; } = XYZ.BasisX;
        /// <summary>
        /// Normal vector pointing from wall toward duct.
        /// </summary>
        public XYZ NormalToWall { get; init; } = XYZ.BasisY;
    }
    
    /// <summary>
    /// Collect all walls in the model.
    /// </summary>
    private List<WallInfo> CollectWalls()
    {
        var walls = new List<WallInfo>();
        var options = new Options { ComputeReferences = true };
        
        var wallElements = new FilteredElementCollector(_doc)
            .OfClass(typeof(Wall))
            .WhereElementIsNotElementType()
            .Cast<Wall>();
        
        foreach (var wall in wallElements)
        {
            try
            {
                var locationCurve = wall.Location as LocationCurve;
                var line = locationCurve?.Curve as Line;
                
                var bbox = wall.get_BoundingBox(null);
                double thickness = wall.Width;
                
                XYZ direction = line?.Direction ?? XYZ.BasisX;
                
                Reference? exteriorFace = GetExteriorFaceReference(wall, options);
                
                walls.Add(new WallInfo
                {
                    WallId = wall.Id,
                    CenterLine = line,
                    Direction = direction,
                    Thickness = thickness,
                    BoundingBox = bbox,
                    ExteriorFace = exteriorFace
                });
            }
            catch
            {
                // Skip problematic walls
            }
        }
        
        return walls;
    }
    
    /// <summary>
    /// Get the exterior face reference from a wall for face-based family placement.
    /// </summary>
    private Reference? GetExteriorFaceReference(Wall wall, Options options)
    {
        try
        {
            var geometry = wall.get_Geometry(options);
            if (geometry == null)
                return null;
            
            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    // Find the largest vertical face (exterior face)
                    Face? largestFace = null;
                    double largestArea = 0;
                    
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            // Check if face is vertical (normal is horizontal)
                            XYZ normal = planarFace.FaceNormal;
                            if (Math.Abs(normal.Z) < 0.1)
                            {
                                double area = face.Area;
                                if (area > largestArea)
                                {
                                    largestArea = area;
                                    largestFace = face;
                                }
                            }
                        }
                    }
                    
                    return largestFace?.Reference;
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
    /// Check if a point is near any wall.
    /// Returns distance from duct center to wall face.
    /// </summary>
    public WallProximityResult CheckProximity(XYZ point)
    {
        WallInfo? nearestWall = null;
        double nearestDistance = double.MaxValue;
        
        foreach (var wall in _walls)
        {
            double distance = GetDistanceToWall(point, wall);
            
            if (distance < nearestDistance && distance <= _proximityThreshold)
            {
                nearestDistance = distance;
                nearestWall = wall;
            }
        }
        
        if (nearestWall != null)
        {
            XYZ normal = GetNormalToWall(point, nearestWall);
            
            return new WallProximityResult
            {
                IsNearWall = true,
                WallId = nearestWall.WallId,
                Distance = nearestDistance,
                WallDirection = nearestWall.Direction,
                NormalToWall = normal
            };
        }
        
        return new WallProximityResult { IsNearWall = false };
    }
    
    /// <summary>
    /// Check wall proximity considering duct direction and width.
    /// Returns distance from the nearest duct face (parallel to wall) to wall face.
    /// 
    /// For a horizontal duct near a wall:
    /// - We need to find which duct face is nearest to the wall
    /// - The duct has two faces parallel to its direction (left and right sides)
    /// - We use the one closest to the wall
    /// </summary>
    /// <param name="ductCenterPoint">Center point of the duct at this position</param>
    /// <param name="ductDirection">Direction vector of the duct</param>
    /// <param name="ductWidth">Width of the duct (perpendicular to direction)</param>
    public WallProximityResult CheckProximityWithDuctDirection(XYZ ductCenterPoint, XYZ ductDirection, double ductWidth)
    {
        WallInfo? nearestWall = null;
        double nearestDistanceToWall = double.MaxValue;
        double distanceFromNearestFace = 0;
        
        foreach (var wall in _walls)
        {
            // Get distance from duct center to wall face
            double distanceFromCenter = GetDistanceToWall(ductCenterPoint, wall);
            
            if (distanceFromCenter > _proximityThreshold + ductWidth)
                continue; // Too far even with duct width
            
            // Get the normal from wall toward duct
            XYZ wallNormal = GetNormalToWall(ductCenterPoint, wall);
            
            // Calculate which duct face is nearest to the wall
            // The perpendicular direction to duct is where the wall might be
            XYZ ductPerp = new XYZ(-ductDirection.Y, ductDirection.X, 0).Normalize();
            
            // Check if wall normal aligns with duct perpendicular (wall is to the side of duct)
            double alignment = Math.Abs(wallNormal.DotProduct(ductPerp));
            
            if (alignment > 0.5) // Wall is roughly perpendicular to duct direction
            {
                // Distance from nearest duct face to wall
                // = distance from duct center to wall - half duct width
                double halfWidth = ductWidth / 2;
                double distFromNearestFace = distanceFromCenter - halfWidth;
                
                if (distFromNearestFace >= 0 && distFromNearestFace <= _proximityThreshold)
                {
                    if (distFromNearestFace < nearestDistanceToWall)
                    {
                        nearestDistanceToWall = distFromNearestFace;
                        nearestWall = wall;
                        distanceFromNearestFace = distFromNearestFace;
                    }
                }
            }
        }
        
        if (nearestWall != null)
        {
            XYZ normal = GetNormalToWall(ductCenterPoint, nearestWall);
            
            return new WallProximityResult
            {
                IsNearWall = true,
                WallId = nearestWall.WallId,
                WallFace = GetWallFaceReference(nearestWall.WallId, ductCenterPoint),
                Distance = distanceFromNearestFace, // Distance from nearest duct face to wall
                WallDirection = nearestWall.Direction,
                NormalToWall = normal
            };
        }
        
        return new WallProximityResult { IsNearWall = false };
    }
    
    /// <summary>
    /// Get distance from point to wall face.
    /// </summary>
    private double GetDistanceToWall(XYZ point, WallInfo wall)
    {
        if (wall.CenterLine == null)
        {
            // Use bounding box if no centerline
            if (wall.BoundingBox == null)
                return double.MaxValue;
            
            return GetDistanceToBoundingBox(point, wall.BoundingBox);
        }
        
        // Project point onto wall line (2D, ignoring Z)
        XYZ start = wall.CenterLine.GetEndPoint(0);
        XYZ end = wall.CenterLine.GetEndPoint(1);
        
        XYZ pointFlat = new XYZ(point.X, point.Y, start.Z);
        XYZ startFlat = new XYZ(start.X, start.Y, start.Z);
        XYZ endFlat = new XYZ(end.X, end.Y, start.Z);
        
        XYZ lineVec = endFlat - startFlat;
        XYZ pointVec = pointFlat - startFlat;
        
        double lineLengthSq = lineVec.GetLength();
        if (lineLengthSq < 0.001)
            return pointFlat.DistanceTo(startFlat);
        
        double t = pointVec.DotProduct(lineVec) / (lineLengthSq * lineLengthSq);
        t = Math.Max(0, Math.Min(1, t));
        
        XYZ closestPoint = startFlat + t * lineVec;
        double distanceToLine = pointFlat.DistanceTo(closestPoint);
        
        // Subtract half wall thickness to get distance to face
        return Math.Max(0, distanceToLine - wall.Thickness / 2);
    }
    
    /// <summary>
    /// Get distance to bounding box.
    /// </summary>
    private double GetDistanceToBoundingBox(XYZ point, BoundingBoxXYZ bbox)
    {
        double dx = Math.Max(Math.Max(bbox.Min.X - point.X, 0), point.X - bbox.Max.X);
        double dy = Math.Max(Math.Max(bbox.Min.Y - point.Y, 0), point.Y - bbox.Max.Y);
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// Get normal direction from wall toward point.
    /// </summary>
    private XYZ GetNormalToWall(XYZ point, WallInfo wall)
    {
        if (wall.CenterLine == null)
            return XYZ.BasisY;
        
        // Normal is perpendicular to wall direction
        XYZ wallDir = wall.Direction;
        XYZ normal = new XYZ(-wallDir.Y, wallDir.X, 0).Normalize();
        
        // Check which side of wall the point is on
        XYZ wallStart = wall.CenterLine.GetEndPoint(0);
        XYZ toPoint = point - wallStart;
        
        if (toPoint.DotProduct(normal) < 0)
            normal = normal.Negate();
        
        return normal;
    }
    
    /// <summary>
    /// Find nearest wall for vertical duct support.
    /// Uses the default proximity threshold.
    /// </summary>
    public WallProximityResult FindNearestWall(XYZ point)
    {
        return FindNearestWall(point, _proximityThreshold);
    }
    
    /// <summary>
    /// Find nearest wall for vertical duct support with a custom proximity threshold.
    /// </summary>
    /// <param name="point">Point to check proximity from</param>
    /// <param name="proximityThresholdFeet">Maximum distance in feet to consider a wall as "near"</param>
    public WallProximityResult FindNearestWall(XYZ point, double proximityThresholdFeet)
    {
        WallInfo? nearestWall = null;
        double nearestDistance = double.MaxValue;
        
        foreach (var wall in _walls)
        {
            double distance = GetDistanceToWall(point, wall);
            
            if (distance < nearestDistance && distance <= proximityThresholdFeet)
            {
                nearestDistance = distance;
                nearestWall = wall;
            }
        }
        
        if (nearestWall != null)
        {
            XYZ normal = GetNormalToWall(point, nearestWall);
            
            return new WallProximityResult
            {
                IsNearWall = true,
                WallId = nearestWall.WallId,
                WallFace = GetWallFaceReference(nearestWall.WallId, point),
                Distance = nearestDistance,
                WallDirection = nearestWall.Direction,
                NormalToWall = normal
            };
        }
        
        return new WallProximityResult { IsNearWall = false };
    }
    
    /// <summary>
    /// Get the wall face reference for face-based family placement at a specific point.
    /// Returns the face closest to the given point.
    /// </summary>
    /// <param name="wallId">The wall element ID</param>
    /// <param name="point">The point near the wall where placement will occur</param>
    /// <returns>Reference to the wall face, or null if not found</returns>
    public Reference? GetWallFaceReference(ElementId wallId, XYZ point)
    {
        var wall = _doc.GetElement(wallId) as Wall;
        if (wall == null)
            return null;
        
        try
        {
            var options = new Options { ComputeReferences = true };
            var geometry = wall.get_Geometry(options);
            if (geometry == null)
                return null;
            
            Reference? closestFaceRef = null;
            double closestDistance = double.MaxValue;
            
            foreach (var geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            // Check if face is vertical (normal is horizontal)
                            XYZ normal = planarFace.FaceNormal;
                            if (Math.Abs(normal.Z) < 0.1)
                            {
                                double distance = planarFace.Project(point)?.Distance ?? double.MaxValue;
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    closestFaceRef = face.Reference;
                                }
                            }
                        }
                    }
                }
            }
            
            return closestFaceRef;
        }
        catch
        {
            return null;
        }
    }
}
