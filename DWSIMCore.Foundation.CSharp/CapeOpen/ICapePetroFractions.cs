// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapePetroFractions
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// ICapePetroFractions interface
  /// Provides methods to identify a CAPE-OPEN component.
  /// </summary>
  [Guid("72A94DE9-9A69-4369-B508-C033CDFD4F81")]
  [Description("ICapePetroFractions Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapePetroFractions
  {
    /// <summary>
    /// Sets bulk characterization properties for the complete set of petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(1)]
    [Description("method SetPetroBulkProp")]
    void SetPetroBulkProp([In] string propertyID, [In] string basis, [In] double value);

    /// <summary>
    /// Sets characterization properties for individual petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("method SetPetroCompoundProp")]
    void SetPetroCompoundProp([In] string propertyID, [In] object compID, [In] string basis, [In] object values);

    /// <summary>
    /// Sets characterization property cruves for the complete set of petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method SetPetroCurveProp")]
    [DispId(3)]
    void SetPetroCurveProp([In] string propertyID, [In] string basis, [In] object Xvalues, [In] object Yvalues);

    /// <summary>
    /// Gets bulk characterization properties for the complete set of petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(4)]
    [Description("method GetPetroBulkProp")]
    double GetPetroBulkProp([In] string propertyID, [In] string basis);

    /// <summary>
    /// Gets characterization properties for individual petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method GetPetroCompoundProp")]
    [DispId(5)]
    object GetPetroCompoundProp([In] string propertyID, [In] object compID, [In] string basis);

    /// <summary>
    /// Gets characterization property cruves for the complete set of petroleum fractions
    /// </summary>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(6)]
    [Description("method GetPetroCurveProp")]
    object GetPetroCurveProp([In] string propertyID, [In] string basis);
  }
}
