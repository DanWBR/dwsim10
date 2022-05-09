// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoEquilibriumServer
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// ICapeThermoCalculationRoutine interface is the mechanism for adding foreign
  /// calculation routines to a physical property package.
  /// </summary>
  [ComVisible(false)]
  [Guid("678c0997-7d66-11d2-a67d-00105a42887f")]
  [Description("ICapeThermoEquilibriumServer Interface")]
  [ComImport]
  public interface ICapeThermoEquilibriumServer
  {
    /// <summary>Calculate some equilibrium values</summary>
    /// <remarks>
    /// Calculates the equilibrium properties requested. It must set the amounts, compositions, temperature
    /// and pressure for all phases present at equilibrium, as well as the temperature and pressure for the
    /// overall mixture, if not set as part of the calculation specifications. See CalcProp and
    /// CalcEquilibrium for more information.
    /// </remarks>
    /// <param name="materialObject">The material object of the calculation.</param>
    /// <param name="flashType">Flash calculation type.</param>
    /// <param name="props">Properties to be calculated at equilibrium. emptyVariant for no properties.
    /// If a list, then the property values should be set for each phase present at equilibrium.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, for example UNDEFINED for property.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">Error raised to indicate that a precondition for this operation
    /// has not been performed.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">An error occurred while calculating equilibrium conditions.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">Indicates that one of the values used in this calculation are
    /// outside their acceptable limits.</exception>
    [Description("method CalcEquilibrium")]
    [DispId(1)]
    void CalcEquilibrium([MarshalAs(UnmanagedType.IDispatch)] object materialObject, string flashType, object props);

    /// <summary>Checks that a property is valid.</summary>
    /// <remarks>
    /// Checks to see if a given type of flash calculations can be performed and whether the properties can
    /// be calculated after the flash calculation.
    /// </remarks>
    /// <param name="valid">The array of booleans for flash and property. First element is reserved for
    /// flashType.</param>
    /// <param name="materialObject">The material object of the calculation.</param>
    /// <param name="flashType">Flash calculation type.</param>
    /// <param name="props">Properties to be calculated at equilibrium. emptyVariant for no properties.
    /// If a list, then the property values should be set for each phase present at equilibrium.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, for example UNDEFINED for property.</exception>
    [Description("method PropCheck")]
    [DispId(2)]
    void PropCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, string flashType, object props, ref object valid);

    /// <summary>Checks the validity of the given properties.</summary>
    /// <remarks>Checks the reliability of the calculation.</remarks>
    /// <param name="relList">The properties for which reliability is checked. First element reserved for
    /// reliability of flash calculations.</param>
    /// <param name="materialObject">The material object of the calculation.</param>
    /// <param name="props">Properties to be calculated at equilibrium. emptyVariant for no properties.
    /// If a list, then the property values should be set for each phase present at equilibrium.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, for example UNDEFINED for property.</exception>
    [DispId(3)]
    [Description("method ValidityCheck")]
    void ValidityCheck([MarshalAs(UnmanagedType.IDispatch)] object materialObject, object props, ref object relList);

    /// <summary>Gets the list of properties.</summary>
    /// <remarks>
    /// Returns the flash types, properties, phases, and calculation types that are supported by a given
    /// Equilibrium Server Routine.
    /// </remarks>
    /// <param name="flashType">Type of flash calculations supported.</param>
    /// <param name="props">List of supported properties.</param>
    /// <param name="phases">List of supported phases.</param>
    /// <param name="calcType">List of supported calculation types. (Pure &amp; Mixture)</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, for example UNDEFINED for property.</exception>
    [DispId(4)]
    [Description("method PropList")]
    void PropList(ref object flashType, ref object props, ref object phases, ref object calcType);
  }
}
