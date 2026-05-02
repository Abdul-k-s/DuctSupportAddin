using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Validation;

/// <summary>
/// Detects clashes between planned support placements and existing elements.
/// </summary>
public class ClashDetector
{
    private readonly Document _doc;
    private readonly double _clearance;
    private readonly List<ElementData> _elements;
    
    /// <summary>
    /// Minimum clearance around supports (in feet).
    /// </summary>
    private const double DefaultClearance = 0.1; // ~30mm
    
    private class ElementData
    {
        public ElementId Id { get; init; } = ElementId.InvalidElementId;
        public string Category { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public BoundingBoxXYZ? BoundingBox { get; init; }
    }
    
    public ClashDetector(Document doc, double clearanceFeet = DefaultClearance)
    {
        _doc = doc;
        _clearance = clearanceFeet;
        _elements = CollectClashableElements();
    }
    
    /// <summary>
    /// Collect elements that supports could clash with.
    /// </summary>
    private List<ElementData> CollectClashableElements()
    {
        var elements = new List<ElementData>();
        
        // Categories to check for clashes
        var categories = new[]
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_MechanicalEquipment
        };
        
        foreach (var category in categories)
        {
            try
            {
                var categoryElements = new FilteredElementCollector(_doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();
                
                foreach (var element in categoryElements)
                {
                    var bbox = element.get_BoundingBox(null);
                    if (bbox != null)
                    {
                        elements.Add(new ElementData
                        {
                            Id = element.Id,
                            Category = element.Category?.Name ?? "Unknown",
                            Name = element.Name,
                            BoundingBox = bbox
                        });
                    }
                }
            }
            catch
            {
                // Skip categories that can't be collected
            }
        }
        
        return elements;
    }
    
    /// <summary>
    /// Check for clashes at a specific support placement.
    /// </summary>
    public ClashResult? CheckClash(SupportPlacement placement)
    {
        // Create approximate bounding box for support
        var supportBBox = CreateSupportBoundingBox(placement);
        
        foreach (var element in _elements)
        {
            // Skip the source duct
            if (element.Id == placement.DuctId)
                continue;
            
            if (element.BoundingBox == null)
                continue;
            
            // Check intersection
            var intersection = CheckIntersection(supportBBox, element.BoundingBox);
            
            if (intersection.intersects)
            {
                var severity = DetermineSeverity(intersection.overlap, element.Category);
                
                return new ClashResult
                {
                    Severity = severity,
                    ClashingElementId = element.Id,
                    ClashingCategory = element.Category,
                    ClashingElementName = element.Name,
                    OverlapDistance = -intersection.overlap, // Negative for overlap
                    Description = $"Clash with {element.Category}: {element.Name}"
                };
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Check clashes for multiple placements.
    /// </summary>
    public void CheckClashes(IEnumerable<SupportPlacement> placements, IProgress<string>? progress = null)
    {
        int count = 0;
        foreach (var placement in placements)
        {
            count++;
            if (count % 20 == 0)
            {
                progress?.Report($"Checking clash {count}...");
            }
            
            placement.Clash = CheckClash(placement);
        }
    }
    
    /// <summary>
    /// Create approximate bounding box for support.
    /// </summary>
    private BoundingBoxXYZ CreateSupportBoundingBox(SupportPlacement placement)
    {
        double halfWidth = placement.SupportWidth / 2;
        double height = placement.SupportType switch
        {
            SupportType.Ceiling => placement.RodLength,
            SupportType.Ground => placement.SupportHeight,
            SupportType.Wall => 0.5, // ~150mm depth
            SupportType.Vertical => placement.SupportLength,
            _ => 0.5
        };
        
        return new BoundingBoxXYZ
        {
            Min = new XYZ(
                placement.Location.X - halfWidth - _clearance,
                placement.Location.Y - halfWidth - _clearance,
                placement.Location.Z - _clearance),
            Max = new XYZ(
                placement.Location.X + halfWidth + _clearance,
                placement.Location.Y + halfWidth + _clearance,
                placement.Location.Z + height + _clearance)
        };
    }
    
    /// <summary>
    /// Check if two bounding boxes intersect.
    /// </summary>
    private (bool intersects, double overlap) CheckIntersection(BoundingBoxXYZ a, BoundingBoxXYZ b)
    {
        // Check for no intersection
        if (a.Max.X < b.Min.X || a.Min.X > b.Max.X ||
            a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y ||
            a.Max.Z < b.Min.Z || a.Min.Z > b.Max.Z)
        {
            return (false, 0);
        }
        
        // Calculate overlap distance
        double overlapX = Math.Min(a.Max.X, b.Max.X) - Math.Max(a.Min.X, b.Min.X);
        double overlapY = Math.Min(a.Max.Y, b.Max.Y) - Math.Max(a.Min.Y, b.Min.Y);
        double overlapZ = Math.Min(a.Max.Z, b.Max.Z) - Math.Max(a.Min.Z, b.Min.Z);
        
        double minOverlap = Math.Min(Math.Min(overlapX, overlapY), overlapZ);
        
        return (true, minOverlap);
    }
    
    /// <summary>
    /// Determine clash severity based on overlap and category.
    /// </summary>
    private ClashSeverity DetermineSeverity(double overlap, string category)
    {
        // Structural elements are critical
        if (category.Contains("Structural"))
        {
            return overlap > 0.05 ? ClashSeverity.Critical : ClashSeverity.Error;
        }
        
        // Large overlap is error
        if (overlap > 0.1) // > ~30mm
        {
            return ClashSeverity.Error;
        }
        
        // Small overlap is warning
        return ClashSeverity.Warning;
    }
    
    /// <summary>
    /// Get clash statistics.
    /// </summary>
    public static (int critical, int error, int warning) GetClashStats(IEnumerable<SupportPlacement> placements)
    {
        int critical = 0, error = 0, warning = 0;
        
        foreach (var placement in placements)
        {
            if (placement.Clash != null)
            {
                switch (placement.Clash.Severity)
                {
                    case ClashSeverity.Critical: critical++; break;
                    case ClashSeverity.Error: error++; break;
                    case ClashSeverity.Warning: warning++; break;
                }
            }
        }
        
        return (critical, error, warning);
    }
}
