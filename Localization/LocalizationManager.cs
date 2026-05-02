using System.Globalization;
using System.Resources;

namespace DuctSupportAddin.Localization;

/// <summary>
/// Manages localization resources for the add-in.
/// </summary>
public static class LocalizationManager
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
    
    /// <summary>
    /// Get the resource manager instance.
    /// </summary>
    private static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "DuctSupportAddin.Localization.Strings",
                typeof(LocalizationManager).Assembly);
            return _resourceManager;
        }
    }
    
    /// <summary>
    /// Set the current culture for localization.
    /// </summary>
    public static void SetCulture(string cultureName)
    {
        try
        {
            _currentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _currentCulture;
        }
        catch
        {
            _currentCulture = CultureInfo.InvariantCulture;
        }
    }
    
    /// <summary>
    /// Set to English.
    /// </summary>
    public static void SetEnglish() => SetCulture("en");
    
    /// <summary>
    /// Set to Arabic.
    /// </summary>
    public static void SetArabic() => SetCulture("ar");
    
    /// <summary>
    /// Get a localized string by key.
    /// </summary>
    public static string GetString(string key)
    {
        try
        {
            return ResourceManager.GetString(key, _currentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }
    
    /// <summary>
    /// Get a localized string with format arguments.
    /// </summary>
    public static string GetString(string key, params object[] args)
    {
        try
        {
            string format = GetString(key);
            return string.Format(format, args);
        }
        catch
        {
            return key;
        }
    }
    
    /// <summary>
    /// Check if current culture is RTL (right-to-left).
    /// </summary>
    public static bool IsRightToLeft => _currentCulture.TextInfo.IsRightToLeft;
    
    /// <summary>
    /// Current culture name.
    /// </summary>
    public static string CurrentCultureName => _currentCulture.Name;
}

/// <summary>
/// Extension methods for localization.
/// </summary>
public static class LocalizationExtensions
{
    /// <summary>
    /// Get localized string (shorthand).
    /// </summary>
    public static string L(this string key) => LocalizationManager.GetString(key);
}
