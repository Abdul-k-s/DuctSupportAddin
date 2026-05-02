using Autodesk.Revit.DB;
using DuctSupportAddin.Models;

namespace DuctSupportAddin.Core.Placement;

/// <summary>
/// Sets parameters on placed support family instances.
/// </summary>
public class ParameterSetter
{
    // Parameter names for RecDuctSupport (Ceiling/Beam mounted)
    public static class CeilingSupportParams
    {
        public const string DuctHeight = "Duct_Height";   // Raycast from duct bottom to ceiling/beam
        public const string DuctWidth = "Duct_Width";     // Width of duct
    }
    
    // Parameter names for GroundDuctSupport
    public static class GroundSupportParams
    {
        public const string SupportHeight = "Support_Height";  // Duct bottom elevation
        public const string DuctWidth = "Duct_Width";          // Width of duct
    }
    
    // Parameter names for WallSupportDuct (Horizontal duct near wall)
    // Generic model, base at wall face
    // Elevation = bottom of duct (set by placement Z coordinate)
    public static class WallSupportParams
    {
        public const string SupportLength = "Support_Length";  // Duct width + 30mm + offset
    }
    
    // Parameter names for VerticalWallSupportDuct
    // U-shape support, wall-based family
    public static class VerticalSupportParams
    {
        public const string ClampSize = "Clamp_Size";    // Duct face parallel to wall (width of clamp)
        public const string ArmLength = "Arm_Length";    // Distance from wall to end of duct
    }
    
    // Parameter names for VerticalFloorSupportDuct
    // Rectangular support surrounding vertical duct, floor-hosted
    public static class VerticalFloorSupportParams
    {
        public const string X = "X";  // Duct face dimension along X axis
        public const string Y = "Y";  // Duct face dimension along Y axis
    }
    
