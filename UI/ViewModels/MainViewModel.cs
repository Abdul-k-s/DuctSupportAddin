using System.Collections.ObjectModel;
using System.Windows.Input;
using DuctSupportAddin.Core.Spacing;
using DuctSupportAddin.Models;
using Microsoft.Win32;

namespace DuctSupportAddin.UI.ViewModels;

/// <summary>
/// ViewModel for the main configuration window.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly Configuration _config;
    
    public MainViewModel(Configuration config)
    {
        _config = config;
        InitializeCollections();
        LoadFromConfiguration();
        
        // Commands
        BrowseCeilingFamilyCommand = new RelayCommand(BrowseCeilingFamily);
        BrowseGroundFamilyCommand = new RelayCommand(BrowseGroundFamily);
        BrowseWallFamilyCommand = new RelayCommand(BrowseWallFamily);
        BrowseVerticalFamilyCommand = new RelayCommand(BrowseVerticalFamily);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
        PlaceSupportsCommand = new RelayCommand(PlaceSupports, () => CanPlaceSupports);
        PreviewCommand = new RelayCommand(Preview, () => CanPlaceSupports);
        CancelCommand = new RelayCommand(Cancel);
    }
    
    #region Collections
    
    public ObservableCollection<SpacingStandardItem> SpacingStandards { get; } = new();
    public ObservableCollection<InsulationTypeItem> InsulationTypes { get; } = new();
    public ObservableCollection<ThemeItem> Themes { get; } = new();
    
    private void InitializeCollections()
    {
        // Spacing standards
        foreach (var (type, name, region) in SpacingCalculator.GetAvailableStandards())
        {
            SpacingStandards.Add(new SpacingStandardItem(type, name, region));
        }
        
        // Insulation types
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.None, "None", 0));
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.MineralWool, "Mineral Wool", 40));
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.Fiberglass, "Fiberglass", 24));
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.Elastomeric, "Elastomeric", 60));
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.PIR_PUR, "PIR/PUR", 35));
        InsulationTypes.Add(new InsulationTypeItem(InsulationType.Custom, "Custom", 40));
        
        // Themes
        Themes.Add(new ThemeItem(AppTheme.System));
        Themes.Add(new ThemeItem(AppTheme.Light));
        Themes.Add(new ThemeItem(AppTheme.Dark));
    }
    
    #endregion
    
    #region Scope Properties
    
    private bool _scopeEntireModel = true;
    public bool ScopeEntireModel
    {
        get => _scopeEntireModel;
        set
        {
            if (SetProperty(ref _scopeEntireModel, value) && value)
                _config.Scope = CollectionScope.EntireModel;
        }
    }
    
    private bool _scopeActiveView;
    public bool ScopeActiveView
    {
        get => _scopeActiveView;
        set
        {
            if (SetProperty(ref _scopeActiveView, value) && value)
                _config.Scope = CollectionScope.ActiveView;
        }
    }
    
    private bool _scopeSelection;
    public bool ScopeSelection
    {
        get => _scopeSelection;
        set
        {
            if (SetProperty(ref _scopeSelection, value) && value)
                _config.Scope = CollectionScope.Selection;
        }
    }
    
    private bool _systemSupply = true;
    public bool SystemSupply
    {
        get => _systemSupply;
        set { SetProperty(ref _systemSupply, value); UpdateSystemTypes(); }
    }
    
    private bool _systemReturn = true;
    public bool SystemReturn
    {
        get => _systemReturn;
        set { SetProperty(ref _systemReturn, value); UpdateSystemTypes(); }
    }
    
    private bool _systemExhaust = true;
    public bool SystemExhaust
    {
        get => _systemExhaust;
        set { SetProperty(ref _systemExhaust, value); UpdateSystemTypes(); }
    }
    
    private bool _skipExistingSupports = true;
    public bool SkipExistingSupports
    {
        get => _skipExistingSupports;
        set { SetProperty(ref _skipExistingSupports, value); _config.SkipExistingSupports = value; }
    }
    
    private bool _considerInsulation = true;
    public bool ConsiderInsulation
    {
        get => _considerInsulation;
        set { SetProperty(ref _considerInsulation, value); _config.ConsiderInsulation = value; }
    }
    
    private void UpdateSystemTypes()
    {
        var types = DuctSystemType.None;
        if (SystemSupply) types |= DuctSystemType.Supply;
        if (SystemReturn) types |= DuctSystemType.Return;
        if (SystemExhaust) types |= DuctSystemType.Exhaust;
        _config.SystemTypes = types;
    }
    
    #endregion
    
    #region Spacing Properties
    
    private SpacingStandardItem? _selectedSpacingStandard;
    public SpacingStandardItem? SelectedSpacingStandard
    {
        get => _selectedSpacingStandard;
        set
        {
            if (SetProperty(ref _selectedSpacingStandard, value) && value != null)
            {
                _config.SpacingStandard = value.Value;
                UpdateActualSpacing();
            }
        }
    }
    
    private double _spacingAdjustmentPercent = 0;
    public double SpacingAdjustmentPercent
    {
        get => _spacingAdjustmentPercent;
        set
        {
            value = Math.Max(0, Math.Min(90, value)); // Clamp 0-90%
            if (SetProperty(ref _spacingAdjustmentPercent, value))
            {
                _config.SpacingAdjustmentPercent = value;
                UpdateActualSpacing();
            }
        }
    }
    
    private string _actualSpacing = "3.0m";
    public string ActualSpacing
    {
        get => _actualSpacing;
        private set => SetProperty(ref _actualSpacing, value);
    }
    
    private void UpdateActualSpacing()
    {
        // Default example spacing for 600mm duct
        double baseSpacing = SelectedSpacingStandard?.Value switch
        {
            SpacingStandardType.SMACNA => 3.0,
            SpacingStandardType.DW144 => 2.5,
            SpacingStandardType.VDI3803 => 3.0,
            SpacingStandardType.AS4254 => 2.4,
            _ => 3.0
        };
        
        // Adjustment reduces spacing: 20% adjustment means 80% of base
        double actual = baseSpacing * (1.0 - SpacingAdjustmentPercent / 100.0);
        ActualSpacing = $"{actual:F2}m (example for 600mm duct)";
    }
    
    #endregion
    
    #region Support Type Properties
    
    private bool _enableHorizontalSupports = true;
    public bool EnableHorizontalSupports
    {
        get => _enableHorizontalSupports;
        set { SetProperty(ref _enableHorizontalSupports, value); _config.EnableHorizontalSupports = value; }
    }
    
    private bool _preferWallSupports;
    public bool PreferWallSupports
    {
        get => _preferWallSupports;
        set { SetProperty(ref _preferWallSupports, value); _config.PreferWallSupports = value; }
    }
    
    private double _wallProximityMm = 200;
    public double WallProximityMm
    {
        get => _wallProximityMm;
        set { SetProperty(ref _wallProximityMm, value); _config.WallProximityMm = value; }
    }
    
    private bool _enableVerticalSupports = true;
    public bool EnableVerticalSupports
    {
        get => _enableVerticalSupports;
        set { SetProperty(ref _enableVerticalSupports, value); _config.EnableVerticalSupports = value; }
    }
    
    #endregion
    
    #region Family Properties
    
    private string _horizontalCeilingSupportFamily = string.Empty;
    public string HorizontalCeilingSupportFamily
    {
        get => _horizontalCeilingSupportFamily;
        set { SetProperty(ref _horizontalCeilingSupportFamily, value); _config.HorizontalCeilingSupportFamily = value; }
    }
    
    private string _horizontalGroundSupportFamily = string.Empty;
    public string HorizontalGroundSupportFamily
    {
        get => _horizontalGroundSupportFamily;
        set { SetProperty(ref _horizontalGroundSupportFamily, value); _config.HorizontalGroundSupportFamily = value; }
    }
    
    private string _horizontalWallSupportFamily = string.Empty;
    public string HorizontalWallSupportFamily
    {
        get => _horizontalWallSupportFamily;
        set { SetProperty(ref _horizontalWallSupportFamily, value); _config.HorizontalWallSupportFamily = value; }
    }
    
    private string _verticalWallSupportFamily = string.Empty;
    public string VerticalWallSupportFamily
    {
        get => _verticalWallSupportFamily;
        set { SetProperty(ref _verticalWallSupportFamily, value); _config.VerticalWallSupportFamily = value; }
    }
    
    #endregion
    
    #region Calculation Properties
    
    private bool _calculateLoads = true;
    public bool CalculateLoads
    {
        get => _calculateLoads;
        set { SetProperty(ref _calculateLoads, value); _config.CalculateLoads = value; }
    }
    
    private bool _recommendRodSizes = true;
    public bool RecommendRodSizes
    {
        get => _recommendRodSizes;
        set { SetProperty(ref _recommendRodSizes, value); _config.RecommendRodSizes = value; }
    }
    
    private InsulationTypeItem? _selectedInsulationType;
    public InsulationTypeItem? SelectedInsulationType
    {
        get => _selectedInsulationType;
        set
        {
            if (SetProperty(ref _selectedInsulationType, value) && value != null)
            {
                _config.DefaultInsulationType = value.Value;
            }
        }
    }
    
    #endregion
    
    #region Export Properties
    
    private bool _exportExcel;
    public bool ExportExcel
    {
        get => _exportExcel;
        set { SetProperty(ref _exportExcel, value); _config.ExportExcel = value; }
    }
    
    private bool _exportPdf;
    public bool ExportPdf
    {
        get => _exportPdf;
        set { SetProperty(ref _exportPdf, value); _config.ExportPdf = value; }
    }
    
    private bool _tagSupports;
    public bool TagSupports
    {
        get => _tagSupports;
        set { SetProperty(ref _tagSupports, value); _config.TagSupports = value; }
    }
    
    private string _outputFolder = string.Empty;
    public string OutputFolder
    {
        get => _outputFolder;
        set { SetProperty(ref _outputFolder, value); _config.OutputFolder = value; }
    }
    
    #endregion
    
    #region UI Properties
    
    private ThemeItem? _selectedTheme;
    public ThemeItem? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value) && value != null)
            {
                _config.Theme = value.Value;
                OnThemeChanged?.Invoke(value.Value);
            }
        }
    }
    
    public event Action<AppTheme>? OnThemeChanged;
    
    #endregion
    
    #region Commands
    
    public ICommand BrowseCeilingFamilyCommand { get; }
    public ICommand BrowseGroundFamilyCommand { get; }
    public ICommand BrowseWallFamilyCommand { get; }
    public ICommand BrowseVerticalFamilyCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand PlaceSupportsCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand CancelCommand { get; }
    
    public bool CanPlaceSupports => EnableHorizontalSupports || EnableVerticalSupports;
    
    // Events for window communication
    public event Action? OnPlaceRequested;
    public event Action? OnPreviewRequested;
    public event Action? OnCancelRequested;
    
    private void BrowseCeilingFamily()
    {
        var path = BrowseFamily();
        if (!string.IsNullOrEmpty(path)) HorizontalCeilingSupportFamily = path;
    }
    
    private void BrowseGroundFamily()
    {
        var path = BrowseFamily();
        if (!string.IsNullOrEmpty(path)) HorizontalGroundSupportFamily = path;
    }
    
    private void BrowseWallFamily()
    {
        var path = BrowseFamily();
        if (!string.IsNullOrEmpty(path)) HorizontalWallSupportFamily = path;
    }
    
    private void BrowseVerticalFamily()
    {
        var path = BrowseFamily();
        if (!string.IsNullOrEmpty(path)) VerticalWallSupportFamily = path;
    }
    
    private string? BrowseFamily()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Revit Family (*.rfa)|*.rfa",
            Title = "Select Support Family"
        };
        
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    
    private void BrowseOutputFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder"
        };
        
        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
        }
    }
    
    private void PlaceSupports() => OnPlaceRequested?.Invoke();
    private void Preview() => OnPreviewRequested?.Invoke();
    private void Cancel() => OnCancelRequested?.Invoke();
    
    #endregion
    
    #region Configuration
    
    private void LoadFromConfiguration()
    {
        // Scope
        ScopeEntireModel = _config.Scope == CollectionScope.EntireModel;
        ScopeActiveView = _config.Scope == CollectionScope.ActiveView;
        ScopeSelection = _config.Scope == CollectionScope.Selection;
        
        // Systems
        SystemSupply = _config.SystemTypes.HasFlag(DuctSystemType.Supply);
        SystemReturn = _config.SystemTypes.HasFlag(DuctSystemType.Return);
        SystemExhaust = _config.SystemTypes.HasFlag(DuctSystemType.Exhaust);
        
        // Options
        SkipExistingSupports = _config.SkipExistingSupports;
        ConsiderInsulation = _config.ConsiderInsulation;
        
        // Spacing
        SelectedSpacingStandard = SpacingStandards.FirstOrDefault(s => s.Value == _config.SpacingStandard);
        SpacingAdjustmentPercent = _config.SpacingAdjustmentPercent;
        
        // Support types
        EnableHorizontalSupports = _config.EnableHorizontalSupports;
        EnableVerticalSupports = _config.EnableVerticalSupports;
        PreferWallSupports = _config.PreferWallSupports;
        WallProximityMm = _config.WallProximityMm;
        
        // Families
        HorizontalCeilingSupportFamily = _config.HorizontalCeilingSupportFamily;
        HorizontalGroundSupportFamily = _config.HorizontalGroundSupportFamily;
        HorizontalWallSupportFamily = _config.HorizontalWallSupportFamily;
        VerticalWallSupportFamily = _config.VerticalWallSupportFamily;
        
        // Calculations
        CalculateLoads = _config.CalculateLoads;
        RecommendRodSizes = _config.RecommendRodSizes;
        SelectedInsulationType = InsulationTypes.FirstOrDefault(i => i.Value == _config.DefaultInsulationType);
        
        // Export
        ExportExcel = _config.ExportExcel;
        ExportPdf = _config.ExportPdf;
        TagSupports = _config.TagSupports;
        OutputFolder = _config.OutputFolder;
        
        // UI
        SelectedTheme = Themes.FirstOrDefault(t => t.Value == _config.Theme);
    }
    
    public Configuration GetConfiguration()
    {
        _config.Save();
        return _config;
    }
    
    #endregion
}

