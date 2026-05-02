using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Structural;

/// <summary>
/// Calculates structural loads for duct supports.
/// </summary>
public class LoadCalculator
{
    private readonly Configuration _config;
    
    /// <summary>
    /// Steel density in kg/m³
    /// </summary>
    private const double SteelDensity = 7850;
    
    /// <summary>
    /// Steel gauge thicknesses in mm
    /// </summary>
    private static readonly Dictionary<int, double> GaugeThickness = new()
    {
        { 26, 0.55 },
        { 24, 0.70 },
        { 22, 0.85 },
        { 20, 1.00 },
        { 18, 1.27 },
        { 16, 1.52 }
    };
    
    /// <summary>
    /// Gauge selection based on perimeter (mm)
    /// </summary>
    private static readonly List<(double maxPerimeter, int gauge)> GaugeRules = new()
    {
        (750, 26),
        (1500, 24),
        (2250, 22),
        (3000, 20),
        (double.MaxValue, 18)
    };
    
    public LoadCalculator(Configuration config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Calculate load for a single support placement.
    /// </summary>
    public LoadData CalculateLoad(DuctInfo duct, SupportPlacement placement, InsulationInfo? insulation = null)
    {
        // Convert duct perimeter to mm
        double perimeterMm = duct.Perimeter * 304.8;
        
        // Determine steel gauge
        int gauge = GetGauge(perimeterMm);
        double thicknessMm = GaugeThickness.GetValueOrDefault(gauge, 0.70);
        double thicknessM = thicknessMm / 1000;
        
        // Calculate duct weight per meter (kg/m)
        double perimeterM = perimeterMm / 1000;
        double crossSectionArea = perimeterM * thicknessM; // m²
        double ductWeightPerM = crossSectionArea * SteelDensity; // kg/m
        
        // Calculate insulation weight per meter
        double insulationWeightPerM = 0;
        if (insulation != null && insulation.HasInsulation && _config.ConsiderInsulation)
        {
            insulationWeightPerM = insulation.GetWeightPerMeter(duct.Perimeter);
        }
        
        // Tributary length in meters
        double tributaryLengthM = placement.TributaryLength * 0.3048;
        
        // Calculate total load
        double totalWeightPerM = ductWeightPerM + insulationWeightPerM;
        double totalLoadKg = totalWeightPerM * tributaryLengthM;
        
        // Get rod sizing
        var (rodSize, capacity) = RodSizer.GetRecommendedRod(totalLoadKg);
        
        return new LoadData
        {
            DuctWeightPerMeter = ductWeightPerM,
            InsulationWeightPerMeter = insulationWeightPerM,
            TributaryLengthM = tributaryLengthM,
            RecommendedRod = rodSize,
            RodCapacityKg = capacity,
            SteelGauge = gauge
        };
    }
    
    /// <summary>
    /// Calculate loads for all placements.
    /// </summary>
    public void CalculateLoads(DuctInfo duct, List<SupportPlacement> placements, InsulationInfo? insulation = null)
    {
        foreach (var placement in placements)
        {
            var loadData = CalculateLoad(duct, placement, insulation);
            placement.LoadKg = loadData.TotalLoadKg;
            placement.RecommendedRod = loadData.RecommendedRod;
        }
    }
    
    /// <summary>
    /// Get steel gauge for duct perimeter.
    /// </summary>
    private int GetGauge(double perimeterMm)
    {
        foreach (var (maxPerimeter, gauge) in GaugeRules)
        {
            if (perimeterMm <= maxPerimeter)
                return gauge;
        }
        return 18;
    }
    
    /// <summary>
    /// Get duct weight per meter for display.
    /// </summary>
    public static double GetDuctWeightPerMeter(double widthFeet, double heightFeet)
    {
        double perimeterMm = (widthFeet + heightFeet) * 2 * 304.8;
        
        // Determine gauge
        int gauge = 24; // Default
        foreach (var (maxPerimeter, g) in GaugeRules)
        {
            if (perimeterMm <= maxPerimeter)
            {
                gauge = g;
                break;
            }
        }
        
        double thicknessMm = GaugeThickness.GetValueOrDefault(gauge, 0.70);
        double thicknessM = thicknessMm / 1000;
        double perimeterM = perimeterMm / 1000;
        
        return perimeterM * thicknessM * SteelDensity;
    }
}
