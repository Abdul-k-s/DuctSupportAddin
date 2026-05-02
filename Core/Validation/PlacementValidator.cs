using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Validation;

/// <summary>
/// Validates support placements before committing to model.
/// </summary>
public class PlacementValidator
{
    private readonly ClashDetector _clashDetector;
    private readonly ExistingSupportChecker _existingChecker;
    private readonly bool _skipExisting;
    
    public PlacementValidator(ClashDetector clashDetector, ExistingSupportChecker existingChecker, bool skipExisting)
    {
        _clashDetector = clashDetector;
        _existingChecker = existingChecker;
        _skipExisting = skipExisting;
    }
    
    /// <summary>
    /// Validate all placements and return filtered list.
    /// </summary>
    public ValidationResult Validate(List<SupportPlacement> placements, IProgress<string>? progress = null)
    {
        var result = new ValidationResult
        {
            OriginalCount = placements.Count
        };
        
        // Step 1: Filter existing supports if configured
        var filtered = placements;
        if (_skipExisting)
        {
            progress?.Report("Checking existing supports...");
            filtered = _existingChecker.FilterExisting(placements);
            result.SkippedDueToExisting = placements.Count - filtered.Count;
        }
        
        // Step 2: Check for clashes
        progress?.Report("Detecting clashes...");
        _clashDetector.CheckClashes(filtered, progress);
        
        // Count clashes by severity
        var (critical, error, warning) = ClashDetector.GetClashStats(filtered);
        result.CriticalClashes = critical;
        result.ErrorClashes = error;
        result.WarningClashes = warning;
        
        // Step 3: Separate valid and invalid placements
        result.ValidPlacements = filtered
            .Where(p => p.Clash == null || p.Clash.Severity != ClashSeverity.Critical)
            .ToList();
        
        result.InvalidPlacements = filtered
            .Where(p => p.Clash?.Severity == ClashSeverity.Critical)
            .ToList();
        
        result.ValidCount = result.ValidPlacements.Count;
        
        return result;
    }
    
    /// <summary>
    /// Auto-resolve clashes based on severity.
    /// </summary>
    public void AutoResolveClashes(List<SupportPlacement> placements, ClashResolution defaultResolution = ClashResolution.Skip)
    {
        foreach (var placement in placements)
        {
            if (placement.Clash == null)
                continue;
            
            placement.Clash.Resolution = placement.Clash.Severity switch
            {
                ClashSeverity.Critical => ClashResolution.Skip,
                ClashSeverity.Error => defaultResolution,
                ClashSeverity.Warning => ClashResolution.Ignore,
                _ => defaultResolution
            };
        }
    }
}

/// <summary>
/// Results of placement validation.
/// </summary>
public class ValidationResult
{
    public int OriginalCount { get; set; }
    public int ValidCount { get; set; }
    public int SkippedDueToExisting { get; set; }
    public int CriticalClashes { get; set; }
    public int ErrorClashes { get; set; }
    public int WarningClashes { get; set; }
    public List<SupportPlacement> ValidPlacements { get; set; } = new();
    public List<SupportPlacement> InvalidPlacements { get; set; } = new();
    
    public int TotalClashes => CriticalClashes + ErrorClashes + WarningClashes;
    
    public bool HasCriticalIssues => CriticalClashes > 0;
    
    public string GetSummary()
    {
        return $"""
            Validation Summary
            ──────────────────
            Original placements: {OriginalCount}
            Valid placements: {ValidCount}
            
            Skipped (existing): {SkippedDueToExisting}
            
            Clashes detected: {TotalClashes}
            • Critical: {CriticalClashes}
            • Error: {ErrorClashes}
            • Warning: {WarningClashes}
            """;
    }
}
