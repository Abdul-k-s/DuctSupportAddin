using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Spacing;

/// <summary>
/// Factory and calculator for spacing standards.
/// Spacing is determined by duct size per standard, then reduced by adjustment percentage.
/// </summary>
public class SpacingCalculator
{
    private readonly ISpacingStandard _standard;
    private readonly double _adjustmentPercent;
    
    /// <summary>
    /// Create a spacing calculator.
    /// </summary>
    /// <param name="standardType">Spacing standard to use (SMACNA, DW/144, etc.)</param>
    /// <param name="adjustmentPercent">Percentage to reduce spacing (e.g., 20 = reduce by 20%, so 3m becomes 2.4m)</param>
    /// <param name="customRules">Custom spacing rules if standardType is Custom</param>
    public SpacingCalculator(SpacingStandardType standardType, double adjustmentPercent, IEnumerable<SpacingRule>? customRules = null)
    {
        _standard = CreateStandard(standardType, customRules);
        _adjustmentPercent = adjustmentPercent;
    }
    
    /// <summary>
    /// Get the spacing standard instance.
    /// </summary>
    public ISpacingStandard Standard => _standard;
    
    /// <summary>
    /// Calculate actual horizontal spacing for a duct.
    /// Spacing is based on duct size per standard, then reduced by adjustment percentage.
    /// </summary>
    /// <param name="ductInfo">Duct information</param>
    /// <returns>Spacing in feet (Revit internal units)</returns>
    public double GetHorizontalSpacing(DuctInfo ductInfo)
    {
        double maxDimensionMm = SpacingStandardBase.FeetToMm(ductInfo.MaxDimension);
        double spacingM = _standard.GetHorizontalSpacing(maxDimensionMm);
        
        // Apply adjustment: if user enters 20, spacing becomes 80% of original (3m * 0.8 = 2.4m)
        double adjustedSpacingM = spacingM * (1.0 - _adjustmentPercent / 100.0);
        
        // Ensure minimum spacing of 0.5m
        adjustedSpacingM = Math.Max(adjustedSpacingM, 0.5);
        
        return SpacingStandardBase.MetersToFeet(adjustedSpacingM);
    }
    
    /// <summary>
    /// Calculate actual vertical spacing for a duct.
    /// </summary>
    /// <param name="ductInfo">Duct information</param>
    /// <returns>Spacing in feet (Revit internal units)</returns>
    public double GetVerticalSpacing(DuctInfo ductInfo)
    {
        double maxDimensionMm = SpacingStandardBase.FeetToMm(ductInfo.MaxDimension);
        double spacingM = _standard.GetVerticalSpacing(maxDimensionMm);
        
        // Apply adjustment: if user enters 20, spacing becomes 80% of original
        double adjustedSpacingM = spacingM * (1.0 - _adjustmentPercent / 100.0);
        
        // Ensure minimum spacing of 0.5m
        adjustedSpacingM = Math.Max(adjustedSpacingM, 0.5);
        
        return SpacingStandardBase.MetersToFeet(adjustedSpacingM);
    }
    
    /// <summary>
    /// Get spacing based on duct orientation.
    /// </summary>
    public double GetSpacing(DuctInfo ductInfo)
    {
        return ductInfo.Orientation switch
        {
            DuctOrientation.Horizontal => GetHorizontalSpacing(ductInfo),
            DuctOrientation.Vertical => GetVerticalSpacing(ductInfo),
            _ => GetHorizontalSpacing(ductInfo) // Default to horizontal for sloped
        };
    }
    
    /// <summary>
    /// Calculate support positions along a duct.
    /// </summary>
    /// <param name="ductInfo">Duct information</param>
    /// <returns>List of parameter values (0-1) along duct centerline</returns>
    public List<double> CalculateSupportPositions(DuctInfo ductInfo)
    {
        var positions = new List<double>();
        double spacing = GetSpacing(ductInfo);
        double length = ductInfo.Length;
        
        if (length <= 0 || spacing <= 0)
            return positions;
        
        // Start with offset from beginning (typically spacing/2 or first support near start)
        double startOffset = Math.Min(spacing / 2, length / 4);
        
        // Calculate number of supports needed
        double remainingLength = length - (2 * startOffset);
        int numSpaces = Math.Max(1, (int)Math.Ceiling(remainingLength / spacing));
        double actualSpacing = remainingLength / numSpaces;
        
        // Generate positions
        double currentPosition = startOffset;
        while (currentPosition < length - 0.1) // Small tolerance
        {
            double parameter = currentPosition / length;
            if (parameter >= 0 && parameter <= 1)
            {
                positions.Add(parameter);
            }
            currentPosition += actualSpacing;
        }
        
        // Ensure at least one support in the middle for short ducts
        if (positions.Count == 0 && length > 0.5) // > ~150mm
        {
            positions.Add(0.5);
        }
        
        return positions;
    }
    
    /// <summary>
    /// Create spacing standard instance based on type.
    /// </summary>
    private static ISpacingStandard CreateStandard(SpacingStandardType type, IEnumerable<SpacingRule>? customRules)
    {
        return type switch
        {
            SpacingStandardType.SMACNA => new SmacnaStandard(),
            SpacingStandardType.DW144 => new Dw144Standard(),
            SpacingStandardType.VDI3803 => new Vdi3803Standard(),
            SpacingStandardType.AS4254 => new As4254Standard(),
            SpacingStandardType.Custom => new CustomStandard(customRules?.ToList() ?? new List<SpacingRule>()),
            _ => new SmacnaStandard()
        };
    }
    
    /// <summary>
    /// Get list of available spacing standards.
    /// </summary>
    public static IEnumerable<(SpacingStandardType type, string name, string region)> GetAvailableStandards()
    {
        yield return (SpacingStandardType.SMACNA, "SMACNA", "North America");
        yield return (SpacingStandardType.DW144, "DW/144", "United Kingdom");
        yield return (SpacingStandardType.VDI3803, "VDI 3803", "Germany");
        yield return (SpacingStandardType.AS4254, "AS 4254", "Australia");
        yield return (SpacingStandardType.Custom, "Custom", "User Defined");
    }
}
