using System.Windows;
using DuctSupportAddin.Models;
using DuctSupportAddin.UI.ViewModels;

namespace DuctSupportAddin.UI.Views;

/// <summary>
/// Main configuration window for duct support placement.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    /// <summary>
    /// Result of the dialog - the configuration to use.
    /// </summary>
    public Configuration? ResultConfiguration { get; private set; }
    
    /// <summary>
    /// Whether to show preview before placing.
    /// </summary>
    public bool ShowPreview { get; private set; }
    
    public MainWindow(Configuration config)
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel(config);
        DataContext = _viewModel;
        
        // Wire up events
        _viewModel.OnPlaceRequested += OnPlaceRequested;
        _viewModel.OnPreviewRequested += OnPreviewRequested;
        _viewModel.OnCancelRequested += OnCancelRequested;
        _viewModel.OnThemeChanged += OnThemeChanged;
        
        // Apply initial theme
        ThemeManager.ApplyConfiguration(this, config);
    }
    
    private void OnPlaceRequested()
    {
        ShowPreview = false;
        ResultConfiguration = _viewModel.GetConfiguration();
        DialogResult = true;
        Close();
    }
    
    private void OnPreviewRequested()
    {
        ShowPreview = true;
        ResultConfiguration = _viewModel.GetConfiguration();
        DialogResult = true;
        Close();
    }
    
    private void OnCancelRequested()
    {
        DialogResult = false;
        Close();
    }
    
    private void OnThemeChanged(AppTheme theme)
    {
        ThemeManager.ApplyTheme(this, theme);
    }
}
