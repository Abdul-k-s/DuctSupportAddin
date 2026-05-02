namespace DuctSupportAddin.Models;

/// <summary>
/// Defines the type of support based on hosting conditions.
/// </summary>
public enum SupportType
{
    /// <summary>Ceiling or beam mounted hanging support</summary>
    Ceiling,
    
    /// <summary>Floor/ground mounted support when no ceiling above</summary>
    Ground,
    
    /// <summary>Wall mounted support for horizontal ducts near walls</summary>
    Wall,
    
    /// <summary>Wall mounted support for vertical ducts (VerticalWallSupportDuct.rfa)</summary>
    Vertical,
    
    /// <summary>Floor-based support for vertical ducts at floor penetrations (VerticalFloorSupportDuct.rfa)</summary>
    VerticalFloor
}

/// <summary>
/// Defines duct orientation classification.
/// </summary>
public enum DuctOrientation
{
    /// <summary>Duct runs horizontally (level)</summary>
    Horizontal,
    
    /// <summary>Duct runs vertically (riser)</summary>
    Vertical,
    
    /// <summary>Duct runs at an angle - skipped for support placement</summary>
    Sloped
}

/// <summary>
/// Defines duct system types for filtering.
/// </summary>
[Flags]
public enum DuctSystemType
{
    None = 0,
    Supply = 1,
    Return = 2,
    Exhaust = 4,
    Other = 8,
    All = Supply | Return | Exhaust | Other
}

/// <summary>
/// Defines the scope of duct collection.
/// </summary>
public enum CollectionScope
{
    /// <summary>Process all ducts in the entire model</summary>
    EntireModel,
    
    /// <summary>Process only ducts visible in active view</summary>
    ActiveView,
    
    /// <summary>Process only currently selected ducts</summary>
    Selection
}

/// <summary>
/// Defines available spacing standards.
/// </summary>
public enum SpacingStandardType
{
    /// <summary>SMACNA - North America</summary>
    SMACNA,
    
    /// <summary>DW/144 - United Kingdom</summary>
    DW144,
    
    /// <summary>VDI 3803 - Germany</summary>
    VDI3803,
    
    /// <summary>AS 4254 - Australia</summary>
    AS4254,
    
    /// <summary>User-defined custom rules</summary>
    Custom
}

/// <summary>
/// Defines insulation material types with their densities.
/// </summary>
public enum InsulationType
{
    None,
    MineralWool,    // 40 kg/m³
    Fiberglass,     // 24 kg/m³
    Elastomeric,    // 60 kg/m³
    PIR_PUR,        // 35 kg/m³
    Custom
}

/// <summary>
/// Defines clash severity levels.
/// </summary>
public enum ClashSeverity
{
    /// <summary>No clash detected</summary>
    None,
    
    /// <summary>Warning - minor issue that can be ignored</summary>
    Warning,
    
    /// <summary>Error - clash that should be resolved</summary>
    Error,
    
    /// <summary>Critical - severe clash that must be resolved</summary>
    Critical
}

/// <summary>
/// Defines clash resolution actions.
/// </summary>
public enum ClashResolution
{
    /// <summary>Skip placing this support</summary>
    Skip,
    
    /// <summary>Offset support to avoid clash</summary>
    Offset,
    
    /// <summary>Ignore clash and place anyway</summary>
    Ignore
}

/// <summary>
/// Defines application theme options.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow system theme</summary>
    System,
    
    /// <summary>Light theme</summary>
    Light,
    
    /// <summary>Dark theme</summary>
    Dark
}

/// <summary>
/// Defines application language options.
/// </summary>
public enum AppLanguage
{
    /// <summary>English</summary>
    English,
    
    /// <summary>Arabic (RTL)</summary>
    Arabic
}
