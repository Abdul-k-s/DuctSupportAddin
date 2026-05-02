using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuctSupportAddin.Models;

/// <summary>
/// User configuration for duct support placement.
/// Persisted to JSON file in AppData.
/// </summary>
public class Configuration
{
    // Scope settings
    public CollectionScope Scope { get; set; } = CollectionScope.EntireModel;
    public DuctSystemType SystemTypes { get; set; } = DuctSystemType.All;
    public bool SkipExistingSupports { get; set; } = true;
    public bool ConsiderInsulation { get; set; } = true;
    
    // Spacing settings
    public SpacingStandardType SpacingStandard { get; set; } = SpacingStandardType.SMACNA;
    /// <summary>
    /// Adjustment percentage - reduces spacing by this percent.
    /// E.g., 20 means spacing is reduced by 20% (3m becomes 2.4m).
    /// </summary>
    public double SpacingAdjustmentPercent { get; set; } = 0.0;
    public List<SpacingRule> CustomSpacingRules { get; set; } = new();
    
    // Support type settings
    public bool EnableHorizontalSupports { get; set; } = true;
    public bool EnableVerticalSupports { get; set; } = true;
    public bool PreferWallSupports { get; set; } = false;
    public double WallProximityMm { get; set; } = 200.0;
    
    // Vertical duct support settings
    /// <summary>
    /// Maximum distance from vertical duct to wall for wall-based support (in mm).
    /// If no wall is found within this distance, floor-based placement is used.
    /// </summary>
    public double VerticalDuctWallProximityMm { get; set; } = 500.0;
    
    /// <summary>
    /// Offset above floor for floor-based vertical duct supports (in mm).
    /// </summary>
    public double VerticalDuctFloorOffsetMm { get; set; } = 50.0;
    
    /// <summary>
    /// Use SMACNA spacing standards for vertical ducts.
    /// If false, uses fixed 2m spacing.
    /// </summary>
    public bool UseSmacnaVerticalSpacing { get; set; } = true;
    
    // Family filenames - relative to addin Families folder
    public string HorizontalCeilingSupportFamily { get; set; } = "RecDuctSupport.rfa";
    public string HorizontalGroundSupportFamily { get; set; } = "GroundDuctSupport.rfa";
    public string HorizontalWallSupportFamily { get; set; } = "WallSupportDuct.rfa";
    public string VerticalWallSupportFamily { get; set; } = "VerticalWallSupportDuct.rfa";
    public string VerticalFloorSupportFamily { get; set; } = "VerticalFloorSupportDuct.rfa";
    
    /// <summary>
    /// Get the full path to the families folder (next to the addin DLL).
    /// </summary>
    [JsonIgnore]
    public static string FamiliesFolder
    {
        get
        {
            // Try multiple methods to find the assembly directory
            string? assemblyDir = null;
            
            // Method 1: Assembly.Location (may be empty in .NET 5+)
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string location = assembly.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    assemblyDir = Path.GetDirectoryName(location);
                }
            }
            catch { }
            
            // Method 2: AppContext.BaseDirectory (reliable in .NET 5+)
            if (string.IsNullOrEmpty(assemblyDir))
            {
                assemblyDir = AppContext.BaseDirectory;
            }
            
