// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoMaterialContext
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface should be implemented by all Thermodynamic and Physical
  /// Properties components that need an ICapeThermoMaterial interface in order to set
  /// and get a Material’s property values.
  /// </summary>
  [Guid("678C0A9C-7D66-11D2-A67D-00105A42887F")]
  [Description("ICapeThermoMaterialContext Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeThermoMaterialContext
  {
    /// <summary>Allows the client of a component that implements this interface to
    /// pass an ICapeThermoMaterial interface to the component, so that it can
    /// access the properties of a Material.</summary>
    /// <remarks><para>	The SetMaterial method allows a Thermodynamic and
    /// Physical Properties component, such as a Property Package, to be given the
    /// ICapeThermoMaterial interface of a Material Object. This interface gives the
    /// component access to the description of the Material for which Property
    /// Calculations or Equilibrium Calculations are required. The component can
    /// access property values directly using this interface. A client can also use
    /// the ICapeThermoMaterial interface to query a Material Object for its
    /// ICapeThermoCompounds and ICapeThermoPhases interfaces, which provide access
    /// to Compound and Phase information, respectively.</para>
    /// <para>It is envisaged that the SetMaterial method will be used to check that
    /// the Material Interface supplied is valid and useable. For example, a
    /// Property Package may check that there are some Compounds in a Material
    /// Object and that those Compounds can be identified by the Property Package.
    /// In addition a Property Package may perform any initialisation that depends
    /// on the configuration of a Material Object. A Property Calculator component
    /// might typically use this method to query the Material Object for any required
    /// information concerning the Compounds.</para>
    /// <para>Calling the UnsetMaterial method of the ICapeThermoMaterialContext
    /// interface has the effect of removing the interface set by the SetMaterial
    /// method.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The input argument is not a valid
    /// CapeInterface.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation"><para>The pre-requisites for the
    /// property calculation are not valid. For example:</para>
    /// <para>• There are no Compounds in the object that implements the
    /// ICapeThermoMaterial interface.</para>
    /// <para>• The Compounds cannot be identified by the client (e.g. a Property
    /// Package). This case is a possibility if the way a Material Object has been
    /// configured by a PME is not consistent with the Property Package being used.</para>
    /// </exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    [DispId(1)]
    [Description("method SetMaterial")]
    void SetMaterial([MarshalAs(UnmanagedType.IDispatch), In] object material);

    /// <summary>Removes any previously set Material interface.</summary>
    /// <remarks><para>The UnsetMaterial method removes any Material interface previously
    /// set by a call to the SetMaterial method of the ICapeThermoMaterialContext
    /// interface. This means that any methods of other interfaces that depend on having
    /// a valid Material Interface, for example methods of the ICapeThermoPropertyRoutine
    /// or ICapeThermoEquilibriumRoutine interfaces, should behave in the same way as if
    /// the SetMaterial method had never been called.</para>
    /// <para>If UnsetMaterial is called before a call to SetMaterial it has no effect
    /// and no exception should be raised.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the current
    /// implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    [Description("method UnsetMaterial")]
    [DispId(2)]
    void UnsetMaterial();
  }
}
