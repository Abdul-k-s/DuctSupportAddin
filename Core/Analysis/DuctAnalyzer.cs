using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using DuctSupportAddin.Models;
using RevitDuctSystemType = Autodesk.Revit.DB.Mechanical.DuctSystemType;

namespace DuctSupportAddin.Core.Analysis;

/// <summary>
/// Analyzes duct elements to extract geometry and properties.
/// </summary>
public class DuctAnalyzer
{
    private const double HorizontalTolerance = 0.0175; // ~1 degree in radians
    private const double VerticalTolerance = 0.0175;
    
    /// <summary>
    /// Analyze a duct element and return structured information.
    /// </summary>
    public DuctInfo? Analyze(Element element)
    {
        if (element is not Duct duct)
            return null;
        
        // Get location curve
        if (duct.Location is not LocationCurve locationCurve)
            return null;
        
        Curve curve = locationCurve.Curve;
        if (curve is not Line line)
            return null; // Only handle straight ducts
        
        XYZ startPoint = line.GetEndPoint(0);
        XYZ endPoint = line.GetEndPoint(1);
        XYZ direction = (endPoint - startPoint).Normalize();
        double length = line.Length;
        
        // Get duct dimensions
        double width = GetParameterValue(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
        double height = GetParameterValue(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
        
        // If rectangular params not found, try diameter (round duct - skip)
        if (width <= 0 || height <= 0)
        {
            double diameter = GetParameterValue(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
            if (diameter > 0)
                return null; // Round duct, not supported
        }
        
        // Determine orientation
        DuctOrientation orientation = DetermineOrientation(direction, startPoint, endPoint);
        
        // Get bottom elevation
        double bottomElevation = Math.Min(startPoint.Z, endPoint.Z) - height / 2;
        double topElevation = Math.Max(startPoint.Z, endPoint.Z) + height / 2;
        
        // Get system type
        Models.DuctSystemType systemType = GetSystemType(duct);
        
        // Get insulation
        double insulationThickness = GetInsulationThickness(duct);
        
        // Get level
        ElementId levelId = duct.ReferenceLevel?.Id ?? ElementId.InvalidElementId;
        string levelName = duct.ReferenceLevel?.Name ?? "Unknown";
        
        // Get system name
        string systemName = GetSystemName(duct);
        
        // Get width direction from connector coordinate system
        XYZ widthDirection = GetWidthDirection(duct, direction);
        
        return new DuctInfo
        {
            ElementId = duct.Id,
            Orientation = orientation,
            SystemType = systemType,
            Width = width,
            Height = height,
            StartPoint = startPoint,
            EndPoint = endPoint,
            Direction = direction,
            WidthDirection = widthDirection,
            Length = length,
            BottomElevation = bottomElevation,
            TopElevation = topElevation,
            InsulationThickness = insulationThickness,
            InsulationType = insulationThickness > 0 ? InsulationType.MineralWool : InsulationType.None,
            LevelId = levelId,
            LevelName = levelName,
            SystemName = systemName
        };
    }
    
    /// <summary>
    /// Determine duct orientation based on direction vector.
    /// </summary>
    private DuctOrientation DetermineOrientation(XYZ direction, XYZ start, XYZ end)
    {
        // Check if vertical (direction is primarily Z)
        double verticalComponent = Math.Abs(direction.Z);
        if (verticalComponent > 1 - VerticalTolerance)
        {
            return DuctOrientation.Vertical;
        }
        
        // Check if horizontal (start and end at same Z)
        double zDiff = Math.Abs(start.Z - end.Z);
        if (zDiff < HorizontalTolerance)
        {
            return DuctOrientation.Horizontal;
        }
        
        // Otherwise it's sloped
        return DuctOrientation.Sloped;
    }
    
    /// <summary>
    /// Get duct system type.
    /// </summary>
    private Models.DuctSystemType GetSystemType(Duct duct)
    {
        try
        {
            var connector = duct.ConnectorManager?.Connectors
                .Cast<Connector>()
                .FirstOrDefault(c => c.DuctSystemType != RevitDuctSystemType.UndefinedSystemType);
            
            if (connector != null)
            {
                return connector.DuctSystemType switch
                {
                    RevitDuctSystemType.SupplyAir => Models.DuctSystemType.Supply,
                    RevitDuctSystemType.ReturnAir => Models.DuctSystemType.Return,
                    RevitDuctSystemType.ExhaustAir => Models.DuctSystemType.Exhaust,
                    _ => Models.DuctSystemType.Other
                };
            }
        }
        catch
        {
            // Ignore errors in system type detection
        }
        
        return Models.DuctSystemType.Other;
    }
    
    /// <summary>
    /// Get system name for display.
    /// </summary>
    private string GetSystemName(Duct duct)
    {
        try
        {
            var systemParam = duct.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
            return systemParam?.AsString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
    
    /// <summary>
    /// Get insulation thickness from duct.
    /// </summary>
    private double GetInsulationThickness(Duct duct)
    {
        try
        {
            // Try to get insulation from associated insulation element
            var insulationIds = InsulationLiningBase.GetInsulationIds(duct.Document, duct.Id);
            foreach (ElementId insId in insulationIds)
            {
                var insulation = duct.Document.GetElement(insId);
                if (insulation != null)
                {
                    var thicknessParam = insulation.get_Parameter(BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS);
                    if (thicknessParam != null)
                        return thicknessParam.AsDouble();
                }
            }
            
            // Try duct's own insulation parameter
            var ductInsParam = duct.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT);
            if (ductInsParam != null && ductInsParam.AsDouble() > 0)
                return ductInsParam.AsDouble();
        }
        catch
        {
            // Ignore errors
        }
        
        return 0;
    }
    
    /// <summary>
    /// Get parameter value as double.
    /// </summary>
    private double GetParameterValue(Element element, BuiltInParameter param)
    {
        try
        {
            var parameter = element.get_Parameter(param);
            if (parameter != null && parameter.HasValue)
                return parameter.AsDouble();
        }
        catch
        {
            // Ignore
        }
        return 0;
    }
    
    /// <summary>
    /// Get the direction along which the duct Width is measured.
    /// This is extracted from the connector's coordinate system.
    /// </summary>
    /// <param name="duct">The duct element</param>
    /// <param name="flowDirection">The duct flow direction (centerline direction)</param>
    /// <returns>Normalized direction vector for Width measurement</returns>
    private XYZ GetWidthDirection(Duct duct, XYZ flowDirection)
    {
        try
        {
            // Get connector to extract coordinate system
            var connectorManager = duct.ConnectorManager;
            if (connectorManager != null)
            {
                foreach (Connector connector in connectorManager.Connectors)
                {
                    // Use the connector's coordinate system
                    // For rectangular ducts:
                    // - Origin is at connector center
                    // - BasisZ points along flow direction (out of connector)
                    // - BasisX is typically the "width" direction
                    // - BasisY is typically the "height" direction
                    var transform = connector.CoordinateSystem;
                    if (transform != null)
                    {
                        // BasisX from the connector's coordinate system is the width direction
                        XYZ widthDir = transform.BasisX;
                        
                        // Ensure it's perpendicular to flow direction (it should be, but normalize)
                        // Remove any component along flow direction
                        double dot = widthDir.DotProduct(flowDirection);
                        widthDir = (widthDir - flowDirection * dot);
                        
                        if (widthDir.GetLength() > 0.001)
                        {
                            return widthDir.Normalize();
                        }
                    }
                }
            }
        }
        catch
        {
            // Fall through to default
        }
        
        // Default: for vertical ducts, use X axis; for horizontal, perpendicular to flow and Z
        if (Math.Abs(flowDirection.Z) > 0.9)
        {
            // Vertical duct - default to X axis in XY plane
            return XYZ.BasisX;
        }
        else
        {
            // Horizontal duct - width direction is perpendicular to flow and horizontal
            XYZ widthDir = flowDirection.CrossProduct(XYZ.BasisZ);
            if (widthDir.GetLength() > 0.001)
            {
                return widthDir.Normalize();
            }
            return XYZ.BasisX;
        }
    }
}
