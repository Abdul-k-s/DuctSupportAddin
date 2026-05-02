using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Export;

/// <summary>
/// Data model for export reports.
/// </summary>
public class ReportData
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; } = DateTime.Now;
    public string GeneratedBy { get; set; } = "AUS Duct Support Add-in";
    public string Version { get; set; } = "1.0.0";
    
    // Configuration used
    public string SpacingStandard { get; set; } = string.Empty;
    public double SpacingPercentage { get; set; }
    public bool IncludesInsulation { get; set; }
    
    // Summary statistics
    public int TotalSupports { get; set; }
    public int CeilingSupports { get; set; }
    public int GroundSupports { get; set; }
    public int WallSupports { get; set; }
    public int VerticalSupports { get; set; }
    public int DuctsProcessed { get; set; }
    public int SkippedDueToFittings { get; set; }
    public int SkippedDueToExisting { get; set; }
    public int ClashesDetected { get; set; }
    public double TotalLoadKg { get; set; }
    
    // Per-level breakdown
    public List<LevelSummary> LevelSummaries { get; set; } = new();
    
    // Per-system breakdown
    public List<SystemSummary> SystemSummaries { get; set; } = new();
    
    // Detailed support list
    public List<SupportDetail> Supports { get; set; } = new();
    
    // Warnings and errors
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Summary by level.
/// </summary>
public class LevelSummary
{
    public string LevelName { get; set; } = string.Empty;
    public int SupportCount { get; set; }
    public double TotalLoadKg { get; set; }
}

/// <summary>
/// Summary by system.
/// </summary>
public class SystemSummary
{
    public string SystemName { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public int DuctCount { get; set; }
    public int SupportCount { get; set; }
    public double TotalLoadKg { get; set; }
}

/// <summary>
/// Detailed support information for export.
/// </summary>
public class SupportDetail
{
    public string Mark { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public string GridLocation { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double DuctWidth { get; set; }
    public double DuctHeight { get; set; }
    public string DuctSystem { get; set; } = string.Empty;
    public double SupportWidth { get; set; }
    public double RodLength { get; set; }
    public double SupportHeight { get; set; }
    public double LoadKg { get; set; }
    public string RecommendedRod { get; set; } = string.Empty;
    public string HostElement { get; set; } = string.Empty;
    public string ClashStatus { get; set; } = string.Empty;
}

/// <summary>
/// Builder for creating ReportData from placements.
/// </summary>
public class ReportDataBuilder
{
    private readonly ReportData _data = new();
    
    public ReportDataBuilder WithProjectInfo(string name, string number)
    {
        _data.ProjectName = name;
        _data.ProjectNumber = number;
        return this;
    }
    
    public ReportDataBuilder WithConfiguration(Configuration config)
    {
        _data.SpacingStandard = config.SpacingStandard.ToString();
        _data.SpacingPercentage = 100.0 - config.SpacingAdjustmentPercent; // Convert adjustment to effective percentage
        _data.IncludesInsulation = config.ConsiderInsulation;
        return this;
    }
    
    public ReportDataBuilder WithPlacements(List<SupportPlacement> placements)
    {
        _data.TotalSupports = placements.Count(p => p.IsPlaced);
        _data.CeilingSupports = placements.Count(p => p.IsPlaced && p.SupportType == SupportType.Ceiling);
        _data.GroundSupports = placements.Count(p => p.IsPlaced && p.SupportType == SupportType.Ground);
        _data.WallSupports = placements.Count(p => p.IsPlaced && p.SupportType == SupportType.Wall);
        _data.VerticalSupports = placements.Count(p => p.IsPlaced && p.SupportType == SupportType.Vertical);
        _data.TotalLoadKg = placements.Sum(p => p.LoadKg);
        
        // Build level summaries
        var levelGroups = placements
            .Where(p => p.IsPlaced)
            .GroupBy(p => p.LevelName);
        
        foreach (var group in levelGroups)
        {
            _data.LevelSummaries.Add(new LevelSummary
            {
                LevelName = group.Key,
                SupportCount = group.Count(),
                TotalLoadKg = group.Sum(p => p.LoadKg)
            });
        }
        
        // Build detailed list
        foreach (var placement in placements.Where(p => p.IsPlaced))
        {
            _data.Supports.Add(new SupportDetail
            {
                Mark = placement.Mark,
                Type = placement.SupportType.ToString(),
                LevelName = placement.LevelName,
                X = placement.Location.X * 304.8, // Convert to mm
                Y = placement.Location.Y * 304.8,
                Z = placement.Location.Z * 304.8,
                DuctWidth = placement.DuctWidth * 304.8,
                SupportWidth = placement.SupportWidth * 304.8,
                RodLength = placement.RodLength * 304.8,
                SupportHeight = placement.SupportHeight * 304.8,
                LoadKg = placement.LoadKg,
                RecommendedRod = placement.RecommendedRod,
                ClashStatus = placement.Clash?.Severity.ToString() ?? "None"
            });
        }
        
        return this;
    }
    
    public ReportDataBuilder WithResults(PlacementResults results)
    {
        _data.DuctsProcessed = results.TotalDuctsProcessed;
        _data.SkippedDueToFittings = results.SkippedDueToFittings;
        _data.SkippedDueToExisting = results.SkippedDueToExisting;
        _data.ClashesDetected = results.ClashesDetected;
        _data.Warnings.AddRange(results.Warnings);
        _data.Errors.AddRange(results.Errors);
        return this;
    }
    
    public ReportData Build() => _data;
}
