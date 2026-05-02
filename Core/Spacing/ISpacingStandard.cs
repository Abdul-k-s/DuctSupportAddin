using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Spacing;

/// <summary>
/// Interface for spacing standard implementations.
/// </summary>
public interface ISpacingStandard
{
    /// <summary>Standard name for display</summary>
    string Name { get; }
    
    /// <summary>Standard code (e.g., "SMACNA")</summary>
    string Code { get; }
    
    /// <summary>Region/country of origin</summary>
    string Region { get; }
    
    /// <summary>
    /// Get horizontal spacing for given duct dimensions.
    /// </summary>
    /// <param name="maxDimensionMm">Maximum duct dimension in mm</param>
    /// <returns>Spacing in meters</returns>
    double GetHorizontalSpacing(double maxDimensionMm);
    
    /// <summary>
    /// Get vertical spacing for given duct dimensions.
    /// </summary>
    /// <param name="maxDimensionMm">Maximum duct dimension in mm</param>
    /// <returns>Spacing in meters</returns>
    double GetVerticalSpacing(double maxDimensionMm);
    
    /// <summary>
    /// Get all spacing rules for display in UI.
    /// </summary>
    IReadOnlyList<SpacingRule> GetRules();
}

/// <summary>
/// Base class for spacing standards with common functionality.
/// </summary>
public abstract class SpacingStandardBase : ISpacingStandard
{
    public abstract string Name { get; }
    public abstract string Code { get; }
    public abstract string Region { get; }
    
    protected abstract List<SpacingRule> Rules { get; }
    
    public double GetHorizontalSpacing(double maxDimensionMm)
    {
        foreach (var rule in Rules.OrderBy(r => r.MaxDimensionMm))
        {
            if (rule.AppliesTo(maxDimensionMm))
                return rule.HorizontalSpacingM;
        }
        return Rules.Last().HorizontalSpacingM;
    }
    
    public double GetVerticalSpacing(double maxDimensionMm)
    {
        foreach (var rule in Rules.OrderBy(r => r.MaxDimensionMm))
        {
            if (rule.AppliesTo(maxDimensionMm))
                return rule.VerticalSpacingM;
        }
        return Rules.Last().VerticalSpacingM;
    }
    
    public IReadOnlyList<SpacingRule> GetRules() => Rules.AsReadOnly();
    
    /// <summary>
    /// Convert spacing from meters to feet (Revit internal units).
    /// </summary>
    public static double MetersToFeet(double meters) => meters / 0.3048;
    
    /// <summary>
    /// Convert dimension from feet to mm.
    /// </summary>
    public static double FeetToMm(double feet) => feet * 304.8;
}
