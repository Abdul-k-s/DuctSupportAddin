using Autodesk.Revit.DB;

namespace DuctSupportAddin.Models;

/// <summary>
/// Contains information about where a support should be placed.
/// </summary>
public class SupportPlacement
{
    /// <summary>Unique identifier for this placement</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>Source duct element ID</summary>
    public required ElementId DuctId { get; init; }
    
    /// <summary>Type of support to place</summary>
    public SupportType SupportType { get; init; }
    
    /// <summary>Placement location point</summary>
    public XYZ Location { get; init; } = XYZ.Zero;
    
    /// <summary>Support rotation angle in radians</summary>
    public double Rotation { get; init; }
    
    /// <summary>Support direction vector (for wall-based)</summary>
    public XYZ Direction { get; init; } = XYZ.BasisX;
    
    /// <summary>Level to place support on</summary>
    public required ElementId LevelId { get; init; }
    
    /// <summary>Host element ID (ceiling, beam, floor, wall)</summary>
    public ElementId HostId { get; init; } = ElementId.InvalidElementId;
    
    /// <summary>Reference face for wall-hosted supports</summary>
    public Reference? HostFace { get; set; }
    
    // =====================================================
    // Family parameters for RecDuctSupport (Ceiling/Beam)
    // =====================================================
    
    /// <summary>
    /// Duct_Height: Distance from bottom of duct to ceiling/beam (raycast result, in feet)
    /// </summary>
    public double DuctHeight { get; set; }
    
    /// <summary>
    /// Duct_Width: Width of the duct (in feet)
    /// </summary>
    public double DuctWidth { get; set; }
    
    // =====================================================
    // Family parameters for GroundDuctSupport
    // =====================================================
    
    /// <summary>
    /// Support_Height: Bottom of duct elevation (in feet)
    /// </summary>
    public double SupportHeight { get; set; }
    
    // =====================================================
    // Family parameters for WallSupportDuct (horizontal duct near wall)
    // Generic model, base at wall face
    // =====================================================
    
    /// <summary>
    /// Support_Length: Duct width + 30mm + offset from duct to wall (in feet)
    /// </summary>
    public double WallSupportLength { get; set; }
    
    // =====================================================
    // Family parameters for VerticalWallSupportDuct
    // U-shape support, origin at duct center
    // =====================================================
    
    /// <summary>
    /// Support_Width: Duct face parallel to wall (in feet)
    /// </summary>
    public double VerticalSupportWidth { get; set; }
    
    /// <summary>
    /// Support_Length: Duct face perpendicular to wall + offset to wall (in feet)
    /// </summary>
    public double VerticalSupportLength { get; set; }
    
    // =====================================================
    // Family parameters for VerticalFloorSupportDuct
    // Floor-based rectangular support surrounding duct
    // =====================================================
    
    /// <summary>
    /// X: Duct dimension along the X axis (in feet)
    /// </summary>
    public double FloorSupportX { get; set; }
    
    /// <summary>
    /// Y: Duct dimension along the Y axis (in feet)
    /// </summary>
    public double FloorSupportY { get; set; }
    
    // Legacy parameters (for backward compatibility)
    public double RodLength { get; init; }
    public double SupportWidth { get; init; }
    public double SupportLength { get; init; }
    public double Offset { get; set; }
    public double BottomOfDuct { get; set; }
    
    // Calculated load data
    
    /// <summary>Calculated load at this support point (in kg)</summary>
    public double LoadKg { get; set; }
    
    /// <summary>Recommended rod diameter (e.g., "M10")</summary>
    public string RecommendedRod { get; set; } = string.Empty;
    
    /// <summary>Tributary length this support covers (in feet)</summary>
    public double TributaryLength { get; set; }
    
    // Validation results
    
    /// <summary>Any clash detected at this location</summary>
    public ClashResult? Clash { get; set; }
    
    /// <summary>Whether this placement is valid</summary>
    public bool IsValid => Clash?.Severity != ClashSeverity.Critical;
    
    /// <summary>Whether this placement has been committed to model</summary>
    public bool IsPlaced { get; set; }
    
    /// <summary>Placed family instance element ID</summary>
    public ElementId PlacedInstanceId { get; set; } = ElementId.InvalidElementId;
    
    // Display properties
    
    /// <summary>Mark/tag for this support</summary>
    public string Mark { get; set; } = string.Empty;
    
    /// <summary>Level name for display</summary>
    public string LevelName { get; set; } = string.Empty;
}
