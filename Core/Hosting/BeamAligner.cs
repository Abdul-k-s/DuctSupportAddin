using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Hosting;

/// <summary>
/// Aligns support positions with structural beams for coordination.
/// </summary>
public class BeamAligner
{
    private readonly Document _doc;
    private readonly List<BeamInfo> _beams;
    private readonly double _alignmentTolerance;
    
    /// <summary>
    /// Maximum distance to snap to a beam (in feet).
    /// </summary>
    private const double DefaultTolerance = 1.5; // ~450mm - increased for beam systems
    
    private class BeamInfo
    {
        public ElementId BeamId { get; init; } = ElementId.InvalidElementId;
        public Line? CenterLine { get; init; }
        public XYZ Direction { get; init; } = XYZ.BasisX;
        public double BottomZ { get; init; }
        public double Width { get; init; }
    }
    
    public class AlignmentResult
    {
        public bool IsAligned { get; init; }
        public XYZ AdjustedPosition { get; init; } = XYZ.Zero;
        public ElementId BeamId { get; init; } = ElementId.InvalidElementId;
        public double Offset { get; init; }
    }
    
    /// <summary>
    /// Number of beams detected (for diagnostics).
    /// </summary>
    public int BeamCount => _beams.Count;
    
    /// <summary>
    /// Diagnostic message.
    /// </summary>
    public static string LastDiagnostic { get; private set; } = "";
    
    public BeamAligner(Document doc, double toleranceFeet = DefaultTolerance)
    {
        _doc = doc;
        _alignmentTolerance = toleranceFeet;
        _beams = CollectBeams();
    }
    
    /// <summary>
    /// Collect all structural beams.
    /// </summary>
    private List<BeamInfo> CollectBeams()
    {
        var beams = new List<BeamInfo>();
        var diagnosticLines = new List<string>();
        
        // Collect individual beams from OST_StructuralFraming (excluding BeamSystem elements)
        var beamElements = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsNotElementType()
            .ToElements()
            .Where(e => !(e is BeamSystem));
        
        diagnosticLines.Add($"Found {beamElements.Count()} structural framing elements");
        
        foreach (var beam in beamElements)
        {
            var beamInfo = ExtractBeamInfo(beam);
            if (beamInfo != null)
                beams.Add(beamInfo);
        }
        
        // Also check for structural framing that might be nested (excluding BeamSystem)
        var allStructuralFraming = new FilteredElementCollector(_doc)
            .OfClass(typeof(FamilyInstance))
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .Cast<FamilyInstance>()
            .Where(fi => !(fi is BeamSystem))
            .ToList();
        
        foreach (var fi in allStructuralFraming)
        {
            if (!beams.Any(b => b.BeamId == fi.Id))
            {
                var beamInfo = ExtractBeamInfo(fi);
                if (beamInfo != null)
                    beams.Add(beamInfo);
            }
        }
        
        diagnosticLines.Add($"Total beams for alignment: {beams.Count}");
        LastDiagnostic = string.Join("\n", diagnosticLines);
        
        return beams;
    }
    
