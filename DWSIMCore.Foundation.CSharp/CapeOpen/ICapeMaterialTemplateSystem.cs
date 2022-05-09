// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeMaterialTemplateSystem
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Creates a new thermo material template of the specified type.
  /// </summary>
  /// <remarks>
  /// When a Unit Operation needs to obtain thermodynamic calculations, it will
  /// typically perform them on the material objects attached to the Unit ports. However,
  /// in some cases, like distillation columns, there may be the need to utilise a different
  /// Property Package. Even the user could be requested to choose which thermodynamic
  /// model to must be used. All the mechanisms for accessing CAPE-OPEN Property Packages
  /// are already in the COSE´s, as part of the functionality necessary for making use of
  /// CAPE-OPEN Property Packages. Therefore, instead of each PMC implementing support for
  /// performing this selection and creation of thermo engine, delegating that
  /// responsibility to the COSE will result in thinner and easier to code Unit Operation
  /// Components. If configuration of Material Templates is in the PME side, the only
  /// additional functionality the Unit Operation would require is that for accessing the
  /// list of already configured Material Templates, and picking one of them.
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0a9e-0100-11d2-a67d-00105a42887f")]
  [Description("ICapeMaterialTemplateSystem Interface")]
  [ComImport]
  public interface ICapeMaterialTemplateSystem
  {
    /// <summary>
    /// Creates a new thermo material template of the specified type.
    /// </summary>
    /// <remarks>
    /// When a Unit Operation needs to obtain thermodynamic calculations, it will
    /// typically perform them on the material objects attached to the Unit ports. However,
    /// in some cases, like distillation columns, there may be the need to utilise a different
    /// Property Package. Even the user could be requested to choose which thermodynamic
    /// model to must be used. All the mechanisms for accessing CAPE-OPEN Property Packages
    /// are already in the COSE´s, as part of the functionality necessary for making use of
    /// CAPE-OPEN Property Packages. Therefore, instead of each PMC implementing support for
    /// performing this selection and creation of thermo engine, delegating that
    /// responsibility to the COSE will result in thinner and easier to code Unit Operation
    /// Components. If configuration of Material Templates is in the PME side, the only
    /// additional functionality the Unit Operation would require is that for accessing the
    /// list of already configured Material Templates, and picking one of them.
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property MaterialTemplates")]
    [DispId(1)]
    object MaterialTemplates { get; }

    /// <summary>
    /// Creates a new thermo material template of the specified type.
    /// </summary>
    /// <remarks>
    /// When a Unit Operation needs to obtain thermodynamic calculations, it will
    /// typically perform them on the material objects attached to the Unit ports. However,
    /// in some cases, like distillation columns, there may be the need to utilise a different
    /// Property Package. Even the user could be requested to choose which thermodynamic
    /// model to must be used. All the mechanisms for accessing CAPE-OPEN Property Packages
    /// are already in the COSE´s, as part of the functionality necessary for making use of
    /// CAPE-OPEN Property Packages. Therefore, instead of each PMC implementing support for
    /// performing this selection and creation of thermo engine, delegating that
    /// responsibility to the COSE will result in thinner and easier to code Unit Operation
    /// Components. If configuration of Material Templates is in the PME side, the only
    /// additional functionality the Unit Operation would require is that for accessing the
    /// list of already configured Material Templates, and picking one of them.
    /// </remarks>
    /// <returns>
    /// Returns StringArray of material template names supported by the COSE. This can include:
    /// - CAPE-OPEN standalone property packages
    /// - CAPE-OPEN property packages that depend on a Property System
    /// - Property packages that are native to the COSE.
    /// </returns>
    /// <param name="materialTemplateName">TThe name of the material template to be resolved (which
    /// must be included in the list returned by MaterialTemplates)</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method CreateMaterialTemplate")]
    [DispId(2)]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object CreateMaterialTemplate(string materialTemplateName);
  }
}
