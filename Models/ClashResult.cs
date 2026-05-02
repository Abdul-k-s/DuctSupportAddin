using Autodesk.Revit.DB;

namespace DuctSupportAddin.Models;

/// <summary>
/// Contains information about a detected clash.
/// </summary>
public class ClashResult
{
    /// <summary>Severity of the clash</summary>
    public ClashSeverity Severity { get; init; } = ClashSeverity.None;
    
    /// <summary>Element that clashes with the support</summary>
    public ElementId ClashingElementId { get; init; } = ElementId.InvalidElementId;
    
    /// <summary>Category of the clashing element</summary>
    public string ClashingCategory { get; init; } = string.Empty;
    
    /// <summary>Name of the clashing element</summary>
    public string ClashingElementName { get; init; } = string.Empty;
    
    /// <summary>Distance of overlap (negative = overlap, positive = clearance)</summary>
    public double OverlapDistance { get; init; }
    
    /// <summary>Suggested offset direction to resolve clash</summary>
    public XYZ? SuggestedOffset { get; init; }
    
    /// <summary>Human-readable description of the clash</summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>Resolution action taken or suggested</summary>
    public ClashResolution Resolution { get; set; } = ClashResolution.Skip;
    
    /// <summary>
    /// Create a no-clash result.
    /// </summary>
    public static ClashResult None => new() { Severity = ClashSeverity.None };
    
    /// <summary>
    /// Create a warning clash result.
    /// </summary>
    public static ClashResult Warning(ElementId elementId, string category, string name, string description) => new()
    {
        Severity = ClashSeverity.Warning,
        ClashingElementId = elementId,
        ClashingCategory = category,
        ClashingElementName = name,
        Description = description
    };
    
    /// <summary>
    /// Create an error clash result.
    /// </summary>
    public static ClashResult Error(ElementId elementId, string category, string name, string description) => new()
    {
        Severity = ClashSeverity.Error,
        ClashingElementId = elementId,
        ClashingCategory = category,
        ClashingElementName = name,
        Description = description
    };
    
    /// <summary>
    /// Create a critical clash result.
    /// </summary>
    public static ClashResult Critical(ElementId elementId, string category, string name, string description) => new()
    {
        Severity = ClashSeverity.Critical,
        ClashingElementId = elementId,
        ClashingCategory = category,
        ClashingElementName = name,
        Description = description
    };
}