    /// <summary>
    /// Extract beam info from an element.
    /// </summary>
    private BeamInfo? ExtractBeamInfo(Element beam)
    {
        try
        {
            Line? line = null;
            
            // Try to get centerline from LocationCurve
            if (beam.Location is LocationCurve locationCurve)
            {
                line = locationCurve.Curve as Line;
            }
            
            // If no line, try to get from AnalyticalModel or bounding box
            if (line == null)
            {
                var bbox = beam.get_BoundingBox(null);
                if (bbox != null)
                {
                    // Create approximate centerline from bounding box
                    var minPt = bbox.Min;
                    var maxPt = bbox.Max;
                    
                    // Determine beam direction from bounding box
                    double dx = maxPt.X - minPt.X;
                    double dy = maxPt.Y - minPt.Y;
                    
                    XYZ start, end;
                    if (dx > dy)
                    {
                        // Beam runs in X direction
                        double midY = (minPt.Y + maxPt.Y) / 2;
                        double midZ = (minPt.Z + maxPt.Z) / 2;
                        start = new XYZ(minPt.X, midY, midZ);
                        end = new XYZ(maxPt.X, midY, midZ);
                    }
                    else
                    {
                        // Beam runs in Y direction
                        double midX = (minPt.X + maxPt.X) / 2;
                        double midZ = (minPt.Z + maxPt.Z) / 2;
                        start = new XYZ(midX, minPt.Y, midZ);
                        end = new XYZ(midX, maxPt.Y, midZ);
                    }
                    
                    if (start.DistanceTo(end) > 0.1)
                    {
                        line = Line.CreateBound(start, end);
                    }
                }
            }
            
            if (line == null) return null;
            
            var bboxFinal = beam.get_BoundingBox(null);
            double bottomZ = bboxFinal?.Min.Z ?? 0;
            double width = 0.5; // Default width
            
            // Try to get actual width from type
            var typeId = beam.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var beamType = _doc.GetElement(typeId);
                var widthParam = beamType?.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
                if (widthParam != null && widthParam.HasValue)
                    width = widthParam.AsDouble();
            }
            
            return new BeamInfo
            {
                BeamId = beam.Id,
                CenterLine = line,
                Direction = line.Direction,
                BottomZ = bottomZ,
                Width = width
            };
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Try to align a support position to a nearby beam.
    /// </summary>
    public AlignmentResult TryAlign(XYZ position, XYZ ductDirection)
    {
        BeamInfo? bestBeam = null;
        XYZ bestPosition = position;
        double bestOffset = double.MaxValue;
        
        foreach (var beam in _beams)
        {
            if (beam.CenterLine == null) continue;
            
            // Check if beam is roughly parallel or perpendicular to duct
            double dotProduct = Math.Abs(beam.Direction.DotProduct(ductDirection));
            bool isParallel = dotProduct > 0.9;
            bool isPerpendicular = dotProduct < 0.1;
            
            // Also allow beams that are at 45 degrees or similar
            bool isAtAngle = dotProduct >= 0.1 && dotProduct <= 0.9;
            
            // Find closest point on beam to support position
            var result = GetClosestPointOnBeam(position, beam);
            
            // Skip if too far
            if (result.distance > _alignmentTolerance)
                continue;
            
            if (result.distance < bestOffset)
            {
                bestOffset = result.distance;
                bestBeam = beam;
                
                if (isPerpendicular)
                {
                    // For perpendicular beams, snap to beam centerline along duct direction
                    // This is the most common case - support aligns with crossing beam
                    bestPosition = new XYZ(result.point.X, result.point.Y, position.Z);
                }
                else if (isParallel)
                {
                    // For parallel beams, keep position but could offset perpendicular
                    XYZ perpendicular = ductDirection.CrossProduct(XYZ.BasisZ).Normalize();
                    XYZ toBeam = (result.point - position);
                    if (toBeam.GetLength() > 0.001)
                    {
                        toBeam = toBeam.Normalize();
                        double sign = toBeam.DotProduct(perpendicular) > 0 ? 1 : -1;
                        bestPosition = position + perpendicular * result.distance * sign;
                    }
                }
                else
                {
                    // For angled beams, project onto beam centerline
                    bestPosition = new XYZ(result.point.X, result.point.Y, position.Z);
                }
            }
        }
        
        if (bestBeam != null)
        {
            return new AlignmentResult
            {
                IsAligned = true,
                AdjustedPosition = bestPosition,
                BeamId = bestBeam.BeamId,
                Offset = bestOffset
            };
        }
        
        return new AlignmentResult
        {
            IsAligned = false,
            AdjustedPosition = position
        };
    }
    
    /// <summary>
    /// Get closest point on beam centerline to given point.
    /// </summary>
    private (XYZ point, double distance) GetClosestPointOnBeam(XYZ point, BeamInfo beam)
    {
        if (beam.CenterLine == null)
            return (point, double.MaxValue);
        
        XYZ start = beam.CenterLine.GetEndPoint(0);
        XYZ end = beam.CenterLine.GetEndPoint(1);
        
        // Project to 2D (XY plane)
        XYZ pointXY = new XYZ(point.X, point.Y, 0);
        XYZ startXY = new XYZ(start.X, start.Y, 0);
        XYZ endXY = new XYZ(end.X, end.Y, 0);
        
        XYZ lineVec = endXY - startXY;
        double lineLength = lineVec.GetLength();
        
        if (lineLength < 0.001)
            return (start, pointXY.DistanceTo(startXY));
        
        XYZ lineDir = lineVec.Normalize();
        XYZ pointVec = pointXY - startXY;
        
        double t = pointVec.DotProduct(lineDir);
        t = Math.Max(0, Math.Min(lineLength, t));
        
        XYZ closestXY = startXY + lineDir * t;
        double distance = pointXY.DistanceTo(closestXY);
        
        // Return 3D point at beam elevation
        XYZ closestPoint = new XYZ(closestXY.X, closestXY.Y, beam.BottomZ);
        
        return (closestPoint, distance);
    }
    
    /// <summary>
    /// Get positions along a duct where supports should be placed based on beams above.
    /// When "Align with Structure" is enabled, looks for beams running PARALLEL to the duct
    /// and places supports at intervals along those beams.
    /// </summary>
    /// <param name="ductStartPoint">Start point of the duct</param>
    /// <param name="ductEndPoint">End point of the duct</param>
    /// <param name="ductDirection">Direction vector of the duct</param>
    /// <param name="ductBottomZ">Bottom Z elevation of the duct</param>
    /// <param name="maxDistanceAbove">Maximum distance above duct to look for beams (feet)</param>
    /// <returns>List of positions (as parameter 0-1 along duct) where supports should be placed</returns>
    public List<double> GetBeamCrossingPositions(XYZ ductStartPoint, XYZ ductEndPoint, XYZ ductDirection, double ductBottomZ, double maxDistanceAbove = 20.0)
    {
        var result = new List<double>();
        
        double ductLength = ductStartPoint.DistanceTo(ductEndPoint);
        if (ductLength < 0.1)
            return result;
        
        // Find beams that run PARALLEL to the duct and are above it
        foreach (var beam in _beams)
        {
            if (beam.CenterLine == null) continue;
            
            // Check if beam is above the duct
            if (beam.BottomZ < ductBottomZ || beam.BottomZ > ductBottomZ + maxDistanceAbove)
                continue;
            
            // Check if beam runs PARALLEL to the duct (dot product close to 1)
            double dotProduct = Math.Abs(beam.Direction.DotProduct(ductDirection));
            bool isParallel = dotProduct > 0.7; // More than ~45 degrees alignment
            
            if (!isParallel)
                continue;
            
            // Check if beam is horizontally close to the duct (within ~2 feet)
            XYZ beamStart = beam.CenterLine.GetEndPoint(0);
            XYZ beamEnd = beam.CenterLine.GetEndPoint(1);
            
            // Project duct midpoint onto beam to check proximity
            XYZ ductMidXY = new XYZ(
                (ductStartPoint.X + ductEndPoint.X) / 2,
                (ductStartPoint.Y + ductEndPoint.Y) / 2,
                0);
            
            var (closestPt, distance) = GetClosestPointOnBeam(
                new XYZ(ductMidXY.X, ductMidXY.Y, beam.BottomZ), beam);
            
            // Beam must be within horizontal tolerance of duct
            if (distance > 3.0) // 3 feet horizontal tolerance
                continue;
            
            // Found a parallel beam above - determine support positions
            // Place supports at beam ends and at regular intervals
            XYZ beamStartXY = new XYZ(beamStart.X, beamStart.Y, 0);
            XYZ beamEndXY = new XYZ(beamEnd.X, beamEnd.Y, 0);
            XYZ ductStartXY = new XYZ(ductStartPoint.X, ductStartPoint.Y, 0);
            
            // Project beam endpoints onto duct line
            double beamStartParam = ProjectPointOntoLine(beamStartXY, ductStartXY, ductDirection, ductLength);
            double beamEndParam = ProjectPointOntoLine(beamEndXY, ductStartXY, ductDirection, ductLength);
            
            // Ensure start < end
            if (beamStartParam > beamEndParam)
                (beamStartParam, beamEndParam) = (beamEndParam, beamStartParam);
            
            // Clamp to duct bounds
            beamStartParam = Math.Max(0.05, Math.Min(0.95, beamStartParam));
            beamEndParam = Math.Max(0.05, Math.Min(0.95, beamEndParam));
            
            // Add support at start and end of beam overlap
            if (beamStartParam >= 0 && beamStartParam <= 1)
                result.Add(beamStartParam);
            if (beamEndParam >= 0 && beamEndParam <= 1 && Math.Abs(beamEndParam - beamStartParam) > 0.1)
                result.Add(beamEndParam);
            
            // If beam span is long, add intermediate supports
            double beamSpan = beamEndParam - beamStartParam;
            if (beamSpan > 0.3) // More than 30% of duct length
            {
                double midParam = (beamStartParam + beamEndParam) / 2;
                result.Add(midParam);
            }
        }
        
        // Sort and remove duplicates
        result = result.Distinct().OrderBy(p => p).ToList();
        
        // Remove positions that are too close together
        var filtered = new List<double>();
        double lastParam = -1;
        const double minGap = 0.1;
        
        foreach (var param in result)
        {
            if (lastParam < 0 || param - lastParam >= minGap)
            {
                filtered.Add(param);
                lastParam = param;
            }
        }
        
        return filtered;
    }
    
    /// <summary>
    /// Project a point onto a line and return the parameter (0-1).
    /// </summary>
    private double ProjectPointOntoLine(XYZ point, XYZ lineStart, XYZ lineDirection, double lineLength)
    {
        XYZ toPoint = point - lineStart;
        double projection = toPoint.DotProduct(lineDirection);
        return projection / lineLength;
    }
    
    /// <summary>
    /// Find intersection point of two lines in XY plane.
    /// </summary>
    private XYZ? GetLineIntersectionXY(XYZ line1Start, XYZ line1End, XYZ line2Start, XYZ line2End)
    {
        double x1 = line1Start.X, y1 = line1Start.Y;
        double x2 = line1End.X, y2 = line1End.Y;
        double x3 = line2Start.X, y3 = line2Start.Y;
        double x4 = line2End.X, y4 = line2End.Y;
        
        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        
        if (Math.Abs(denom) < 0.0001)
            return null; // Lines are parallel
        
        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
        
        // Check if intersection is within both line segments
        if (t < -0.1 || t > 1.1 || u < -0.1 || u > 1.1)
            return null;
        
        double x = x1 + t * (x2 - x1);
        double y = y1 + t * (y2 - y1);
        
        return new XYZ(x, y, 0);
    }
}
