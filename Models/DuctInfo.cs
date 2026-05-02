using Autodesk.Revit.DB;

namespace DuctSupportAddin.Models;

/// <summary>
/// Contains analyzed information about a duct element.
/// </summary>
public class DuctInfo
{
    /// <summary>Revit Element ID of the duct</summary>
    public required ElementId ElementId { get; init; }
    
    /// <summary>Duct orientation (Horizontal, Vertical, Sloped)</summary>
    public DuctOrientation Orientation { get; init; }
    
    /// <summary>Duct system type (Supply, Return, Exhaust, Other)</summary>
    public DuctSystemType SystemType { get; init; }
    
    /// <summary>Duct width in feet (internal units)</summary>
    public double Width { get; init; }
    
    /// <summary>Duct height in feet (internal units)</summary>
    public double Height { get; init; }
    
    /// <summary>Duct centerline start point</summary>
    public XYZ StartPoint { get; init; } = XYZ.Zero;
    
    /// <summary>Duct centerline end point</summary>
    public XYZ EndPoint { get; init; } = XYZ.Zero;
    
    /// <summary>Duct centerline direction vector (normalized) - flow direction</summary>
    public XYZ Direction { get; init; } = XYZ.BasisX;
    
    /// <summary>
    /// Direction vector along which Width is measured (in the profile plane).
    /// For horizontal ducts, this is typically horizontal (perpendicular to flow and Z).
    /// For vertical ducts, this defines the orientation of the cross-section in the XY plane.
    /// </summary>
    public XYZ WidthDirection { get; init; } = XYZ.BasisX;
    
    /// <summary>
    /// Direction vector along which Height is measured (in the profile plane).
    /// Perpendicular to both Direction and WidthDirection.
    /// </summary>
    public XYZ HeightDirection => Direction.CrossProduct(WidthDirection).Normalize();
    
    /// <summary>Duct centerline length in feet</summary>
    public double Length { get; init; }
    
    /// <summary>Bottom elevation of duct in feet</summary>
    public double BottomElevation { get; init; }
    
    /// <summary>Top elevation of duct in feet</summary>
    public double TopElevation { get; init; }
    
    /// <summary>Insulation thickness in feet (0 if none)</summary>
    public double InsulationThickness { get; init; }
    
    /// <summary>Insulation type if present</summary>
    public InsulationType InsulationType { get; init; } = InsulationType.None;
    
    /// <summary>Associated level element ID</summary>
    public ElementId LevelId { get; init; } = ElementId.InvalidElementId;
    
    /// <summary>Level name for display</summary>
    public string LevelName { get; init; } = string.Empty;
    
    /// <summary>System name for display</summary>
    public string SystemName { get; init; } = string.Empty;
    
    /// <summary>Duct perimeter in feet (for weight calculation)</summary>
    public double Perimeter => 2 * (Width + Height);
    
    /// <summary>Max dimension in feet (for spacing lookup)</summary>
    public double MaxDimension => Math.Max(Width, Height);
    
    /// <summary>
    /// Get a point along the duct centerline at a given parameter (0-1).
    /// </summary>
    public XYZ GetPointAt(double parameter)
    {
        return StartPoint + Direction * (Length * parameter);
    }
    
    /// <summary>
    /// Get the bounding box of the duct.
    /// </summary>
    public BoundingBoxXYZ GetBoundingBox()
    {
        double halfWidth = Width / 2;
        double halfHeight = Height / 2;
        
        XYZ min = new XYZ(
            Math.Min(StartPoint.X, EndPoint.X) - halfWidth,
            Math.Min(StartPoint.Y, EndPoint.Y) - halfWidth,
            BottomElevation
        );
        
        XYZ max = new XYZ(
            Math.Max(StartPoint.X, EndPoint.X) + halfWidth,
            Math.Max(StartPoint.Y, EndPoint.Y) + halfWidth,
            TopElevation
        );
        
        return new BoundingBoxXYZ { Min = min, Max = max };
    }
}
