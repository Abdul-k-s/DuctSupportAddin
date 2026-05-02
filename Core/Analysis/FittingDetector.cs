using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Analysis;

/// <summary>
/// Detects duct fittings to avoid placing supports near them.
/// </summary>
public class FittingDetector
{
    private readonly Document _doc;
    private readonly List<BoundingBoxXYZ> _fittingBounds;
    private readonly double _avoidanceBuffer;
    
    /// <summary>
    /// Buffer distance around fittings to avoid (in feet).
    /// </summary>
    private const double DefaultBuffer = 0.5; // ~150mm
    
    public FittingDetector(Document doc, double bufferFeet = DefaultBuffer)
    {
        _doc = doc;
        _avoidanceBuffer = bufferFeet;
        _fittingBounds = CollectFittingBounds();
    }
    
    /// <summary>
    /// Collect bounding boxes of all duct fittings.
    /// </summary>
    private List<BoundingBoxXYZ> CollectFittingBounds()
    {
        var bounds = new List<BoundingBoxXYZ>();
        
        var fittings = new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_DuctFitting)
            .WhereElementIsNotElementType()
            .ToElements();
        
        foreach (var fitting in fittings)
        {
            var bbox = fitting.get_BoundingBox(null);
            if (bbox != null)
            {
                // Expand bounds by buffer
                var expanded = new BoundingBoxXYZ
                {
                    Min = new XYZ(
                        bbox.Min.X - _avoidanceBuffer,
                        bbox.Min.Y - _avoidanceBuffer,
                        bbox.Min.Z - _avoidanceBuffer),
                    Max = new XYZ(
                        bbox.Max.X + _avoidanceBuffer,
                        bbox.Max.Y + _avoidanceBuffer,
                        bbox.Max.Z + _avoidanceBuffer)
                };
                bounds.Add(expanded);
            }
        }
        
        return bounds;
    }
    
    /// <summary>
    /// Check if a point is near any fitting.
    /// </summary>
    public bool IsNearFitting(XYZ point)
    {
        foreach (var bbox in _fittingBounds)
        {
            if (IsPointInBounds(point, bbox))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Filter support positions to exclude those near fittings.
    /// </summary>
    public List<double> FilterPositions(DuctInfo duct, List<double> positions)
    {
        var filtered = new List<double>();
        
        foreach (double param in positions)
        {
            XYZ point = duct.GetPointAt(param);
            if (!IsNearFitting(point))
            {
                filtered.Add(param);
            }
        }
        
        return filtered;
    }
    
    /// <summary>
    /// Get count of fittings detected.
    /// </summary>
    public int FittingCount => _fittingBounds.Count;
    
    /// <summary>
    /// Check if point is within bounding box.
    /// </summary>
    private static bool IsPointInBounds(XYZ point, BoundingBoxXYZ bbox)
    {
        return point.X >= bbox.Min.X && point.X <= bbox.Max.X &&
               point.Y >= bbox.Min.Y && point.Y <= bbox.Max.Y &&
               point.Z >= bbox.Min.Z && point.Z <= bbox.Max.Z;
    }
    
    /// <summary>
    /// Find nearest valid position if current position is near fitting.
    /// </summary>
    public double? FindNearestValidPosition(DuctInfo duct, double parameter, double step = 0.05)
    {
        XYZ point = duct.GetPointAt(parameter);
        if (!IsNearFitting(point))
            return parameter;
        
        // Search in both directions
        for (double offset = step; offset < 0.5; offset += step)
        {
            // Try forward
            double forward = parameter + offset;
            if (forward <= 1.0)
            {
                XYZ forwardPoint = duct.GetPointAt(forward);
                if (!IsNearFitting(forwardPoint))
                    return forward;
            }
            
            // Try backward
            double backward = parameter - offset;
            if (backward >= 0)
            {
                XYZ backwardPoint = duct.GetPointAt(backward);
                if (!IsNearFitting(backwardPoint))
                    return backward;
            }
        }
        
        return null; // No valid position found
    }
}
