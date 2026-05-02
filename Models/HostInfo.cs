using Autodesk.Revit.DB;

namespace DuctSupportAddin.Models;

/// <summary>
/// Contains information about detected host elements (ceiling, beam, floor, wall).
/// </summary>
public class HostInfo
{
    /// <summary>Type of hosting determined</summary>
    public SupportType SupportType { get; init; }
    
    /// <summary>Host element ID</summary>
    public ElementId HostId { get; init; } = ElementId.InvalidElementId;
    
    /// <summary>Reference to host face (for wall-hosted)</summary>
    public Reference? HostFace { get; init; }
    
    /// <summary>Distance to host element in feet</summary>
    public double Distance { get; init; }
    
    /// <summary>Bottom elevation of host (ceiling/beam) in feet</summary>
    public double HostBottomElevation { get; init; }
    
    /// <summary>Top elevation of host (floor) in feet</summary>
    public double HostTopElevation { get; init; }
    
    /// <summary>Host element category name</summary>
    public string HostCategory { get; init; } = string.Empty;
    
    /// <summary>Host element name</summary>
    public string HostName { get; init; } = string.Empty;
    
    /// <summary>Whether a valid host was found</summary>
    public bool IsValid => HostId != ElementId.InvalidElementId || SupportType == SupportType.Ground;
}

/// <summary>
/// Contains insulation information for a duct.
/// </summary>
public class InsulationInfo
{
    /// <summary>Insulation thickness in feet</summary>
    public double Thickness { get; init; }
    
    /// <summary>Insulation type</summary>
    public InsulationType Type { get; init; } = InsulationType.None;
    
    /// <summary>Insulation density in kg/m³</summary>
    public double DensityKgM3 { get; init; }
    
    /// <summary>Whether insulation is present</summary>
    public bool HasInsulation => Thickness > 0;
    
    /// <summary>
    /// Get weight per meter of duct length in kg/m.
    /// </summary>
    public double GetWeightPerMeter(double ductPerimeterFeet)
    {
        if (!HasInsulation) return 0;
        
        // Convert perimeter to meters
        double perimeterM = ductPerimeterFeet * 0.3048;
        // Thickness in meters
        double thicknessM = Thickness * 0.3048;
        // Surface area per meter length
        double surfaceAreaM2 = perimeterM * 1.0; // per meter
        // Volume
        double volumeM3 = surfaceAreaM2 * thicknessM;
        // Weight
        return volumeM3 * DensityKgM3;
    }
    
    /// <summary>
    /// Get density for insulation type.
    /// </summary>
    public static double GetDensity(InsulationType type) => type switch
    {
        InsulationType.MineralWool => 40.0,
        InsulationType.Fiberglass => 24.0,
        InsulationType.Elastomeric => 60.0,
        InsulationType.PIR_PUR => 35.0,
        InsulationType.Custom => 40.0, // Default to mineral wool
        _ => 0.0
    };
}

/// <summary>
/// Contains structural load calculation results.
/// </summary>
public class LoadData
{
    /// <summary>Duct weight per meter in kg/m</summary>
    public double DuctWeightPerMeter { get; init; }
    
    /// <summary>Insulation weight per meter in kg/m</summary>
    public double InsulationWeightPerMeter { get; init; }
    
    /// <summary>Total weight per meter in kg/m</summary>
    public double TotalWeightPerMeter => DuctWeightPerMeter + InsulationWeightPerMeter;
    
    /// <summary>Tributary length in meters</summary>
    public double TributaryLengthM { get; init; }
    
    /// <summary>Total load at support point in kg</summary>
    public double TotalLoadKg => TotalWeightPerMeter * TributaryLengthM;
    
    /// <summary>Recommended rod diameter</summary>
    public string RecommendedRod { get; init; } = "M10";
    
    /// <summary>Rod capacity in kg</summary>
    public double RodCapacityKg { get; init; }
    
    /// <summary>Safety factor (capacity / load)</summary>
    public double SafetyFactor => RodCapacityKg / Math.Max(TotalLoadKg, 0.01);
    
    /// <summary>Steel gauge used for weight calculation</summary>
    public int SteelGauge { get; init; }
}

/// <summary>
/// Defines a spacing rule for a given dimension range.
/// </summary>
public class SpacingRule
{
    /// <summary>Maximum duct dimension in mm for this rule</summary>
    public double MaxDimensionMm { get; init; }
    
    /// <summary>Horizontal spacing in meters</summary>
    public double HorizontalSpacingM { get; init; }
    
    /// <summary>Vertical spacing in meters</summary>
    public double VerticalSpacingM { get; init; }
    
    /// <summary>
    /// Check if this rule applies to the given dimension.
    /// </summary>
    public bool AppliesTo(double dimensionMm) => dimensionMm <= MaxDimensionMm;
}
