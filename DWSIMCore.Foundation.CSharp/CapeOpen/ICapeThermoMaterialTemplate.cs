// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoMaterialTemplate
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Material Template interface</summary>
  [Description("ICapeThermoMaterialTemplate Interface")]
  [ComVisible(false)]
  [Guid("678c0993-7d66-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeThermoMaterialTemplate
  {
    /// <summary>Create a material object from this Template</summary>
    /// <remarks>
    /// Allows a Material Object to be created from the Material Template interface.
    /// </remarks>
    /// <returns>The created/initialized Material Object.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">ECapeOutOfResources</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(1)]
    [Description("method CreateMaterialObject")]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object CreateMaterialObject();

    /// <summary>Set some property value(s)</summary>
    /// <remarks>
    /// Allows custom property and values to be set on the Material Template to
    /// support pseudo components.
    /// </remarks>
    /// <param name="property">The custom property to set.</param>
    /// <param name="values">
    /// The actual values of the property. A System.Object containing a double
    /// array marshalled from a COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(2)]
    [Description("method SetProp")]
    void SetProp(string property, object values);
  }
}
