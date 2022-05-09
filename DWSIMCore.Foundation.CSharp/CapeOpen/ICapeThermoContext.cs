// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoContext
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides a material object for physical property calculations.
  /// </summary>
  /// <remarks>
  /// Allows a material object to be passed between a PME and the Reactions components it is
  /// using so that the Reactions components can make Physical Property calculation calls.
  /// </remarks>
  [Guid("678c0b5f-0100-11d2-a67d-00105a42887f")]
  [Description("ICapeThermoContext Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeThermoContext
  {
    /// <summary>
    /// Allows the client of a component that implements this interface to pass an
    /// <see cref="T:CapeOpen.ICapeThermoMaterialObject" /> interface to the component, so that
    /// it can access the properties of a material and request property calculations.
    /// </summary>
    /// <remarks>
    /// The SetMaterial method allows a Reactions component to be given the
    /// <see cref="T:CapeOpen.ICapeThermoMaterialObject" /> interface of a Material Object.
    /// This interface gives the component access to the description of the material for
    /// which Property calculations are required. A component can also use the
    /// <see cref="T:CapeOpen.ICapeThermoMaterialObject" /> interface to to get lists of components
    /// and phases.
    /// </remarks>
    /// <param name="materialObject">The interface of an object support the
    /// <see cref="T:CapeOpen.ICapeThermoMaterialObject" /> interface.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(1)]
    [Description("SetMaterial")]
    void SetMaterial(object materialObject);
  }
}
