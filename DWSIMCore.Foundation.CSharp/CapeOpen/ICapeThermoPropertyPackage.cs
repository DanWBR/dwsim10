// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoPropertyPackage
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Interface implemented by a CAPE-OPEN version 1.0 Physical Property Package.
  /// </summary>
  /// <remarks>
  /// <para>A Simple Properties Package (SPP) is a complete, consistent, reusable, ready-to-use collection of
  /// methods, chemical components and model parameters for calculating any of a set of known properties for
  /// the phases of a multiphase system. It includes all the pure component methods and data, together with
  /// the relevant mixing rules and interaction parameters. A package normally covers only a small subset of
  /// the chemical components and methods accessible through a Properties System. It is thus established by
  /// selecting methods etc from within a larger system, possibly adding to or replacing these methods by
  /// third party components.
  /// </para>
  /// <para>These additional methods will normally be CAPE-OPEN compliant methods which may have been specially
  /// written, or may come from another properties system. (They can only come from another system where that
  /// system provides them as CAPE-OPEN compliant components). A Properties Package may be a Simple
  /// Properties Package, or at a vendors discretion, made up from Option Sets (see definition of Option Set).
  /// </para>
  /// </remarks>
  [Description("ICapeThermoPropertyPackage Interface")]
  [ComVisible(false)]
  [Guid("678c0996-7d66-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeThermoPropertyPackage
  {
    /// <summary>Get the phase list</summary>
    /// <remarks>
    /// Provides the list of the supported phases. When supported, the Overall phase
    /// and multiphase identifiers must be returned by this method.
    /// </remarks>
    /// <returns>
    /// The list of phases supported by the property package.
    /// A System.Object containing a String array marshalled from a COM Object.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(1)]
    [Description("method GetPhaseList")]
    object GetPhaseList();

    /// <summary>Get the component list</summary>
    /// <remarks>
    /// <para>Returns the list of components of a given property package.</para>
    /// <para>In order to identify the components of a Property Package, the
    /// Executive will use the ‘casno’ argument instead of the compIds. The reason is
    /// that different COSEs may give different names to the same chemical compounds,
    /// whereas CAS Numbers are universal. Nevertheless, GetProp/SetProp... will still
    /// require their compIds argument to have the usual contents ("hydrogen",
    /// "methane",...). Be aware that some simulators may have a limitation on the
    /// length of the names for pure components. Hence, it is recommended that each
    /// identifier returned by the compIds argument should not contain more than 8
    /// characters. See notes on Description of component constants for more
    /// information.</para>
    /// <para>If the package does not return a value for the ‘casno’ argument, or its
    /// value is not recognised by the Executive, then the compIds will be interpreted
    /// as the component’s English name: such as "benzene", "water",... Obviously, it
    /// is recommended to provide a value for the casno argument.</para>
    /// <para>The same information can also be extracted using the
    /// ICapeThermoPropertyPackage GetComponentConstant method, using the
    /// casRegistryNumber property identifier.</para>
    /// </remarks>
    /// <param name="compIds">
    /// Reference value to the list of component IDs.
    /// A reference to a System.Object containing a String array marshalled from a
    /// COM Object.
    /// </param>
    /// <param name="formulae">
    /// List of component formulae.
    /// A reference to a System.Object containing a String array marshalled from a
    /// COM Object.
    /// </param>
    /// <param name="names">
    /// List of component names.
    /// A reference to a System.Object containing a String array marshalled from a
    /// COM Object.
    /// </param>
    /// <param name="boilTemps">
    /// List of boiling point temperatures.
    /// A reference to a System.Object containing a double array marshalled from a
    /// COM Object.
    /// </param>
    /// <param name="molWt">
    /// List of molecular weight.
    /// A reference to a System.Object containing a double array marshalled from a
    /// COM Object.
    /// </param>
    /// <param name="casNo">
    /// List of CAS number.
    /// A reference to a System.Object containing a String array marshalled from a
    /// COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("method GetComponentList")]
    void GetComponentList(
      ref object compIds,
      ref object formulae,
      ref object names,
      ref object boilTemps,
      ref object molWt,
      ref object casNo);

    /// <summary>Get some universal constant(s)</summary>
    /// <remarks>Returns the values of the Universal Constants.</remarks>
    /// <param name="materialObject">The Material object.</param>
    /// <param name="props">
    /// List of requested universal constants.
    /// A reference to a System.Object containing a String array marshalled as a
    /// COM Object.
    /// </param>
    /// <returns>
    /// Values of universal constants.
    /// A reference to a System.Object containing an System.Object array marshalled
    /// from a COM Object.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method GetUniversalConstant")]
    [DispId(3)]
    object GetUniversalConstant([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);

    /// <summary>Get some pure component constant(s)</summary>
    /// <remarks>
    /// Returns the values of the Constant properties of the components contained in
    /// the passed Material Object.
    /// </remarks>
    /// <param name="materialObject">The Material object.</param>
    /// <param name="props">
    /// The list of properties.
    /// A reference to a System.Object containing a String array marshalled as a
    /// COM Object.
    /// </param>
    /// <returns>
    /// Component Constant values. See description of return value of the
    /// <see cref="M:CapeOpen.ICapeThermoMaterialObject.GetComponentConstant(System.Object,System.Object)" /> method.
    /// A reference to a System.Object containing an System.Object array marshalled
    /// from a COM Object.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(4)]
    [Description("method GetComponentConstant")]
    object GetComponentConstant([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);

    /// <summary>Calculate some proeprties.</summary>
    /// <remarks>
    /// This method is responsible for doing all calculations and is implemented by
    /// the associated thermo system. This method is further defined in the
    /// descriptions of the CAPE-OPEN Calling Pattern and the User Guide
    /// Section.
    /// </remarks>
    /// <param name="materialObject">
    /// The MaterialObject for the Calculation.
    /// </param>
    /// <param name="props">
    /// The List of Properties to be calculated.
    /// A reference to a System.Object containing a String array marshalled as a
    /// COM Object.
    /// </param>
    /// <param name="phases">
    /// List of phases for which the properties are to be calculated.
    /// A reference to a System.Object containing a String array marshalled as a
    /// COM Object.
    /// </param>
    /// <param name="calcType">
    /// Type of calculation: Mixture Property or Pure Component Property. For partial
    /// property, such as fugacity coefficients of components in a mixture, use
    /// “Mixture” CalcType. For pure component fugacity coefficients, use “Pure”
    /// CalcType.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method CalcProp")]
    [DispId(5)]
    void CalcProp([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props, object phases, string calcType);

    /// <summary>Calculate some equilibrium values</summary>
    /// <remarks>
    /// Method responsible for calculating/delegating flash calculation requests. It
    /// must set the amounts, compositions, temperature and pressure for all phases
    /// present at equilibrium, as well as the temperature and pressure for the overall
    /// mixture, if not set as part of the calculation specifications. See CalcProp
    /// and CalcEquilibrium for more information.
    /// </remarks>
    /// <param name="materialObject">
    /// The MaterialObject for the Calculation.
    /// </param>
    /// <param name="props">
    /// Properties to be calculated at equilibrium. emptyObject for no properties.
    /// If a list, then the property values should be set for each phase present at
    /// equilibrium.
    /// A reference to a System.Object containing a String array marshalled as a
    /// COM Object.
    /// </param>
    /// <param name="flashType">Flash calculation type.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">ECapeSolvingError</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">ECapeOutOfBounds</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [Description("method CalcEquilibrium")]
    [DispId(6)]
    void CalcEquilibrium([MarshalAs(UnmanagedType.IDispatch)] object materialObject, string flashType, object props);

    /// <summary>Check a property is valid</summary>
    /// <remarks>Check to see if properties can be calculated.</remarks>
    /// <returns>
    /// The array of booleans for each property.
    /// A System.Object containing an System.Boolean (marshalled as VT_BOOL) array
    /// marshalled as a COM Object.
    /// </returns>
    /// <param name="materialObject">
    /// The MaterialObject for the Calculation.
    /// </param>
    /// <param name="props">
    /// List of Properties to check.
    /// A System.Object containing a String array marshalled as a COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(7)]
    [Description("method PropCheck")]
    object PropCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);

    /// <summary>Check the validity of the given properties</summary>
    /// <remarks>Checks the validity of the calculation.</remarks>
    /// <returns>
    /// The properties for which reliability is checked.
    /// A System.Object containing an System.Boolean (marshalled as VT_BOOL) array
    /// marshalled as a COM Object.
    /// </returns>
    /// <param name="materialObject">
    /// The MaterialObject for the Calculation.
    /// </param>
    /// <param name="props">
    /// List of Properties to check.
    /// A System.Object containing a CapeArrayThermoReliability marshalled as a
    /// COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method ValidityCheck")]
    [DispId(8)]
    object ValidityCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);

    /// <summary>Get the list of properties</summary>
    /// <remarks>
    /// <para>Returns list of Thermo System supported properties. The properties TEMPERATURE,
    /// PRESSURE, FRACTION, FLOW, PHASEFRACTION, TOTALFLOW cannot be returned by
    /// GetPropList, since all components must support them. Although the property
    /// identifier of derivative properties is formed from the identifier of another
    /// property, the GetPropList method will return the identifiers of all supported
    /// derivative and non-derivative properties. For instance, a Property Package
    /// could return the following list:
    /// </para>
    /// <para>
    /// enthalpy, enthalpy.Dtemperature, entropy, entropy.Dpressure.
    /// </para>
    /// </remarks>
    /// <returns>
    /// String list of all supported Properties.
    /// A System.Object containing an System.String array marshalled as a COM Object.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(9)]
    [Description("method GetPropList")]
    object GetPropList();
  }
}
