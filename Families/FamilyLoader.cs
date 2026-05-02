using System.IO;
using Autodesk.Revit.DB;

namespace DuctSupportAddin.Families;

/// <summary>
/// Loads and manages support family files.
/// </summary>
public class FamilyLoader
{
    private readonly Document _doc;
    private readonly Dictionary<string, Family> _loadedFamilies = new();
    
    public FamilyLoader(Document doc)
    {
        _doc = doc;
    }
    
    /// <summary>
    /// Load a family from file path and return the first symbol.
    /// </summary>
    public FamilySymbol? LoadFamily(string familyPath)
    {
        try
        {
            // Normalize path
            familyPath = Environment.ExpandEnvironmentVariables(familyPath);
            
            // Check if already loaded in document
            var existingFamily = FindExistingFamily(familyPath);
            if (existingFamily != null)
            {
                return GetFirstSymbol(existingFamily);
            }
            
            // Check cache
            if (_loadedFamilies.TryGetValue(familyPath, out var cachedFamily))
            {
                return GetFirstSymbol(cachedFamily);
            }
            
            // Check file exists
            if (!File.Exists(familyPath))
            {
                // Try common locations
                familyPath = FindFamilyFile(familyPath);
                if (string.IsNullOrEmpty(familyPath))
                    return null;
            }
            
            // Load family - needs to be in a transaction
            Family? family = null;
            
            // Check if we're already in a transaction
            if (_doc.IsModifiable)
            {
                // Already in a transaction, just load
                bool loaded = _doc.LoadFamily(familyPath, new FamilyLoadOptions(), out family);
                if (!loaded || family == null)
                    return null;
            }
            else
            {
                // Start a new transaction to load the family
                using (var trans = new Transaction(_doc, "Load Support Family"))
                {
                    trans.Start();
                    try
                    {
                        bool loaded = _doc.LoadFamily(familyPath, new FamilyLoadOptions(), out family);
                        if (loaded && family != null)
                        {
                            trans.Commit();
                        }
                        else
                        {
                            trans.RollBack();
                            return null;
                        }
                    }
                    catch
                    {
                        trans.RollBack();
                        return null;
                    }
                }
            }
            
            if (family != null)
            {
                _loadedFamilies[familyPath] = family;
                return GetFirstSymbol(family);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Find existing family in document by name.
    /// </summary>
    private Family? FindExistingFamily(string familyPath)
    {
        string familyName = Path.GetFileNameWithoutExtension(familyPath);
        
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Get first symbol from family.
    /// </summary>
    private FamilySymbol? GetFirstSymbol(Family family)
    {
        var symbolIds = family.GetFamilySymbolIds();
        if (symbolIds.Count == 0)
            return null;
        
        var symbol = _doc.GetElement(symbolIds.First()) as FamilySymbol;
        
        // Activate symbol if not active
        if (symbol != null && !symbol.IsActive)
        {
            if (_doc.IsModifiable)
            {
                // Already in a transaction
                symbol.Activate();
                _doc.Regenerate();
            }
            else
            {
                // Need our own transaction
                using var trans = new Transaction(_doc, "Activate Symbol");
                trans.Start();
                symbol.Activate();
                _doc.Regenerate();
                trans.Commit();
            }
        }
        
        return symbol;
    }
    
    /// <summary>
    /// Search for family file in common locations.
    /// </summary>
    private string FindFamilyFile(string originalPath)
    {
        string fileName = Path.GetFileName(originalPath);
        
        // Get addin's Families folder
        string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string assemblyDir = Path.GetDirectoryName(assemblyLocation) ?? "";
        string addinFamiliesFolder = Path.Combine(assemblyDir, "Families");
        
        // Common search paths - addin folder first
        var searchPaths = new[]
        {
            addinFamiliesFolder,
            Path.GetDirectoryName(originalPath),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Revit Families"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AUS", "RectangularDuctSupport", "Families"),
            @"C:\ProgramData\Autodesk\RVT 2025\Libraries",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        
        foreach (var searchPath in searchPaths)
        {
            if (string.IsNullOrEmpty(searchPath))
                continue;
            
            var fullPath = Path.Combine(searchPath, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }
        
        return string.Empty;
    }
    
    /// <summary>
    /// Validate that a family file exists and can be read.
    /// </summary>
    public static (bool exists, string message) ValidateFamilyPath(string familyPath)
    {
        if (string.IsNullOrWhiteSpace(familyPath))
            return (false, "Path is empty");
        
        familyPath = Environment.ExpandEnvironmentVariables(familyPath);
        
        if (!File.Exists(familyPath))
            return (false, $"File not found: {familyPath}");
        
        if (!familyPath.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            return (false, "File must be a Revit family (.rfa)");
        
        return (true, "OK");
    }
}

/// <summary>
/// Options for family loading.
/// </summary>
public class FamilyLoadOptions : IFamilyLoadOptions
{
    public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
    {
        overwriteParameterValues = false;
        return true; // Continue loading
    }
    
    public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
    {
        source = FamilySource.Family;
        overwriteParameterValues = false;
        return true;
    }
}
