using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Analysis;

/// <summary>
/// Collects duct elements based on configuration settings.
/// </summary>
public class DuctCollector
{
    private readonly Document _doc;
    private readonly DuctAnalyzer _analyzer;
    
    public DuctCollector(Document doc)
    {
        _doc = doc;
        _analyzer = new DuctAnalyzer();
    }
    
    /// <summary>
    /// Collect and analyze ducts based on configuration.
    /// </summary>
    public List<DuctInfo> Collect(Configuration config, UIDocument? uiDoc = null, IProgress<string>? progress = null)
    {
        var results = new List<DuctInfo>();
        
        IEnumerable<Element> ducts = config.Scope switch
        {
            CollectionScope.EntireModel => CollectFromModel(),
            CollectionScope.ActiveView => CollectFromView(uiDoc?.ActiveView),
            CollectionScope.Selection => CollectFromSelection(uiDoc),
            _ => CollectFromModel()
        };
        
        int count = 0;
        foreach (var duct in ducts)
        {
            count++;
            if (count % 50 == 0)
            {
                progress?.Report($"Analyzing duct {count}...");
            }
            
            var info = _analyzer.Analyze(duct);
            if (info == null)
                continue;
            
            // Filter by system type
            if (!config.SystemTypes.HasFlag(info.SystemType))
                continue;
            
            // Skip sloped ducts
            if (info.Orientation == DuctOrientation.Sloped)
                continue;
            
            // Filter by orientation settings
            if (info.Orientation == DuctOrientation.Horizontal && !config.EnableHorizontalSupports)
                continue;
            if (info.Orientation == DuctOrientation.Vertical && !config.EnableVerticalSupports)
                continue;
            
            results.Add(info);
        }
        
        progress?.Report($"Found {results.Count} ducts to process");
        return results;
    }
    
    /// <summary>
    /// Collect all ducts from the entire model.
    /// </summary>
    private IEnumerable<Element> CollectFromModel()
    {
        return new FilteredElementCollector(_doc)
            .OfCategory(BuiltInCategory.OST_DuctCurves)
            .WhereElementIsNotElementType()
            .ToElements();
    }
    
    /// <summary>
    /// Collect ducts visible in the specified view.
    /// </summary>
    private IEnumerable<Element> CollectFromView(View? view)
    {
        if (view == null)
            return CollectFromModel();
        
        return new FilteredElementCollector(_doc, view.Id)
            .OfCategory(BuiltInCategory.OST_DuctCurves)
            .WhereElementIsNotElementType()
            .ToElements();
    }
    
    /// <summary>
    /// Collect ducts from current selection.
    /// </summary>
    private IEnumerable<Element> CollectFromSelection(UIDocument? uiDoc)
    {
        if (uiDoc == null)
            return Enumerable.Empty<Element>();
        
        var selection = uiDoc.Selection.GetElementIds();
        return selection
            .Select(id => _doc.GetElement(id))
            .Where(e => e != null && e.Category?.Id.Value == (long)BuiltInCategory.OST_DuctCurves)
            .Cast<Element>();
    }
    
    /// <summary>
    /// Get summary statistics for collected ducts.
    /// </summary>
    public static (int horizontal, int vertical, int total) GetStatistics(IEnumerable<DuctInfo> ducts)
    {
        var list = ducts.ToList();
        int horizontal = list.Count(d => d.Orientation == DuctOrientation.Horizontal);
        int vertical = list.Count(d => d.Orientation == DuctOrientation.Vertical);
        return (horizontal, vertical, list.Count);
    }
}
