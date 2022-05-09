// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoCalculationRoutine
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// ICapeThermoCalculationRoutine is a mechanism for adding foreign calculation
  /// routines to a physical property package.
  /// </summary>
  [Guid("678c0991-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [Description("ICapeThermoCalculationRoutine Interface")]
  [ComImport]
  public interface ICapeThermoCalculationRoutine
  {
    /// <summary>Calculate some properties</summary>
    /// <remarks>
    /// This method is responsible for doing all calculations on behalf of the
    /// calculation routine component. This method is further defined in the
    /// descriptions of the CAPE-OPEN Calling Pattern and the User Guide Section.
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
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">ECapeBadInvOrder</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">ECapeSolvingError</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">ECapeOutOfBounds</exception>
    void CalcProp([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props, object phases, string calcType);

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
    object PropCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);

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
    [DispId(3)]
    [Description("method GetPropList")]
    object GetPropList();

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
    [DispId(4)]
    [Description("method ValidityCheck")]
    object ValidityCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props);
  }
}
