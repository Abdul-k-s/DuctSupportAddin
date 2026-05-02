using Autodesk.Revit.DB;

namespace DuctSupportAddin.Utilities;

/// <summary>
/// Unit conversion utilities.
/// </summary>
public static class UnitUtils
{
    /// <summary>Convert feet to millimeters.</summary>
    public static double FeetToMm(double feet) => feet * 304.8;
    
    /// <summary>Convert millimeters to feet.</summary>
    public static double MmToFeet(double mm) => mm / 304.8;
    
    /// <summary>Convert feet to meters.</summary>
    public static double FeetToM(double feet) => feet * 0.3048;
    
    /// <summary>Convert meters to feet.</summary>
    public static double MToFeet(double m) => m / 0.3048;
    
    /// <summary>Convert feet to centimeters.</summary>
    public static double FeetToCm(double feet) => feet * 30.48;
    
    /// <summary>Convert centimeters to feet.</summary>
    public static double CmToFeet(double cm) => cm / 30.48;
    
    /// <summary>Format dimension for display in mm.</summary>
    public static string FormatMm(double feet) => $"{FeetToMm(feet):N0} mm";
    
    /// <summary>Format dimension for display in m.</summary>
    public static string FormatM(double feet) => $"{FeetToM(feet):F2} m";
}

/// <summary>
/// Geometry utilities.
/// </summary>
public static class GeometryUtils
{
    /// <summary>
    /// Check if two vectors are parallel.
    /// </summary>
    public static bool AreParallel(XYZ a, XYZ b, double tolerance = 0.01)
    {
        var cross = a.CrossProduct(b);
        return cross.GetLength() < tolerance;
    }
    
    /// <summary>
    /// Check if two vectors are perpendicular.
    /// </summary>
    public static bool ArePerpendicular(XYZ a, XYZ b, double tolerance = 0.01)
    {
        return Math.Abs(a.DotProduct(b)) < tolerance;
    }
    
    /// <summary>
    /// Get horizontal distance between two points (ignoring Z).
    /// </summary>
    public static double HorizontalDistance(XYZ a, XYZ b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// Project point onto XY plane.
    /// </summary>
    public static XYZ FlattenToXY(XYZ point) => new XYZ(point.X, point.Y, 0);
    
    /// <summary>
    /// Get midpoint between two points.
    /// </summary>
    public static XYZ Midpoint(XYZ a, XYZ b) => (a + b) / 2;
}

/// <summary>
/// Revit document utilities.
/// </summary>
public static class RevitUtils
{
    /// <summary>
    /// Get project information.
    /// </summary>
    public static (string name, string number) GetProjectInfo(Document doc)
    {
        var projectInfo = doc.ProjectInformation;
        return (projectInfo?.Name ?? "Unknown", projectInfo?.Number ?? "");
    }
    
    /// <summary>
    /// Get all levels in document ordered by elevation.
    /// </summary>
    public static List<Level> GetLevels(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();
    }
    
    /// <summary>
    /// Find level at or below elevation.
    /// </summary>
    public static Level? FindLevelAtElevation(Document doc, double elevation)
    {
        return GetLevels(doc)
            .Where(l => l.Elevation <= elevation)
            .OrderByDescending(l => l.Elevation)
            .FirstOrDefault();
    }
}
