using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace DuctSupportAddin;

/// <summary>
/// Main entry point for the Duct Support Add-in.
/// Implements IExternalApplication to create ribbon UI on Revit startup.
/// </summary>
public class App : IExternalApplication
{
    public static UIControlledApplication? UiApp { get; private set; }
    
    public Result OnStartup(UIControlledApplication application)
    {
        UiApp = application;
        
        try
        {
            CreateRibbonPanel(application);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("AUS Duct Support", $"Failed to load add-in: {ex.Message}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private void CreateRibbonPanel(UIControlledApplication application)
    {
        string tabName = "AUS";
        
        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch
        {
            // Tab may already exist
        }

        RibbonPanel panel = application.CreateRibbonPanel(tabName, "Duct Supports");
        
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        // Main command button
        PushButtonData placeSupportsData = new PushButtonData(
            "PlaceDuctSupports",
            "Place\nSupports",
            assemblyPath,
            "DuctSupportAddin.Commands.PlaceDuctSupportsCommand")
        {
            ToolTip = "Automatically place duct supports based on SMACNA and international standards",
            LongDescription = "Places supports on rectangular ducts with intelligent host detection " +
                              "(ceiling, beam, floor, wall). Supports multiple international standards " +
                              "including SMACNA, DW/144, VDI 3803, and AS 4254. Includes clash detection, " +
                              "load calculations, and comprehensive reporting."
        };

        // Try to set button images
        try
        {
            placeSupportsData.LargeImage = LoadImage("pack://application:,,,/RectangularDuctSupport;component/Resources/Icons/duct-support-32.png");
            placeSupportsData.Image = LoadImage("pack://application:,,,/RectangularDuctSupport;component/Resources/Icons/duct-support-16.png");
        }
        catch
        {
            // Images not found, continue without them
        }

        panel.AddItem(placeSupportsData);

        // Settings button
        PushButtonData settingsData = new PushButtonData(
            "DuctSupportSettings",
            "Settings",
            assemblyPath,
            "DuctSupportAddin.Commands.SettingsCommand")
        {
            ToolTip = "Configure default settings, themes, and language preferences"
        };

        panel.AddItem(settingsData);
    }

    private static BitmapImage? LoadImage(string uri)
    {
        try
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            image.EndInit();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