            // Method 3: CodeBase (legacy, may work in some scenarios)
            if (string.IsNullOrEmpty(assemblyDir))
            {
                try
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var codeBase = assembly.CodeBase;
                    if (!string.IsNullOrEmpty(codeBase))
                    {
                        var uri = new Uri(codeBase);
                        assemblyDir = Path.GetDirectoryName(uri.LocalPath);
                    }
                }
                catch { }
            }
            
            // Method 4: Use Revit Addins folder directly
            if (string.IsNullOrEmpty(assemblyDir))
            {
                assemblyDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", "2025", "RectangularDuctSupport");
            }
            
            return Path.Combine(assemblyDir ?? "", "Families");
        }
    }
    
    /// <summary>
    /// Get full path to horizontal ceiling support family.
    /// </summary>
    [JsonIgnore]
    public string HorizontalCeilingSupportFamilyPath => ResolveFamilyPath(HorizontalCeilingSupportFamily);
    
    /// <summary>
    /// Get full path to horizontal ground support family.
    /// </summary>
    [JsonIgnore]
    public string HorizontalGroundSupportFamilyPath => ResolveFamilyPath(HorizontalGroundSupportFamily);
    
    /// <summary>
    /// Get full path to horizontal wall support family.
    /// </summary>
    [JsonIgnore]
    public string HorizontalWallSupportFamilyPath => ResolveFamilyPath(HorizontalWallSupportFamily);
    
    /// <summary>
    /// Get full path to vertical wall support family.
    /// </summary>
    [JsonIgnore]
    public string VerticalWallSupportFamilyPath => ResolveFamilyPath(VerticalWallSupportFamily);
    
    /// <summary>
    /// Get full path to vertical floor support family.
    /// </summary>
    [JsonIgnore]
    public string VerticalFloorSupportFamilyPath => ResolveFamilyPath(VerticalFloorSupportFamily);
    
    /// <summary>
    /// Resolve a family path - handles both absolute and relative paths.
    /// </summary>
    private string ResolveFamilyPath(string familyPath)
    {
        if (string.IsNullOrEmpty(familyPath))
            return string.Empty;
        
        // If it's an absolute path and file exists, use it
        if (Path.IsPathRooted(familyPath) && File.Exists(familyPath))
            return familyPath;
        
        // Get just the filename
        string fileName = Path.GetFileName(familyPath);
        
        // Try the Families folder
        string familiesPath = Path.Combine(FamiliesFolder, fileName);
        if (File.Exists(familiesPath))
            return familiesPath;
        
        // Return the path in Families folder regardless (for error reporting)
        return familiesPath;
    }
    
    // Structural settings
    public bool CalculateLoads { get; set; } = true;
    public bool RecommendRodSizes { get; set; } = true;
    public InsulationType DefaultInsulationType { get; set; } = InsulationType.MineralWool;
    public double CustomInsulationDensity { get; set; } = 40.0;
    
    // Export settings
    public bool ExportExcel { get; set; } = false;
    public bool ExportPdf { get; set; } = false;
    public bool TagSupports { get; set; } = false;
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    
    // UI settings
    public AppTheme Theme { get; set; } = AppTheme.System;
    
    // Constants for clearance
    public const double DefaultClearanceMm = 100.0; // 10 cm clearance
    
    /// <summary>
    /// Wall proximity in feet (internal units).
    /// </summary>
    [JsonIgnore]
    public double WallProximityFeet => WallProximityMm / 304.8;
    
    /// <summary>
    /// Vertical duct wall proximity in feet (internal units).
    /// </summary>
    [JsonIgnore]
    public double VerticalDuctWallProximityFeet => VerticalDuctWallProximityMm / 304.8;
    
    /// <summary>
    /// Vertical duct floor offset in feet (internal units).
    /// </summary>
    [JsonIgnore]
    public double VerticalDuctFloorOffsetFeet => VerticalDuctFloorOffsetMm / 304.8;
    
    /// <summary>
    /// Default clearance in feet.
    /// </summary>
    [JsonIgnore]
    public double ClearanceFeet => DefaultClearanceMm / 304.8;
    
    /// <summary>
    /// Config file path.
    /// </summary>
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AUS", "RectangularDuctSupport", "config.json");
    
    /// <summary>
    /// Load configuration from disk or return defaults.
    /// </summary>
    public static Configuration Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration();
            }
        }
        catch
        {
            // Return defaults on error
        }
        return new Configuration();
    }
    
    /// <summary>
    /// Save configuration to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
    
    /// <summary>
    /// Get insulation density based on current settings.
    /// </summary>
    public double GetInsulationDensity()
    {
        if (DefaultInsulationType == InsulationType.Custom)
            return CustomInsulationDensity;
        return InsulationInfo.GetDensity(DefaultInsulationType);
    }
}

/// <summary>
/// Results summary after placement operation.
/// </summary>
public class PlacementResults
{
    public int TotalDuctsProcessed { get; set; }
    public int TotalSupportsPlaced { get; set; }
    public int CeilingSupports { get; set; }
    public int GroundSupports { get; set; }
    public int WallSupports { get; set; }
    public int VerticalSupports { get; set; }
    public int SkippedDueToFittings { get; set; }
    public int SkippedDueToExisting { get; set; }
    public int SkippedDueToClash { get; set; }
    public int ClashesDetected { get; set; }
    public int ClashesResolved { get; set; }
    public double TotalLoadKg { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan ElapsedTime { get; set; }
    
    /// <summary>
    /// Get a summary string of the placement results.
    /// </summary>
    public string GetSummary()
    {
        var lines = new List<string>
        {
            $"Placement Complete",
            $"",
            $"Ducts Processed: {TotalDuctsProcessed}",
            $"Total Supports Placed: {TotalSupportsPlaced}",
            $"  - Ceiling: {CeilingSupports}",
            $"  - Ground: {GroundSupports}",
            $"  - Wall: {WallSupports}",
            $"  - Vertical: {VerticalSupports}"
        };
        
        if (TotalLoadKg > 0)
        {
            lines.Add($"Total Load: {TotalLoadKg:F1} kg");
        }
        
        if (SkippedDueToExisting > 0)
        {
            lines.Add($"Skipped (existing): {SkippedDueToExisting}");
        }
        
        if (ClashesDetected > 0)
        {
            lines.Add($"Clashes Detected: {ClashesDetected}");
        }
        
        if (ElapsedTime.TotalSeconds > 0)
        {
            lines.Add($"");
            lines.Add($"Time: {ElapsedTime.TotalSeconds:F1} seconds");
        }
        
        return string.Join("\n", lines);
    }
}
