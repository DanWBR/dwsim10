// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoPropertyRoutine
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <remarks>
  ///  <para>Any Component or object that can calculate a Physical Property must
  ///  implement the ICapeThermoPropertyRoutine interface. Within the scope of this
  ///  specification this means that it must be implemented by Calculation Routine
  ///  components, Property Package components and Material Object implementations that
  ///  will be passed to clients which may need to perform Property Calculations, such
  ///  as Unit Operations [2] and Reaction Package components [3].</para>
  ///  <para>When the ICapeThermoPropertyRoutine interface is implemented by a Material
  ///  Object, it is expected that the actual Calculate, Check and Get functions will be
  ///  delegated either to proprietary methods within a PME or to methods in an
  ///  associated CAPE-OPEN Property Package or Calculation Routine component.</para>
  /// </remarks>
  [Guid("678C0A9F-7D66-11D2-A67D-00105A42887F")]
  [Description("ICapeThermoPropertyRoutine Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeThermoPropertyRoutine
  {
    /// <summary>This method is used to calculate the natural logarithm of the
    /// fugacity coefficients (and optionally their derivatives) in a single Phase
    /// mixture. The values of temperature, pressure and composition are specified in
    /// the argument list and the results are also returned through the argument list.
    /// </summary>
    /// <param name="phaseLabel">Phase label of the Phase for which the properties
    /// are to be calculated. The Phase label must be one of the strings returned by
    /// the GetPhaseList method on the ICapeThermoPhases interface.</param>
    /// <param name="temperature">The temperature (K) for the calculation.</param>
    /// <param name="pressure">The pressure (Pa) for the calculation.</param>
    /// <param name="lnPhiDT">Derivatives of natural logarithm of the fugacity
    /// coefficients w.r.t. temperature (if requested).</param>
    /// <param name="moleNumbers">Number of moles of each Compound in the mixture.</param>
    /// <param name="fFlags">Code indicating whether natural logarithm of the
    /// fugacity coefficients and/or derivatives should be calculated (see notes).
    /// </param>
    /// <param name="lnPhi">Natural logarithm of the fugacity coefficients (if
    /// requested).</param>
    /// <param anem="lnPhiDT">Derivatives of natural logarithm of the fugacity
    /// coefficients w.r.t. temperature (if requested).</param>
    /// <param name="lnPhiDP">Derivatives of natural logarithm of the fugacity
    /// coefficients w.r.t. pressure (if requested).</param>
    /// <param name="lnPhiDn">Derivatives of natural logarithm of the fugacity
    /// coefficients w.r.t. mole numbers (if requested).</param>
    /// <remarks>
    /// <para>This method is provided to allow the natural logarithm of the fugacity
    /// coefficient, which is the most commonly used thermodynamic property, to be
    /// calculated and returned in a highly efficient manner.</para>
    /// <para>The temperature, pressure and composition (mole numbers) for the
    /// calculation are specified by the arguments and are not obtained from the
    /// Material Object by a separate request. Likewise, any quantities calculated are
    /// returned through the arguments and are not stored in the Material Object. The
    /// state of the Material Object is not affected by calling this method. It should
    /// be noted however, that prior to calling CalcAndGetLnPhi a valid Material
    /// Object must have been defined by calling the SetMaterial method on the
    /// ICapeThermoMaterialContext interface of the component that implements the
    /// ICapeThermoPropertyRoutine interface. The compounds in the Material Object
    /// must have been identified and the number of values supplied in the moleNumbers
    /// argument must be equal to the number of Compounds in the Material Object.
    /// </para>
    /// <para>The fugacity coefficient information is returned as the natural
    /// logarithm of the fugacity coefficient. This is because thermodynamic models
    /// naturally provide the natural logarithm of this quantity and also a wider
    /// range of values may be safely returned.</para>
    /// <para>The quantities actually calculated and returned by this method are
    /// controlled by an integer code fFlags. The code is formed by summing
    /// contributions for the property and each derivative required using the
    /// enumerated constants eCapeCalculationCode (defined in the
    /// Thermo version 1.1 IDL) shown in the following table. For example, to
    /// calculate log fugacity coefficients and their T-derivatives the fFlags
    /// argument would be set to CAPE_LOG_FUGACITY_COEFFICIENTS + CAPE_T_DERIVATIVE.</para>
    /// <table border="1">
    /// <tr>
    /// <th>Calculation Type</th>
    /// <th>Enumeration Value</th>
    /// <th>Numerical Value</th>
    /// </tr>
    /// <tr>
    /// <td>no calculation</td>
    /// <td>CAPE_NO_CALCULATION</td>
    /// <td>0</td>
    /// </tr>
    /// <tr>
    /// <td>log fugacity coefficients</td>
    /// <td>CAPE_LOG_FUGACITY_COEFFICIENTS</td>
    /// <td>1</td>
    /// </tr>
    /// <tr>
    /// <td>T-derivative</td>
    /// <td>CAPE_T_DERIVATIVE</td>
    /// <td>2</td>
    /// </tr>
    /// <tr>
    /// <td>P-derivative</td>
    /// <td>CAPE_P_DERIVATIVE</td>
    /// <td>4</td>
    /// </tr>
    /// <tr>
    /// <td>mole number derivatives</td>
    /// <td>CAPE_MOLE_NUMBERS_DERIVATIVES</td>
    /// <td>8</td>
    /// </tr>
    /// </table>
    /// <para>If CalcAndGetLnPhi is called with fFlags set to CAPE_NO_CALCULATION no
    /// property values are returned.</para>
    /// <para>A typical sequence of operations for this method when implemented by a
    /// Property Package component would be:
    /// </para>
    /// <para>
    /// - Check that the phaseLabel specified is valid.
    /// </para>
    /// <para>
    /// - Check that the moleNumbers array contains the number of values expected
    /// (should be consistent with the last call to the SetMaterial method).
    /// </para>
    /// <para>
    /// - Calculate the requested properties/derivatives at the T/P/composition specified in the argument list.
    /// </para>
    /// <para>
    /// - Store values for the properties/derivatives in the corresponding arguments.
    /// </para>
    /// <para>Note that this calculation can be carried out irrespective of whether the Phase actually exists in the Material Object.
    /// </para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported by
    /// the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">Would be raised if the one or more of the
    /// properties requested cannot be returned because the calculation is not
    /// implemented.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. For example, the
    /// ICapeThermoMaterial interface has not been passed via a SetMaterial call prior
    /// to calling this method.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for the
    /// Property Calculation are not valid. Forexample, the composition of the phase is
    /// not defined, the number of Compounds in the Material Object is zero or not
    /// consistent with the moleNumbers argument or any other necessary input information
    /// is not available.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">At least one item in the
    /// requested properties cannot be returned. This could be because the property
    /// cannot be calculated at the specified conditions or for the specified Phase.
    /// If the property calculation is not implemented then ECapeLimitedImpl should
    /// be returned.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">One of the property calculations has
    /// failed. For example if one of the iterative solution procedures in the model
    /// has run out of iterations, or has converged to a wrong solution.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example an unrecognised value, or UNDEFINED for the
    /// phaseLabel argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [Description("method CalcAndGetLnPhi")]
    [DispId(1)]
    void CalcAndGetLnPhi(
      [In] string phaseLabel,
      [In] double temperature,
      [In] double pressure,
      [In] object moleNumbers,
      [In] int fFlags,
      [In, Out] ref object lnPhi,
      [In, Out] ref object lnPhiDT,
      [In, Out] ref object lnPhiDP,
      [In, Out] ref object lnPhiDn);

    /// <summary>CalcSinglePhaseProp is used to calculate properties and property
    /// derivatives of a mixture in a single Phase at the current values of
    /// temperature, pressure and composition set in the Material Object.
    /// CalcSinglePhaseProp does not perform phase Equilibrium Calculations.
    /// </summary>
    /// <param name="props">The list of identifiers for the single-phase properties
    /// or derivatives to be calculated. See sections 7.5.5 and 7.6 for the standard
    /// identifiers.</param>
    /// <param name="phaseLabel">Phase label of the Phase for which the properties
    /// are to be calculated. The Phase label must be one of the strings returned by
    /// the GetPhaseList method on the ICapeThermoPhases interface.</param>
    /// <remarks>
    /// <para>CalcSinglePhaseProp calculates properties, such as enthalpy or viscosity
    /// that are defined for a single Phase. Physical Properties that depend on more
    /// than one Phase, for example surface tension or K-values, are handled by
    /// CalcTwoPhaseProp method.</para>
    /// <para>Components that implement this method must get the input specification
    /// for the calculation (temperature, pressure and composition) from the associated
    /// Material Object and set the results in the Material Object.</para>
    /// <para>Thermodynamic and Physical Properties Components, such as a Property
    /// Package or Property Calculator, must implement the ICapeThermoMaterialContext
    /// interface so that an ICapeThermoMaterial interface can be passed via the
    /// SetMaterial method.</para>
    /// <para>A typical sequence of operations for CalcSinglePhaseProp when implemented
    /// by a Property Package component would be:</para>
    /// <para>- Check that the phaseLabel specified is valid.</para>
    /// <para>- Use the GetTPFraction method (of the Material Object specified in the
    /// last call to the SetMaterial method) to get the temperature, pressure and
    /// composition of the specified Phase.</para>
    /// <para>- Calculate the properties.</para>
    /// <para>- Store values for the properties of the Phase in the Material Object
    /// using the SetSinglePhaseProp method of the ICapeThermoMaterial interface.</para>
    /// <para>CalcSinglePhaseProp will request the input Property values it requires
    /// from the Material Object through GetSinglePhaseProp calls. If a requested
    /// property is not available, the exception raised will be
    /// ECapeThrmPropertyNotAvailable. If this error occurs then the Property Package
    /// can return it to the client, or request a different property. Material Object
    /// implementations must be able to supply property values using the client’s
    /// choice of basis by implementing conversion from one basis to another.</para>
    /// <para>Clients should not assume that Phase fractions and Compound fractions in
    /// a Material Object are normalised. Fraction values may also lie outside the
    /// range 0 to 1. If fractions are not normalised, or are outside the expected
    /// range, it is the responsibility of the Property Package to decide how to deal
    /// with the situation.</para>
    /// <para>It is recommended that properties are requested one at a time in order
    /// to simplify error handling. However, it is recognised that there are cases
    /// where the potential efficiency gains of requesting several properties
    /// simultaneously are more important. One such example might be when a property
    /// and its derivatives are required.</para>
    /// <para>If a client uses multiple properties in a call and one of them fails
    /// then the whole call should be considered to have failed. This implies that no
    /// value should be written back to the Material Object by the Property Package
    /// until it is known that the whole request can be satisfied.</para>
    /// <para>It is likely that a PME might request values of properties for a Phase at
    /// conditions of temperature, pressure and composition where the Phase does not
    /// exist (according to the mathematical/physical models used to represent
    /// properties). The exception ECapeThrmPropertyNotAvailable may be raised or an
    /// extrapolated value may be returned.</para>
    /// <para>It is responsibility of the implementer to decide how to handle this
    /// circumstance.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">Would be raised if the one or more of the
    /// properties requested cannot be returned because the calculation (of the
    /// particular property) is not implemented. This exception should also be raised
    /// (rather than ECapeInvalidArgument) if the props argument is not recognised
    /// because the list of properties in section 7.5.5 is not intended to be
    /// exhaustive and an unrecognised property identifier may be valid. If no
    /// properties at all are supported ECapeNoImpl should be raised (see above).</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. For example, the
    /// ICapeThermoMaterial interface has not been passed via a SetMaterial call prior
    /// to calling this method.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for the
    /// property calculation are not valid. For example, the composition of the phases
    /// is not defined or any other necessary input information is not available.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">At least one item in the
    /// requested properties cannot be returned. This could be because the property
    /// cannot be calculated at the specified conditions or for the specified phase.
    /// If the property calculation is not implemented then ECapeLimitedImpl should be
    /// returned.</exception>
    [DispId(2)]
    [Description("method CalcSinglePhaseProp")]
    void CalcSinglePhaseProp([In] object props, [In] string phaseLabel);

    /// <summary>CalcTwoPhaseProp is used to calculate mixture properties and property
    /// derivatives that depend on two Phases at the current values of temperature,
    /// pressure and composition set in the Material Object. It does not perform
    /// Equilibrium Calculations.</summary>
    /// <param name="props">The list of identifiers for properties to be calculated.
    /// This must be one or more of the supported two-phase properties and derivatives
    /// (as given by the GetTwoPhasePropList method). The standard identifiers for
    /// two-phase properties are given in section 7.5.6 and 7.6.</param>
    /// <param name="phaseLabels">Phase labels of the phases for which the properties
    /// are to be calculated. The phase labels must be two of the strings returned by
    /// the GetPhaseList method on the ICapeThermoPhases interface.</param>
    /// <remarks>
    /// <para>CalcTwoPhaseProp calculates the values of properties such as surface
    /// tension or K-values. Properties that pertain to a single Phase are handled by
    /// the CalcSinglePhaseProp method of the ICapeThermoPropertyRoutine interface.
    /// Components that implement this method must get the input specification for the
    /// calculation (temperature, pressure and composition) from the associated
    /// Material Object and set the results in the Material Object.</para>
    /// <para>Components such as a Property Package or Property Calculator must
    /// implement the ICapeThermoMaterialContext interface so that an
    /// ICapeThermoMaterial interface can be passed via the SetMaterial method.</para>
    /// <para>A typical sequence of operations for CalcTwoPhaseProp when implemented by
    /// a Property Package component would be:</para>
    /// <para>- Check that the phaseLabels specified are valid.</para>
    /// <para>- Use the GetTPFraction method (of the Material Object specified in the
    /// last call to the SetMaterial method) to get the temperature, pressure and
    /// composition of the specified Phases.</para>
    /// <para>- Calculate the properties.</para>
    /// <para>- Store values for the properties in the Material Object using the
    /// SetTwoPhaseProp method of the ICapeThermoMaterial interface.</para>
    /// <para>CalcTwoPhaseProp will request the values it requires from the Material Object
    /// through GetTPFraction or GetSinglePhaseProp calls. If a requested property is
    /// not available, the exception raised will be ECapeThrmPropertyNotAvailable. If
    /// this error occurs, then the Property Package can return it to the client, or
    /// request a different property. Material Object implementations must be able to
    /// supply property values using the client choice of basis by implementing
    /// conversion from one basis to another.</para>
    /// <para>Clients should not assume that Phase fractions and Compound fractions in
    /// a Material Object are normalised. Fraction values may also lie outside the
    /// range 0 to 1. If fractions are not normalised, or are outside the expected
    /// range, it is the responsibility of the Property Package to decide how to deal
    /// with the situation.</para>
    /// <para>It is recommended that properties are requested one at a time in order to
    /// simplify error handling. However, it is recognised that there are cases where
    /// the potential efficiency gains of requesting several properties simultaneously
    /// are more important. One such example might be when a property and its
    /// derivatives are required.</para>
    /// <para>If a client uses multiple properties in a call and one of them fails, then the
    /// whole call should be considered to have failed. This implies that no value
    /// should be written back to the Material Object by the Property Package until
    /// it is known that the whole request can be satisfied.</para>
    /// <para>CalcTwoPhaseProp must be called separately for each combination of Phase
    /// groupings. For example, vapour-liquid K-values have to be calculated in a
    /// separate call from liquid-liquid K-values.</para>
    /// <para>Two-phase properties may not be meaningful unless the temperatures and
    /// pressures of all Phases are identical. It is the responsibility of the Property
    /// Package to check such conditions and to raise an exception if appropriate.</para>
    /// <para>It is likely that a PME might request values of properties for Phases at
    /// conditions of temperature, pressure and composition where one or both of the
    /// Phases do not exist (according to the mathematical/physical models used to
    /// represent properties). The exception ECapeThrmPropertyNotAvailable may be
    /// raised or an extrapolated value may be returned. It is responsibility of the
    /// implementer to decide how to handle this circumstance.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">Would be raised if the one or more of the
    /// properties requested cannot be returned because the calculation (of the
    /// particular property) is not implemented. This exception should also be raised
    /// (rather than ECapeInvalidArgument) if the props argument is not recognised
    /// because the list of properties in section 7.5.6 is not intended to be
    /// exhaustive and an unrecognised property identifier may be valid. If no
    /// properties at all are supported ECapeNoImpl should be raised (see above).</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. For example, the
    /// ICapeThermoMaterial interface has not been passed via a SetMaterial call
    /// prior to calling this method.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for the
    /// property calculation are not valid. For example, the composition of one of the
    /// Phases is not defined, or any other necessary input information is not
    /// available.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">At least one item in the
    /// requested properties cannot be returned. This could be because the property
    /// cannot be calculated at the specified conditions or for the specified Phase.
    /// If the property calculation is not implemented then ECapeLimitedImpl should be
    /// returned.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">One of the property calculations has
    /// failed. For example if one of the iterative solution procedures in the model
    /// has run out of iterations, or has converged to a wrong solution.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example an unrecognised value or UNDEFINED for the
    /// phaseLabels argument or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [Description("method CalcTwoPhaseProp")]
    [DispId(3)]
    void CalcTwoPhaseProp([In] object props, [In] object phaseLabels);

    /// <summary>Checks whether it is possible to calculate a property with the
    /// CalcSinglePhaseProp method for a given Phase.</summary>
    /// <param name="property">The identifier of the property to check. To be valid
    /// this must be one of the supported single-phase properties or derivatives (as
    /// given by the GetSinglePhasePropList method).</param>
    /// <param name="phaseLabel">The Phase label for the calculation check. This must
    /// be one of the labels returned by the GetPhaseList method on the
    /// ICapeThermoPhases interface.</param>
    /// <returns> A boolean set to True if the combination of property and phaseLabel
    /// is supported or False if not supported.</returns>
    /// <remarks>
    /// <para>The result of the check should only depend on the capabilities and
    /// configuration (Compounds and Phases present) of the component that implements
    /// the ICapeThermoPropertyRoutine interface (eg. a Property Package). It should
    /// not depend on whether a Material Object has been set nor on the state
    /// (temperature, pressure, composition etc.), or configuration of a Material
    /// Object that might be set.</para>
    /// <para>It is expected that the PME, or other client, will use this method to
    /// check whether the properties it requires are supported by the Property Package
    /// when the package is imported. If any essential properties are not available,
    /// the import process should be aborted.</para>
    /// <para>If either the property or the phaseLabel arguments are not recognised by
    /// the component that implements the ICapeThermoPropertyRoutine interface this
    /// method should return False.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation CheckSinglePhasePropSpec is
    /// “not” implemented even if this method can be called for reasons of
    /// compatibility with the CAPE-OPEN standards. That is to say that the operation
    /// exists, but it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. The ICapeThermoMaterial
    /// interface has not been passed via a SetMaterial call prior to calling this
    /// method.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for the
    /// property calculation are not valid. For example, if a prior call to the
    /// SetMaterial method of the ICapeThermoMaterialContext interface has failed to
    /// provide a valid Material Object.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">One or more of the input arguments is
    /// not valid: for example, UNDEFINED value for the property argument or the
    /// phaseLabel argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the CheckSinglePhasePropSpec operation, are not suitable.</exception>
    [Description("method CheckSinglePhasePropSpec")]
    [DispId(4)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool CheckSinglePhasePropSpec([In] string property, [In] string phaseLabel);

    /// <summary>Checks whether it is possible to calculate a property with the
    /// CalcTwoPhaseProp method for a given set of Phases.</summary>
    /// <param name="property">The identifier of the property to check. To be valid
    /// this must be one of the supported two-phase properties (including derivatives),
    /// as given by the GetTwoPhasePropList method.</param>
    /// <param name="phaseLabels">Phase labels of the Phases for which the properties
    /// are to be calculated. The Phase labels must be two of the identifiers returned
    /// by the GetPhaseList method on the ICapeThermoPhases interface.</param>
    /// <returns> A boolean Set to True if the combination of property and
    /// phaseLabels is supported, or False if not supported.</returns>
    /// <remarks>
    /// <para>The result of the check should only depend on the capabilities and
    /// configuration (Compounds and Phases present) of the component that implements
    /// the ICapeThermoPropertyRoutine interface (eg. a Property Package). It should
    /// not depend on whether a Material Object has been set nor on the state
    /// (temperature, pressure, composition etc.), or configuration of a Material
    /// Object that might be set.</para>
    /// <para>It is expected that the PME, or other client, will use this method to
    /// check whether the properties it requires are supported by the Property Package
    /// when the Property Package is imported. If any essential properties are not
    /// available, the import process should be aborted.</para>
    /// <para>If either the property argument or the values in the phaseLabels
    /// arguments are not recognised by the component that implements the
    /// ICapeThermoPropertyRoutine interface this method should return False.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation CheckTwoPhasePropSpec is “not”
    /// implemented even if this method can be called for reasons of compatibility with
    /// the CAPE-OPEN standards. That is to say that the operation exists, but it is
    /// not supported by the current implementation. This may be the case if no
    /// two-phase property is supported.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. The ICapeThermoMaterial
    /// interface has not been passed via a SetMaterial call prior to calling this
    /// method.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for the
    /// property calculation are not valid. For example, if a prior call to the
    /// SetMaterial method of the ICapeThermoMaterialContext interface has failed to
    /// provide a valid Material Object.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">One or more of the input arguments is
    /// not valid. For example, UNDEFINED value for the property argument or the
    /// phaseLabels argument or number of elements in phaseLabels array not equal to
    /// two.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the CheckTwoPhasePropSpec operation, are not suitable.</exception>
    [DispId(5)]
    [Description("method CheckTwoPhasePropSpec")]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool CheckTwoPhasePropSpec([In] string property, [In] object phaseLabels);

    /// <summary>Returns the list of supported non-constant single-phase Physical
    /// Properties.</summary>
    /// <returns>List of all supported non-constant single-phase property identifiers.
    /// The standard single-phase property identifiers are listed in section 7.5.5.
    /// </returns>
    /// <remarks>
    /// <para>A non-constant property depends on the state of the Material Object. </para>
    /// <para>Single-phase properties, e.g. enthalpy, only depend on the state of one
    /// phase. GetSinglePhasePropList must return all the single-phase properties that
    /// can be calculated by CalcSinglePhaseProp. If derivatives can be calculated
    /// these must also be returned.</para>
    /// <para>If no single-phase properties are supported this method should return
    /// UNDEFINED.</para>
    /// <para>To get the list of supported two-phase properties, use
    /// GetTwoPhasePropList.</para>
    /// <para>A component that implements this method may return non-constant
    /// single-phase property identifiers which do not belong to the list defined in
    /// section 7.5.5. However, these proprietary identifiers may not be understood by
    /// most of the clients of this component.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported by
    /// the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetSinglePhasePropList operation, are not suitable.</exception>
    [Description("method GetSinglePhasePropList")]
    [DispId(6)]
    object GetSinglePhasePropList();

    /// <summary>Returns the list of supported non-constant two-phase properties.</summary>
    /// <returns>List of all supported non-constant two-phase property identifiers.
    /// The standard two-phase property identifiers are listed in section 7.5.6.</returns>
    /// <remarks>
    /// <para>A non-constant property depends on the state of the Material Object.
    /// Two-phase properties are those that depend on more than one co-existing phase,
    /// e.g. K-values.</para>
    /// <para>GetTwoPhasePropList must return all the properties that can be calculated
    /// by CalcTwoPhaseProp. If derivatives can be calculated, these must also be
    /// returned.</para>
    /// <para>If no two-phase properties are supported this method should return
    /// UNDEFINED.</para>
    /// <para>To check whether a property can be evaluated for a particular set of
    /// phase labels use the CheckTwoPhasePropSpec method.</para>
    /// <para>A component that implements this method may return non-constant
    /// two-phase property identifiers which do not belong to the list defined in
    /// section 7.5.6. However, these proprietary identifiers may not be understood by
    /// most of the clients of this component.</para>
    /// <para>To get the list of supported single-phase properties, use
    /// GetSinglePhasePropList.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetTwoPhasePropList operation, are not suitable.</exception>
    [DispId(7)]
    [Description("method GetTwoPhasePropList")]
    object GetTwoPhasePropList();
  }
}
