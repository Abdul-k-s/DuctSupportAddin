using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Validation;

/// <summary>
/// Checks for existing supports on ducts to avoid duplication.
/// </summary>
public class ExistingSupportChecker
{
    private readonly Document _doc;
    private readonly HashSet<ElementId> _ductsWithSupports;
    private readonly List<BoundingBoxXYZ> _existingSupportBounds;
    private readonly double _proximityThreshold;
    
    /// <summary>
    /// Distance threshold to consider a support as existing (in feet).
    /// </summary>
    private const double DefaultProximityThreshold = 0.5; // ~150mm
    
    public ExistingSupportChecker(Document doc, double proximityFeet = DefaultProximityThreshold)
    {
        _doc = doc;
        _proximityThreshold = proximityFeet;
        _ductsWithSupports = new HashSet<ElementId>();
        _existingSupportBounds = new List<BoundingBoxXYZ>();
        
        ScanExistingSupports();
    }
    
    /// <summary>
    /// Scan model for existing support elements.
    /// </summary>
    private void ScanExistingSupports()
    {
        // Categories that might be supports
        var supportCategories = new[]
        {
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_SpecialityEquipment
        };
        
        // Keywords that identify supports
        var supportKeywords = new[]
        {
            "support", "hanger", "bracket", "mount", "trapeze", "clamp", "دعامة"
        };
        
        foreach (var category in supportCategories)
        {
            try
            {
                var elements = new FilteredElementCollector(_doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();
                
                foreach (var element in elements)
                {
                    string name = (element.Name ?? string.Empty).ToLowerInvariant();
                    string typeName = string.Empty;
                    
                    try
                    {
                        var typeId = element.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            var type = _doc.GetElement(typeId);
                            typeName = (type?.Name ?? string.Empty).ToLowerInvariant();
                        }
                    }
                    catch { }
                    
                    // Check if element is a support based on name
                    bool isSupport = supportKeywords.Any(k => 
                        name.Contains(k) || typeName.Contains(k));
                    
                    if (isSupport)
                    {
                        var bbox = element.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            // Expand bounds slightly
                            var expanded = new BoundingBoxXYZ
                            {
                                Min = new XYZ(
                                    bbox.Min.X - _proximityThreshold,
                                    bbox.Min.Y - _proximityThreshold,
                                    bbox.Min.Z - _proximityThreshold),
                                Max = new XYZ(
                                    bbox.Max.X + _proximityThreshold,
                                    bbox.Max.Y + _proximityThreshold,
                                    bbox.Max.Z + _proximityThreshold)
                            };
                            _existingSupportBounds.Add(expanded);
                        }
                    }
                }
            }
            catch
            {
                // Skip categories that can't be collected
            }
        }
    }
    
    /// <summary>
    /// Check if a duct already has support near it.
    /// </summary>
    public bool HasExistingSupport(DuctInfo duct)
    {
        return _ductsWithSupports.Contains(duct.ElementId);
    }
    
    /// <summary>
    /// Check if there's an existing support near a point.
    /// </summary>
    public bool HasSupportNearPoint(XYZ point)
    {
        foreach (var bbox in _existingSupportBounds)
        {
            if (IsPointInBounds(point, bbox))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// Filter placements to remove those near existing supports.
    /// </summary>
    public List<SupportPlacement> FilterExisting(List<SupportPlacement> placements)
    {
        return placements
            .Where(p => !HasSupportNearPoint(p.Location))
            .ToList();
    }
    
    /// <summary>
    /// Get count of placements filtered due to existing supports.
    /// </summary>
    public int CountFiltered(List<SupportPlacement> original, List<SupportPlacement> filtered)
    {
        return original.Count - filtered.Count;
    }
    
    /// <summary>
    /// Check if point is within bounding box.
    /// </summary>
    private static bool IsPointInBounds(XYZ point, BoundingBoxXYZ bbox)
    {
        return point.X >= bbox.Min.X && point.X <= bbox.Max.X &&
               point.Y >= bbox.Min.Y && point.Y <= bbox.Max.Y &&
               point.Z >= bbox.Min.Z && point.Z <= bbox.Max.Z;
    }
    
    /// <summary>
    /// Get count of existing supports found.
    /// </summary>
    public int ExistingSupportCount => _existingSupportBounds.Count;
}
