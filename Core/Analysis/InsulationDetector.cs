using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Analysis;

/// <summary>
/// Detects insulation on ducts and calculates insulation properties.
/// </summary>
public class InsulationDetector
{
    private readonly Document _doc;
    private readonly InsulationType _defaultType;
    private readonly double _customDensity;
    
    public InsulationDetector(Document doc, InsulationType defaultType = InsulationType.MineralWool, double customDensity = 40.0)
    {
        _doc = doc;
        _defaultType = defaultType;
        _customDensity = customDensity;
    }
    
    /// <summary>
    /// Get insulation information for a duct.
    /// </summary>
    public InsulationInfo GetInsulation(ElementId ductId)
    {
        var duct = _doc.GetElement(ductId);
        if (duct == null)
            return new InsulationInfo { Type = InsulationType.None };
        
        return GetInsulation(duct);
    }
    
    /// <summary>
    /// Get insulation information for a duct element.
    /// </summary>
    public InsulationInfo GetInsulation(Element duct)
    {
        double thickness = 0;
        InsulationType type = InsulationType.None;
        
        try
        {
            // Method 1: Check for associated insulation element
            var insulationIds = InsulationLiningBase.GetInsulationIds(_doc, duct.Id);
            foreach (ElementId insId in insulationIds)
            {
                var insulation = _doc.GetElement(insId);
                if (insulation != null)
                {
                    var thicknessParam = insulation.get_Parameter(BuiltInParameter.RBS_REFERENCE_INSULATION_THICKNESS);
                    if (thicknessParam != null && thicknessParam.AsDouble() > 0)
                    {
                        thickness = thicknessParam.AsDouble();
                        type = DetectInsulationType(insulation);
                        break;
                    }
                }
            }
            
            // Method 2: Check duct's own insulation parameter
            if (thickness <= 0)
            {
                var ductInsParam = duct.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT);
                if (ductInsParam != null && ductInsParam.AsDouble() > 0)
                {
                    thickness = ductInsParam.AsDouble();
                    type = _defaultType;
                }
            }
        }
        catch
        {
            // Ignore errors, return no insulation
        }
        
        if (thickness <= 0)
            return new InsulationInfo { Type = InsulationType.None };
        
        // Use default type if couldn't detect
        if (type == InsulationType.None)
            type = _defaultType;
        
        double density = type == InsulationType.Custom ? _customDensity : InsulationInfo.GetDensity(type);
        
        return new InsulationInfo
        {
            Thickness = thickness,
            Type = type,
            DensityKgM3 = density
        };
    }
    
    /// <summary>
    /// Detect insulation type from element.
    /// </summary>
    private InsulationType DetectInsulationType(Element insulation)
    {
        try
        {
            string? typeName = insulation.Name?.ToLowerInvariant();
            if (string.IsNullOrEmpty(typeName))
                return _defaultType;
            
            if (typeName.Contains("mineral") || typeName.Contains("rock"))
                return InsulationType.MineralWool;
            if (typeName.Contains("fiber") || typeName.Contains("glass"))
                return InsulationType.Fiberglass;
            if (typeName.Contains("elastomer") || typeName.Contains("rubber"))
                return InsulationType.Elastomeric;
            if (typeName.Contains("pir") || typeName.Contains("pur") || typeName.Contains("polyurethane"))
                return InsulationType.PIR_PUR;
        }
        catch
        {
            // Ignore
        }
        
        return _defaultType;
    }
    
    /// <summary>
    /// Calculate total support width including insulation.
    /// </summary>
    public double CalculateSupportWidth(double ductWidth, double insulationThickness, double clearance)
    {
        // Support width = duct width + insulation on both sides + clearance
        return ductWidth + (insulationThickness * 2) + clearance;
    }
    
    /// <summary>
    /// Get insulation weight per meter of duct.
    /// </summary>
    public double GetInsulationWeightPerMeter(DuctInfo duct, InsulationInfo insulation)
    {
        if (!insulation.HasInsulation)
            return 0;
        
        return insulation.GetWeightPerMeter(duct.Perimeter);
    }
}
