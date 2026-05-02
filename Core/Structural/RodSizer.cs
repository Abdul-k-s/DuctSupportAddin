namespace DuctSupportAddin.Core.Structural;

/// <summary>
/// Determines appropriate threaded rod sizes for support loads.
/// </summary>
public static class RodSizer
{
    /// <summary>
    /// Rod specifications: (size name, safe working load in kg, ultimate load in kg)
    /// Based on standard threaded rod capacities with safety factor
    /// </summary>
    private static readonly List<(string size, double safeLoad, double ultimateLoad)> RodSpecs = new()
    {
        ("M6",    35,   140),
        ("M8",    90,   360),
        ("M10",   180,  720),
        ("M12",   320,  1280),
        ("M16",   580,  2320),
        ("M20",   920,  3680),
        ("M24",   1350, 5400)
    };
    
    /// <summary>
    /// Imperial rod specifications
    /// </summary>
    private static readonly List<(string size, double safeLoad, double ultimateLoad)> ImperialRodSpecs = new()
    {
        ("1/4\"",  30,   120),
        ("5/16\"", 55,   220),
        ("3/8\"",  90,   360),
        ("1/2\"",  180,  720),
        ("5/8\"",  320,  1280),
        ("3/4\"",  480,  1920),
        ("7/8\"",  680,  2720),
        ("1\"",    920,  3680)
    };
    
    /// <summary>
    /// Get recommended rod size for a given load.
    /// </summary>
    /// <param name="loadKg">Total load in kg</param>
    /// <param name="useMetric">Use metric rod sizes</param>
    /// <returns>Rod size name and capacity</returns>
    public static (string size, double capacity) GetRecommendedRod(double loadKg, bool useMetric = true)
    {
        var specs = useMetric ? RodSpecs : ImperialRodSpecs;
        
        foreach (var (size, safeLoad, _) in specs)
        {
            if (loadKg <= safeLoad)
            {
                return (size, safeLoad);
            }
        }
        
        // Return largest if load exceeds all
        var largest = specs.Last();
        return (largest.size, largest.safeLoad);
    }
    
    /// <summary>
    /// Get all available rod sizes for UI selection.
    /// </summary>
    public static IEnumerable<(string size, double safeLoad)> GetAvailableSizes(bool useMetric = true)
    {
        var specs = useMetric ? RodSpecs : ImperialRodSpecs;
        return specs.Select(s => (s.size, s.safeLoad));
    }
    
    /// <summary>
    /// Calculate safety factor for a given rod and load.
    /// </summary>
    public static double GetSafetyFactor(string rodSize, double loadKg, bool useMetric = true)
    {
        var specs = useMetric ? RodSpecs : ImperialRodSpecs;
        var rod = specs.FirstOrDefault(s => s.size == rodSize);
        
        if (rod.size == null)
            return 0;
        
        return rod.safeLoad / Math.Max(loadKg, 0.01);
    }
    
    /// <summary>
    /// Check if rod is adequate for load.
    /// </summary>
    public static bool IsAdequate(string rodSize, double loadKg, double minSafetyFactor = 1.5, bool useMetric = true)
    {
        return GetSafetyFactor(rodSize, loadKg, useMetric) >= minSafetyFactor;
    }
    
    /// <summary>
    /// Get detailed rod information for reporting.
    /// </summary>
    public static RodInfo GetRodInfo(double loadKg, bool useMetric = true)
    {
        var (size, capacity) = GetRecommendedRod(loadKg, useMetric);
        var specs = useMetric ? RodSpecs : ImperialRodSpecs;
        var rod = specs.First(s => s.size == size);
        
        return new RodInfo
        {
            Size = size,
            SafeLoadKg = rod.safeLoad,
            UltimateLoadKg = rod.ultimateLoad,
            ActualLoadKg = loadKg,
            SafetyFactor = rod.safeLoad / Math.Max(loadKg, 0.01),
            Utilization = loadKg / rod.safeLoad * 100
        };
    }
}

/// <summary>
/// Detailed rod information for reporting.
/// </summary>
public class RodInfo
{
    public string Size { get; init; } = string.Empty;
    public double SafeLoadKg { get; init; }
    public double UltimateLoadKg { get; init; }
    public double ActualLoadKg { get; init; }
    public double SafetyFactor { get; init; }
    public double Utilization { get; init; }
    
    public bool IsAdequate => SafetyFactor >= 1.5;
    
    public string GetStatus()
    {
        if (SafetyFactor >= 3.0) return "Excellent";
        if (SafetyFactor >= 2.0) return "Good";
        if (SafetyFactor >= 1.5) return "Adequate";
        if (SafetyFactor >= 1.0) return "Marginal";
        return "Inadequate";
    }
}
