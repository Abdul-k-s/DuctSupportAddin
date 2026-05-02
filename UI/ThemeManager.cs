using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.UI;

/// <summary>
/// Manages application themes (Light/Dark).
/// </summary>
public static class ThemeManager
{
    private static AppTheme _currentTheme = AppTheme.Light;
    
    /// <summary>
    /// Current theme.
    /// </summary>
    public static AppTheme CurrentTheme => _currentTheme;
    
    /// <summary>
    /// Apply theme to a window.
    /// </summary>
    public static void ApplyTheme(Window window, AppTheme theme)
    {
        _currentTheme = theme;
        
        // Determine actual theme
        var actualTheme = theme;
        if (theme == AppTheme.System)
        {
            actualTheme = IsSystemDarkMode() ? AppTheme.Dark : AppTheme.Light;
        }
        
        // Apply theme colors
        if (actualTheme == AppTheme.Dark)
        {
            ApplyDarkTheme(window);
        }
        else
        {
            ApplyLightTheme(window);
        }
    }
    
    /// <summary>
    /// Apply dark theme to window and all controls.
    /// </summary>
    private static void ApplyDarkTheme(Window window)
    {
        var bgColor = Color.FromRgb(30, 30, 30);
        var surfaceColor = Color.FromRgb(45, 45, 45);
        var borderColor = Color.FromRgb(77, 77, 77);
        var textColor = Colors.White;
        var secondaryTextColor = Color.FromRgb(200, 200, 200);
        
        window.Background = new SolidColorBrush(bgColor);
        window.Foreground = new SolidColorBrush(textColor);
        
        // Create and apply resource dictionary for dark theme
        var resources = new ResourceDictionary();
        resources[SystemColors.WindowBrushKey] = new SolidColorBrush(surfaceColor);
        resources[SystemColors.WindowTextBrushKey] = new SolidColorBrush(textColor);
        resources[SystemColors.ControlBrushKey] = new SolidColorBrush(surfaceColor);
        resources[SystemColors.ControlTextBrushKey] = new SolidColorBrush(textColor);
        resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(0, 120, 215));
        resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Colors.White);
        
        window.Resources.MergedDictionaries.Add(resources);
        
        // Apply to all controls recursively
        ApplyThemeToVisual(window, true, bgColor, surfaceColor, borderColor, textColor, secondaryTextColor);
    }
    
    /// <summary>
    /// Apply light theme to window and all controls.
    /// </summary>
    private static void ApplyLightTheme(Window window)
    {
        var bgColor = Colors.White;
        var surfaceColor = Color.FromRgb(249, 249, 249);
        var borderColor = Color.FromRgb(229, 229, 229);
        var textColor = Color.FromRgb(26, 26, 26);
        var secondaryTextColor = Color.FromRgb(96, 96, 96);
        
        window.Background = new SolidColorBrush(bgColor);
        window.Foreground = new SolidColorBrush(textColor);
        
        // Apply to all controls recursively
        ApplyThemeToVisual(window, false, bgColor, surfaceColor, borderColor, textColor, secondaryTextColor);
    }
    
    /// <summary>
    /// Recursively apply theme to all visual children.
    /// </summary>
    private static void ApplyThemeToVisual(DependencyObject parent, bool isDark, 
        Color bgColor, Color surfaceColor, Color borderColor, Color textColor, Color secondaryTextColor)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            
            // Style specific control types
            switch (child)
            {
                case Label label:
                    label.Foreground = new SolidColorBrush(textColor);
                    break;
                    
                case TextBlock textBlock:
                    textBlock.Foreground = new SolidColorBrush(textColor);
                    break;
                    
                case TextBox textBox:
                    textBox.Background = new SolidColorBrush(surfaceColor);
                    textBox.Foreground = new SolidColorBrush(textColor);
                    textBox.BorderBrush = new SolidColorBrush(borderColor);
                    textBox.CaretBrush = new SolidColorBrush(textColor);
                    break;
                    
                case ComboBox comboBox:
                    StyleComboBox(comboBox, isDark, surfaceColor, textColor, borderColor);
                    break;
                    
                case CheckBox checkBox:
                    checkBox.Foreground = new SolidColorBrush(textColor);
                    break;
                    
                case RadioButton radioButton:
                    radioButton.Foreground = new SolidColorBrush(textColor);
                    break;
                    
                case GroupBox groupBox:
                    groupBox.Foreground = new SolidColorBrush(textColor);
                    groupBox.BorderBrush = new SolidColorBrush(borderColor);
                    break;
                    
                case Button button:
                    // Skip buttons with explicit colors (like Place Supports)
                    if (button.Background is SolidColorBrush brush && brush.Color == Color.FromRgb(0, 120, 212))
                    {
                        // Keep the accent color
                        button.Foreground = new SolidColorBrush(Colors.White);
                    }
                    else
                    {
                        button.Background = new SolidColorBrush(surfaceColor);
                        button.Foreground = new SolidColorBrush(textColor);
                        button.BorderBrush = new SolidColorBrush(borderColor);
                    }
                    break;
                    
                case ScrollViewer scrollViewer:
                    scrollViewer.Background = new SolidColorBrush(bgColor);
                    break;
                    
                case Border border:
                    if (isDark && border.Background is SolidColorBrush borderBg)
                    {
                        // Only change light backgrounds to dark
                        if (IsLightColor(borderBg.Color))
                        {
                            border.Background = new SolidColorBrush(surfaceColor);
                        }
                    }
                    break;
            }
            
            // Recurse into children
            ApplyThemeToVisual(child, isDark, bgColor, surfaceColor, borderColor, textColor, secondaryTextColor);
        }
    }
    
    /// <summary>
    /// Style ComboBox with dark mode support.
    /// </summary>
    private static void StyleComboBox(ComboBox comboBox, bool isDark, Color surfaceColor, Color textColor, Color borderColor)
    {
        comboBox.Foreground = new SolidColorBrush(textColor);
        comboBox.BorderBrush = new SolidColorBrush(borderColor);
        
        if (isDark)
        {
            // Set background for the ComboBox
            comboBox.Background = new SolidColorBrush(surfaceColor);
            
            // Create style for ComboBoxItem
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(surfaceColor)));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, new SolidColorBrush(textColor)));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.BorderBrushProperty, new SolidColorBrush(borderColor)));
            
            // Add hover trigger
            var trigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(62, 62, 62))));
            itemStyle.Triggers.Add(trigger);
            
            // Add selected trigger
            var selectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 120, 215))));
            selectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, new SolidColorBrush(Colors.White)));
            itemStyle.Triggers.Add(selectedTrigger);
            
            comboBox.ItemContainerStyle = itemStyle;
            
            // Handle dropdown opened event
            comboBox.DropDownOpened -= ComboBox_DropDownOpened;
            comboBox.DropDownOpened += ComboBox_DropDownOpened;
        }
    }
    
    /// <summary>
    /// Handle ComboBox dropdown opened to style the popup.
    /// </summary>
    private static void ComboBox_DropDownOpened(object sender, EventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var surfaceColor = Color.FromRgb(45, 45, 45);
            var borderColor = Color.FromRgb(77, 77, 77);
            
            // Find and style the popup
            var popup = FindVisualChild<Popup>(comboBox);
            if (popup?.Child != null)
            {
                StylePopupContent(popup.Child, surfaceColor, borderColor);
            }
        }
    }
    
    /// <summary>
    /// Style popup content recursively.
    /// </summary>
    private static void StylePopupContent(DependencyObject element, Color surfaceColor, Color borderColor)
    {
        if (element is Border border)
        {
            border.Background = new SolidColorBrush(surfaceColor);
            border.BorderBrush = new SolidColorBrush(borderColor);
        }
        
        if (element is ScrollViewer scrollViewer)
        {
            scrollViewer.Background = new SolidColorBrush(surfaceColor);
        }
        
        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++)
        {
            StylePopupContent(VisualTreeHelper.GetChild(element, i), surfaceColor, borderColor);
        }
    }
    
    /// <summary>
    /// Check if a color is light (used to determine if we should change it).
    /// </summary>
    private static bool IsLightColor(Color color)
    {
        // Calculate relative luminance
        double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        return luminance > 0.5;
    }
    
    /// <summary>
    /// Find a visual child of a specific type.
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            
            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
    
    /// <summary>
    /// Apply theme from configuration.
    /// </summary>
    public static void ApplyConfiguration(Window window, Configuration config)
    {
        ApplyTheme(window, config.Theme);
    }
    
    /// <summary>
    /// Check if system is in dark mode (Windows 10+).
    /// </summary>
    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0; // 0 = Dark, 1 = Light
            }
        }
        catch
        {
            // Ignore registry errors
        }
        
        return false;
    }
    
    /// <summary>
    /// Get display name for theme.
    /// </summary>
    public static string GetThemeName(AppTheme theme) => theme switch
    {
        AppTheme.System => "System Default",
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        _ => theme.ToString()
    };
}