    /// <summary>
    /// Set parameters on a placed family instance based on support type.
    /// </summary>
    public bool SetParameters(FamilyInstance instance, SupportPlacement placement)
    {
        try
        {
            return placement.SupportType switch
            {
                SupportType.Ceiling => SetCeilingSupportParams(instance, placement),
                SupportType.Ground => SetGroundSupportParams(instance, placement),
                SupportType.Wall => SetWallSupportParams(instance, placement),
                SupportType.Vertical => SetVerticalSupportParams(instance, placement),
                SupportType.VerticalFloor => SetVerticalFloorSupportParams(instance, placement),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Set parameters for ceiling/beam mounted support (RecDuctSupport.rfa).
    /// Duct_Height = raycast from duct bottom upward to ceiling/beam
    /// Duct_Width = duct width
    /// </summary>
    private bool SetCeilingSupportParams(FamilyInstance instance, SupportPlacement placement)
    {
        bool success = true;
        
        // Duct_Height parameter (raycast result)
        var ductHeightParam = instance.LookupParameter(CeilingSupportParams.DuctHeight);
        if (ductHeightParam != null && !ductHeightParam.IsReadOnly)
        {
            success &= ductHeightParam.Set(placement.DuctHeight);
        }
        
        // Duct_Width parameter
        var ductWidthParam = instance.LookupParameter(CeilingSupportParams.DuctWidth);
        if (ductWidthParam != null && !ductWidthParam.IsReadOnly)
        {
            success &= ductWidthParam.Set(placement.DuctWidth);
        }
        
        return success;
    }
    
    /// <summary>
    /// Set parameters for ground/floor mounted support (GroundDuctSupport.rfa).
    /// Support_Height = duct bottom elevation
    /// Duct_Width = duct width
    /// </summary>
    private bool SetGroundSupportParams(FamilyInstance instance, SupportPlacement placement)
    {
        bool success = true;
        
        // Support_Height parameter (duct bottom elevation)
        var heightParam = instance.LookupParameter(GroundSupportParams.SupportHeight);
        if (heightParam != null && !heightParam.IsReadOnly)
        {
            success &= heightParam.Set(placement.SupportHeight);
        }
        
        // Duct_Width parameter
        var widthParam = instance.LookupParameter(GroundSupportParams.DuctWidth);
        if (widthParam != null && !widthParam.IsReadOnly)
        {
            success &= widthParam.Set(placement.DuctWidth);
        }
        
        return success;
    }
    
    /// <summary>
    /// Set parameters for wall mounted support for horizontal duct (WallSupportDuct.rfa).
    /// 
    /// Family: Generic model, base at wall face
    /// Elevation: Set by placement Z coordinate (bottom of duct)
    /// 
    /// Parameters:
    /// - Support_Length = duct width + 30mm + offset from duct to wall
    /// </summary>
    private bool SetWallSupportParams(FamilyInstance instance, SupportPlacement placement)
    {
        bool success = true;
        
        // Support_Length parameter
        var lengthParam = instance.LookupParameter(WallSupportParams.SupportLength);
        if (lengthParam != null && !lengthParam.IsReadOnly)
        {
            success &= lengthParam.Set(placement.WallSupportLength);
        }
        
        return success;
    }
    
    /// <summary>
    /// Set parameters for vertical duct wall support (VerticalWallSupportDuct.rfa).
    /// 
    /// Family: Wall-based, U-shaped bracket surrounding vertical duct
    /// Origin: At wall face
    /// 
    /// Parameters:
    /// - Clamp_Size = duct face parallel to wall (horizontal width of clamp)
    /// - Arm_Length = distance from wall face to far edge of duct
    /// </summary>
    private bool SetVerticalSupportParams(FamilyInstance instance, SupportPlacement placement)
    {
        bool success = true;
        
        // Clamp_Size parameter (duct dimension parallel to wall)
        var widthParam = instance.LookupParameter(VerticalSupportParams.ClampSize);
        if (widthParam != null && !widthParam.IsReadOnly)
        {
            success &= widthParam.Set(placement.VerticalSupportWidth);
        }
        
        // Arm_Length parameter (distance from wall to end of duct)
        var lengthParam = instance.LookupParameter(VerticalSupportParams.ArmLength);
        if (lengthParam != null && !lengthParam.IsReadOnly)
        {
            success &= lengthParam.Set(placement.VerticalSupportLength);
        }
        
        return success;
    }
    
    /// <summary>
    /// Set parameters for vertical duct floor support (VerticalFloorSupportDuct.rfa).
    /// 
    /// Family: Floor-hosted, rectangular support surrounding vertical duct
    /// Origin: At duct center on floor
    /// 
    /// Parameters:
    /// - X = duct face dimension along X axis
    /// - Y = duct face dimension along Y axis
    /// </summary>
    private bool SetVerticalFloorSupportParams(FamilyInstance instance, SupportPlacement placement)
    {
        bool success = true;
        
        // X parameter (duct face along X axis)
        var xParam = instance.LookupParameter(VerticalFloorSupportParams.X);
        if (xParam != null && !xParam.IsReadOnly)
        {
            success &= xParam.Set(placement.FloorSupportX);
        }
        
        // Y parameter (duct face along Y axis)
        var yParam = instance.LookupParameter(VerticalFloorSupportParams.Y);
        if (yParam != null && !yParam.IsReadOnly)
        {
            success &= yParam.Set(placement.FloorSupportY);
        }
        
        return success;
    }
    
    /// <summary>
    /// Try to set a parameter by name with a double value.
    /// </summary>
    public static bool TrySetParameter(FamilyInstance instance, string paramName, double value)
    {
        var param = instance.LookupParameter(paramName);
        if (param != null && !param.IsReadOnly && param.StorageType == StorageType.Double)
        {
            return param.Set(value);
        }
        return false;
    }
    
    /// <summary>
    /// Try to set a parameter by name with a string value.
    /// </summary>
    public static bool TrySetParameter(FamilyInstance instance, string paramName, string value)
    {
        var param = instance.LookupParameter(paramName);
        if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
        {
            return param.Set(value);
        }
        return false;
    }
}
