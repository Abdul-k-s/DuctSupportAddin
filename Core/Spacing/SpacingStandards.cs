using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Spacing;

/// <summary>
/// SMACNA (Sheet Metal and Air Conditioning Contractors' National Association) spacing standard.
/// Used in North America.
/// </summary>
public class SmacnaStandard : SpacingStandardBase
{
    public override string Name => "SMACNA";
    public override string Code => "SMACNA";
    public override string Region => "North America";
    
    protected override List<SpacingRule> Rules { get; } = new()
    {
        // ≤ 750mm: 3.0m horizontal, 4.5m vertical
        new SpacingRule 
        { 
            MaxDimensionMm = 750, 
            HorizontalSpacingM = 3.0, 
            VerticalSpacingM = 4.5 
        },
        // 751-1500mm: 3.0m horizontal, 3.0m vertical
        new SpacingRule 
        { 
            MaxDimensionMm = 1500, 
            HorizontalSpacingM = 3.0, 
            VerticalSpacingM = 3.0 
        },
        // > 1500mm: 2.4m horizontal, 2.4m vertical
        new SpacingRule 
        { 
            MaxDimensionMm = double.MaxValue, 
            HorizontalSpacingM = 2.4, 
            VerticalSpacingM = 2.4 
        }
    };
}

/// <summary>
/// DW/144 spacing standard (HVCA Specification).
/// Used in United Kingdom.
/// </summary>
public class Dw144Standard : SpacingStandardBase
{
    public override string Name => "DW/144";
    public override string Code => "DW144";
    public override string Region => "United Kingdom";
    
    protected override List<SpacingRule> Rules { get; } = new()
    {
        // ≤ 450mm: 3.0m
        new SpacingRule 
        { 
            MaxDimensionMm = 450, 
            HorizontalSpacingM = 3.0, 
            VerticalSpacingM = 3.0 
        },
        // 451-750mm: 2.5m
        new SpacingRule 
        { 
            MaxDimensionMm = 750, 
            HorizontalSpacingM = 2.5, 
            VerticalSpacingM = 2.5 
        },
        // 751-1000mm: 2.0m
        new SpacingRule 
        { 
            MaxDimensionMm = 1000, 
            HorizontalSpacingM = 2.0, 
            VerticalSpacingM = 2.0 
        },
        // > 1000mm: 1.5m
        new SpacingRule 
        { 
            MaxDimensionMm = double.MaxValue, 
            HorizontalSpacingM = 1.5, 
            VerticalSpacingM = 1.5 
        }
    };
}

/// <summary>
/// VDI 3803 spacing standard.
/// Used in Germany.
/// </summary>
public class Vdi3803Standard : SpacingStandardBase
{
    public override string Name => "VDI 3803";
    public override string Code => "VDI3803";
    public override string Region => "Germany";
    
    protected override List<SpacingRule> Rules { get; } = new()
    {
        // ≤ 500mm: 3.0m
        new SpacingRule 
        { 
            MaxDimensionMm = 500, 
            HorizontalSpacingM = 3.0, 
            VerticalSpacingM = 4.0 
        },
        // 501-1000mm: 2.5m
        new SpacingRule 
        { 
            MaxDimensionMm = 1000, 
            HorizontalSpacingM = 2.5, 
            VerticalSpacingM = 3.0 
        },
        // > 1000mm: 2.0m
        new SpacingRule 
        { 
            MaxDimensionMm = double.MaxValue, 
            HorizontalSpacingM = 2.0, 
            VerticalSpacingM = 2.5 
        }
    };
}

/// <summary>
/// AS 4254 spacing standard.
/// Used in Australia.
/// </summary>
public class As4254Standard : SpacingStandardBase
{
    public override string Name => "AS 4254";
    public override string Code => "AS4254";
    public override string Region => "Australia";
    
    protected override List<SpacingRule> Rules { get; } = new()
    {
        // ≤ 750mm: 2.4m
        new SpacingRule 
        { 
            MaxDimensionMm = 750, 
            HorizontalSpacingM = 2.4, 
            VerticalSpacingM = 3.6 
        },
        // 751-1500mm: 1.8m
        new SpacingRule 
        { 
            MaxDimensionMm = 1500, 
            HorizontalSpacingM = 1.8, 
            VerticalSpacingM = 2.4 
        },
        // > 1500mm: 1.2m
        new SpacingRule 
        { 
            MaxDimensionMm = double.MaxValue, 
            HorizontalSpacingM = 1.2, 
            VerticalSpacingM = 1.8 
        }
    };
}

/// <summary>
/// Custom user-defined spacing standard.
/// </summary>
public class CustomStandard : SpacingStandardBase
{
    public override string Name => "Custom";
    public override string Code => "CUSTOM";
    public override string Region => "User Defined";
    
    private readonly List<SpacingRule> _rules;
    
    protected override List<SpacingRule> Rules => _rules;
    
    public CustomStandard(IEnumerable<SpacingRule>? rules = null)
    {
        _rules = rules?.ToList() ?? new List<SpacingRule>
        {
            // Default custom rules (same as SMACNA)
            new SpacingRule 
            { 
                MaxDimensionMm = 750, 
                HorizontalSpacingM = 3.0, 
                VerticalSpacingM = 4.5 
            },
            new SpacingRule 
            { 
                MaxDimensionMm = 1500, 
                HorizontalSpacingM = 3.0, 
                VerticalSpacingM = 3.0 
            },
            new SpacingRule 
            { 
                MaxDimensionMm = double.MaxValue, 
                HorizontalSpacingM = 2.4, 
                VerticalSpacingM = 2.4 
            }
        };
    }
    
    /// <summary>
    /// Update custom rules.
    /// </summary>
    public void SetRules(IEnumerable<SpacingRule> rules)
    {
        _rules.Clear();
        _rules.AddRange(rules);
    }
}
