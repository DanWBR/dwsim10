// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoMaterial
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// A Material Object is a container of information that describes a Material stream.
  /// Calculations of thermophysical and thermodynamic properties are performed by a
  /// Property Package using information stored in a Material Object. Results of such
  /// calculations may be stored in the Material Object for further usage. The
  /// ICapeThermoMaterial interface provides the methods to gather information and
  /// perform checks in preparation for a calculation, to request a calculation and
  /// to retrieve results and information stored in the Material Object.
  /// </summary>
  [Description("ICapeThermoMaterial Interface")]
  [Guid("678C0A9B-7D66-11D2-A67D-00105A42887F")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeThermoMaterial
  {
    /// <summary>Remove all stored Physical Property values.</summary>
    /// <remarks>
    /// <para>
    /// ClearAllProps removes all stored Physical Properties that have been set
    /// using the SetSinglePhaseProp, SetTwoPhaseProp or SetOverallProp methods.
    /// This means that any subsequent call to retrieve Physical Properties will
    /// result in an exception until new values have been stored using one of the
    /// Set methods. ClearAllProps does not remove the configuration information
    /// for a Material, i.e. the list of Compounds and Phases.
    /// </para>
    /// <para>
    /// Using the ClearAllProps method results in a Material Object that is in
    /// the same state as when it was first created. It is an alternative to using
    /// the CreateMaterial method but it is expected to have a smaller overhead in
    /// operating system resources.
    /// </para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method ClearAllProps")]
    [DispId(1)]
    void ClearAllProps();

    /// <summary>
    /// Copies all the stored non-constant Physical Properties (which have been set
    /// using the SetSinglePhaseProp, SetTwoPhaseProp or SetOverallProp) from the
    /// source Material Object to the current instance of the Material Object.
    /// </summary>
    /// <remarks>
    /// <para>Before using this method, the Material Object must have been configured
    /// with the same exact list of Compounds and Phases as the source one. Otherwise,
    /// calling the method will raise an exception. There are two ways to perform the
    /// configuration: through the PME proprietary mechanisms and with
    /// CreateMaterial. Calling CreateMaterial on a Material Object S and
    /// subsequently calling CopyFromMaterial(S) on the newly created Material
    /// Object N is equivalent to the deprecated method ICapeMaterialObject.Duplicate.
    /// </para>
    /// <para>The method is intended to be used by a client, for example a Unit
    /// Operation that needs a Material Object to have the same state as one of the
    /// Material Objects it has been connected to. One example is the representation
    /// of an internal stream in a distillation column.</para>
    /// </remarks>
    /// <param name="source">
    /// Source Material Object from which stored properties will be copied.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even
    /// if this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites for copying
    /// the non-constant Physical Properties of the Material Object are not valid.
    /// The necessary initialisation, such as configuring the current Material with
    /// the same Compounds and Phases as the source, has not been performed or has
    /// failed.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">The physical resources necessary to
    /// copy the non-constant Physical Properties are out of limits.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoMemory">The physical memory necessary to copy the
    /// non-constant Physical Properties is out of limit.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [DispId(2)]
    [Description("method CopyFromMaterial")]
    void CopyFromMaterial([MarshalAs(UnmanagedType.IDispatch)] ref object source);

    /// <summary>
    /// Creates a Material Object with the same configuration as the current
    /// Material Object.
    /// </summary>
    /// <remarks>
    /// The Material Object created does not contain any non-constant Physical
    /// Property value but has the same configuration (Compounds and Phases) as
    /// the current Material Object. These Physical Property values must be set
    /// using SetSinglePhaseProp, SetTwoPhaseProp or SetOverallProp. Any attempt to
    /// retrieve Physical Property values before they have been set will result in
    /// an exception.
    /// </remarks>
    /// <returns>The interface for the Material Object.</returns>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The physical resources
    /// necessary to the creation of the Material Object are out of limits.
    /// </exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">The operation is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation</exception>
    /// <exception cref="T:CapeOpen.ECapeNoMemory">The physical memory necessary to the
    /// creation of the Material Object is out of limit.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method CreateMaterial")]
    [DispId(3)]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object CreateMaterial();

    /// <summary>
    /// Retrieves non-constant Physical Property values for the overall mixture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Physical Property values returned by GetOverallProp refer to the overall
    /// mixture. These values are set by calling the SetOverallProp method. Overall
    /// mixture Physical Properties are not calculated by components that implement
    /// the ICapeThermoMaterial interface. The property values are only used as
    /// input specifications for the CalcEquilibrium method of a component that
    /// implements the ICapeThermoEquilibriumRoutine interface.
    /// </para>
    /// <para>It is expected that this method will normally be able to provide
    /// Physical Property values on any basis, i.e. it should be able to convert
    /// values from the basis on which they are stored to the basis requested. This
    /// operation will not always be possible. For example, if the molecular weight
    /// is not known for one or more Compounds, it is not possible to convert
    /// between a mass basis and a molar basis.
    /// </para>
    /// <para>Although the result of some calls to GetOverallProp will be a single
    /// value, the return type is CapeArrayDouble and the method must always return
    /// an array even if it contains only a single element.</para>
    /// </remarks>
    /// <param name="results"> A double array containing the results vector of
    /// Physical Property value(s) in SI units.</param>
    /// <param name="property">A String identifier of the Physical Property for
    /// which values are requested. This must be one of the single-phase Physical
    /// Properties or derivatives that can be stored for the overall mixture. The
    /// standard identifiers are listed in sections 7.5.5 and 7.6.
    /// </param>
    /// <param name="basis">A String indicating the basis of the results. Valid
    /// settings are: “Mass” for Physical Properties per unit mass or “Mole” for
    /// molar properties. Use UNDEFINED as a place holder for a Physical Property
    /// for which basis does not apply. See section 7.5.5 for details.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetOverallProp is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">The Physical Property
    /// required is not available from the Material Object, possibly for the basis
    /// requested. This exception is raised when a Physical Property value has not
    /// been set following a call to the CreateMaterial or ClearAllProps methods.
    /// </exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, for example UNDEFINED for property.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The necessary initialisation has not been performed or has failed.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method GetOverallProp")]
    [DispId(4)]
    void GetOverallProp([In] string property, [In] string basis, [In, Out] ref object results);

    /// <summary>
    /// Retrieves temperature, pressure and composition for the overall mixture.
    /// </summary>
    /// <remarks>
    ///  <para>
    /// This method is provided to make it easier for developers to make efficient
    ///  use of the CAPEOPEN interfaces. It returns the most frequently requested
    ///  information from a Material Object in a single call.
    ///  </para>
    ///  <para>
    ///  There is no choice of basis in this method. The composition is always
    ///  returned as mole fractions.
    ///  </para>
    ///  </remarks>
    /// <param name="temperature">A reference to a double Temperature (in K)</param>
    /// <param name="pressure">A reference to a double Pressure (in Pa)</param>
    /// <param name="composition">A reference to an array of doubles containing
    /// the  Composition (mole fractions)</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetOverallProp is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">The Physical Property
    /// required is not available from the Material Object, possibly for the basis
    /// requested. This exception is raised when a Physical Property value has not
    /// been set following a call to the CreateMaterial or ClearAllProps methods.
    /// </exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The necessary initialisation has not been performed or has failed.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [DispId(5)]
    [Description("method GetOverallTPFraction")]
    void GetOverallTPFraction([In, Out] ref double temperature, [In, Out] ref double pressure, [In, Out] ref object composition);

    /// <summary>
    /// Returns Phase labels for the Phases that are currently present in the
    /// Material Object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is intended to work in conjunction with the SetPresentPhases
    /// method. Together these methods provide a means of communication between a
    /// PME (or another client) and an Equilibrium Calculator (or other component
    /// that implements the ICapeThermoEquilibriumRoutine interface). The following
    /// sequence of operations is envisaged.
    /// </para>
    /// <para>1. Prior to requesting an Equilibrium Calculation, a PME will use the
    /// SetPresentPhases method to define a list of Phases that may be considered in
    /// the Equilibrium Calculation. Typically, this is necessary because an
    /// Equilibrium Calculator may be capable of handling a large number of Phases
    /// but for a particular application, it may be known that only certain Phases
    /// will be involved. For example, if the complete Phase list contains Phases
    /// with the following labels (with the obvious interpretation): vapour,
    /// hydrocarbonLiquid and aqueousLiquid and it is required to model a liquid
    /// decanter, the present Phases might be set to hydrocarbonLiquid and
    /// aqueousLiquid.</para>
    /// <para>2. The GetPresentPhases method is then used by the CalcEquilibrium
    /// method of the ICapeThermoEquilibriumRoutine interface to obtain the list
    /// of Phase labels corresponding to the Phases that may be present at
    /// equilibrium.</para>
    /// <para>3. The Equilibrium Calculation determines which Phases actually
    /// co-exist at equilibrium. This list of Phases may be a sub-set of the Phases
    /// considered because some Phases may not be present at the prevailing
    /// conditions. For example, if the amount of water is sufficiently small the
    /// aqueousLiquid Phase in the above example may not exist because all the water
    /// dissolves in the hydrocarbonLiquid Phase.</para>
    /// <para>4. The CalcEquilibrium method uses the SetPresentPhases method to indicate
    /// the Phases present following the equilibrium calculation (and sets the phase
    /// properties).</para>
    /// <para>5. The PME uses the GetPresentPhases method to find out the Phases present
    /// following the calculation and it can then use the GetSinglePhaseProp or
    /// GetTPFraction methods to get the Phase properties.</para>
    /// <para>To indicate that a Phase is ‘present’ in a Material Object (or other
    /// component that implements the ICapeThermoMaterial interface) it must be
    /// specified by the SetPresentPhases method of the ICapeThermoMaterial
    /// interface. Even if a Phase is present, it does not imply that any Physical
    /// Properties are actually set unless the phaseStatus is Cape_AtEquilibrium
    /// or Cape_Estimates (see below). </para>
    /// <para>If no Phases are present, UNDEFINED should be returned for both the
    /// phaseLabels and phaseStatus arguments.</para>
    /// <para>The phaseStatus argument contains as many entries as there are Phase
    /// labels. The valid settings are listed in the following table:</para>
    /// <para>Cape_UnknownPhaseStatus - This is the normal setting when a Phase is
    /// specified as being available for an Equilibrium Calculation.</para>
    /// <para>Cape_AtEquilibrium - The Phase has been set as present as a result of
    /// an Equilibrium Calculation.</para>
    /// <para> Cape_Estimates - Estimates of the equilibrium state have been set in
    /// the Material Object.</para>
    /// <para>All the Phases with a status of Cape_AtEquilibrium have values of
    /// temperature, pressure, composition and Phase fraction set that correspond
    /// to an equilibrium state, i.e. equal temperature, pressure and fugacities of
    /// each Compound. Phases with a Cape_Estimates status have values of temperature,
    /// pressure, composition and Phase fraction set in the Material Object. These
    /// values are available for use by an Equilibrium Calculator component to
    /// initialise an Equilibrium Calculation. The stored values are available but
    /// there is no guarantee that they will be used.
    /// </para>
    /// <para>
    /// Using the ClearAllProps method results in a Material Object that is in
    /// the same state as when it was first created. It is an alternative to using
    /// the CreateMaterial method but it is expected to have a smaller overhead in
    /// operating system resources.
    /// </para>
    /// </remarks>
    /// <param name="phaseLabels">A reference to a String array that contains the
    /// list of Phase labels (identifiers – names) for the Phases present in the
    /// Material Object. The Phase labels in the Material Object must be a
    /// subset of the labels returned by the GetPhaseList method of the
    /// ICapeThermoPhases interface.</param>
    /// <param name="phaseStatus">A CapeArrayEnumeration which is an array of
    /// Phase status flags corresponding to each of the Phase labels.
    /// See description below.</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [DispId(6)]
    [Description("method GetPresentPhases")]
    void GetPresentPhases([In, Out] ref object phaseLabels, [In, Out] ref object phaseStatus);

    /// <summary>
    /// Retrieves single-phase non-constant Physical Property values for a mixture.
    /// </summary>
    /// <remarks>
    /// <para>The results argument returned by GetSinglePhaseProp is either a
    /// CapeArrayDouble that contains one or more numerical values, e.g. temperature,
    /// or a CapeInterface that may be used to retrieve single-phase Physical
    /// Properties described by a more complex data structure, e.g. distributed
    /// properties.</para>
    /// <para>Although the result of some calls to GetSinglePhaseProp may be a
    /// single numerical value, the return type for numerical values is
    /// CapeArrayDouble and in such a case the method must return an array even if
    /// it contains only a single element.</para>
    /// <para>A Phase is ‘present’ in a Material if its identifier is returned by
    /// the GetPresentPhases method. An exception is raised by the GetSinglePhaseProp
    /// method if the Phase specified is not present. Even if a Phase is present,
    /// this does not mean that any Physical Properties are available.</para>
    /// <para>The Physical Property values returned by GetSinglePhaseProp refer to
    /// a single Phase. These values may be set by the SetSinglePhaseProp method,
    /// which may be called directly, or by other methods such as the CalcSinglePhaseProp
    /// method of the ICapeThermoPropertyRoutine interface or the CalcEquilibrium
    /// method of the ICapeThermoEquilibriumRoutine interface. Note: Physical
    /// Properties that depend on more than one Phase, for example surface tension
    /// or K-values, are returned by the GetTwoPhaseProp method.</para>
    /// <para>It is expected that this method will normally be able to provide
    /// Physical Property values on any basis, i.e. it should be able to convert
    /// values from the basis on which they are stored to the basis requested. This
    /// operation will not always be possible. For example, if the molecular weight
    /// is not known for one or more Compounds, it is not possible to convert from
    /// mass fractions or mass flows to mole fractions or molar flows.</para>
    /// </remarks>
    /// <param name="property">CapeString The identifier of the Physical Property
    /// for which values are requested. This must be one of the single-phase Physical
    /// Properties or derivatives. The standard identifiers are listed in sections
    /// 7.5.5 and 7.6.</param>
    /// <param name="phaseLabel">CapeString Phase label of the Phase for which
    ///  the Physical Property is required. The Phase label must be one of the
    /// identifiers returned by the GetPresentPhases method of this interface.</param>
    /// <param name="basis">CapeString Basis of the results. Valid settings are:
    /// “Mass” for Physical Properties per unit mass or “Mole” for molar properties.
    /// Use UNDEFINED as a place holder for a Physical Property for which basis does
    /// not apply. See section 7.5.5 for details.</param>
    /// <param name="results">CapeVariant Results vector (CapeArrayDouble)
    /// containing Physical Property value(s) in SI units or CapeInterface (see
    /// notes).	</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">The property required is
    /// not available from the Material Object possibly for the Phase label or
    /// basis requested. This exception is raised when a property value has not been
    /// set following a call to the CreateMaterial or the value has been erased by
    /// a call to the ClearAllProps methods.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed: for example UNDEFINED for property, or an unrecognised
    /// identifier for phaseLabel.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The necessary initialisation has not been performed, or has failed.
    /// This exception is returned if the Phase specified does not exist.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method GetSinglePhaseProp")]
    void GetSinglePhaseProp([In] string property, [In] string phaseLabel, [In] string basis, [In, Out] ref object results);

    /// <summary>
    /// Retrieves temperature, pressure and composition for a Phase.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is provided to make it easier for developers to make efficient
    /// use of the CAPEOPEN interfaces. It returns the most frequently requested
    /// information from a Material Object in a single call.
    /// </para>
    /// <para>There is no choice of basis in this method. The composition is always
    /// returned as mole fractions.
    /// </para>
    /// <para>To get the equivalent information for the overall mixture the
    /// GetOverallTPFraction method of the ICapeThermoMaterial interface should be
    /// used.
    /// </para>
    /// </remarks>
    /// <returns>No return.</returns>
    /// <param name="phaseLabel">Phase label of the Phase for which the property
    /// is required. The Phase label must be one of the identifiers returned by the
    /// GetPresentPhases method of this interface.</param>
    /// <param name="temperature">Temperature (in K)</param>
    /// <param name="pressure">Pressure (in Pa)</param>
    /// <param name="composition">Composition (mole fractions)</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetTPFraction is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">One of the properties is
    /// not available from the Material Object. This exception is raised when a
    /// property value has not been set following a call to the CreateMaterial or
    /// the value has been erased by a call to the ClearAllProps methods.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed: for example UNDEFINED for property, or an unrecognised
    /// identifier for phaseLabel.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The necessary initialisation has not been performed, or has failed.
    /// This exception is returned if the Phase specified does not exist.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [DispId(8)]
    [Description("method GetTPfraction")]
    void GetTPFraction(
      [In] string phaseLabel,
      [In, Out] ref double temperature,
      [In, Out] ref double pressure,
      [In, Out] ref object composition);

    /// <summary>
    /// Retrieves two-phase non-constant Physical Property values for a mixture.
    /// </summary>
    /// <remarks>
    ///  <para>
    /// The results argument returned by GetTwoPhaseProp is either a CapeArrayDouble
    ///  that contains one or more numerical values, e.g. kvalues, or a CapeInterface
    ///  that may be used to retrieve 2-phase Physical Properties described by a more
    ///  complex data structure, e.g.distributed Physical Properties.
    /// </para>
    ///  <para>Although the result of some calls to GetTwoPhaseProp may be a single
    ///  numerical value, the return type for numerical values is CapeArrayDouble and
    ///  in such a case the method must return an array even if it contains only a
    ///  single element.
    /// </para>
    ///  <para>A Phase is ‘present’ in a Material if its identifier is returned by
    ///  the GetPresentPhases method. An exception is raised by the GetTwoPhaseProp
    ///  method if any of the Phases specified is not present. Even if all Phases are
    ///  present, this does not mean that any Physical Properties are available.
    /// </para>
    ///  <para>The Physical Property values returned by GetTwoPhaseProp depend on two
    ///  Phases, for example surface tension or K-values. These values may be set by
    ///  the SetTwoPhaseProp method that may be called directly, or by other methods
    ///  such as the CalcTwoPhaseProp method of the ICapeThermoPropertyRoutine
    ///  interface, or the CalcEquilibrium method of the ICapeThermoEquilibriumRoutine
    ///  interface. Note: Physical Properties that depend on a single Phase are
    ///  returned by the GetSinglePhaseProp method.
    /// </para>
    ///  <para>It is expected that this method will normally be able to provide
    ///  Physical Property values on any basis, i.e. it should be able to convert
    ///  values from the basis on which they are stored to the basis requested. This
    ///  operation will not always be possible. For example, if the molecular weight
    ///  is not known for one or more Compounds, it is not possible to convert between
    ///  a mass basis and a molar basis.
    /// </para>
    ///  <para>If a composition derivative is requested this means that the
    ///  derivatives are returned for both Phases in the order in which the Phase
    ///  labels are specified. The number of values returned for a composition
    ///  derivative will depend on the dimensionality of the property. For example,
    ///  if there are N Compounds then the results vector for the surface tension
    ///  derivative will contain N composition derivative values for the first Phase,
    ///  followed by N composition derivative values for the second Phase. For K-value
    ///  derivative there will be N2 derivative values for the first phase followed by
    ///  N2 values for the second phase in the order defined in 7.6.2.
    /// </para>
    ///  </remarks>
    /// <param name="property">The identifier of the property for which values are
    /// requested. This must be one of the two-phase Physical Properties or Physical
    /// Property derivatives listed in sections 7.5.6 and 7.6.</param>
    /// <param name="phaseLabels">List of Phase labels of the Phases for which the
    /// property is required. The Phase labels must be two of the identifiers
    /// returned by the GetPhaseList method of the Material Object.</param>
    /// <param name="basis">Basis of the results. Valid settings are: “Mass” for
    /// Physical Properties per unit mass or “Mole” for molar properties. Use
    /// UNDEFINED as a place holder for a Physical Property for which basis does not
    /// apply. See section 7.5.5 for details.</param>
    /// <param name="results">Results vector (CapeArrayDouble) containing property
    /// value(s) in SI units or CapeInterface (see notes).</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation. This could be the case if two-phase non-constant
    /// Physical Properties are not required by the PME and so there is no particular
    /// need to implement this method.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">The property required is
    /// not available from the Material Object possibly for the Phases or basis
    /// requested.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. This exception is raised when a call to the SetTwoPhaseProp method
    /// has not been performed, or has failed, or when one or more of the Phases
    /// referenced does not exist.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed: for example, UNDEFINED for property, or an unrecognised
    /// identifier in phaseLabels.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method GetTwoPhaseProp")]
    [DispId(9)]
    void GetTwoPhaseProp([In] string property, [In] object phaseLabels, [In] string basis, [In, Out] ref object results);

    /// <summary>
    /// Sets non-constant property values for the overall mixture.
    /// </summary>
    /// <remarks>
    /// <para>The property values set by SetOverallProp refer to the overall mixture.
    /// These values are retrieved by calling the GetOverallProp method. Overall
    /// mixture properties are not calculated by components that implement the
    /// ICapeThermoMaterial interface. The property values are only used as input
    /// specifications for the CalcEquilibrium method of a component that implements
    /// the ICapeThermoEquilibriumRoutine interface.</para>
    /// <para>Although some properties set by calls to SetOverallProp will have a
    /// single value, the type of argument values is CapeArrayDouble and the method
    /// must always be called with values as an array even if it contains only a
    /// single element.</para>
    /// </remarks>
    /// <param name="property"> CapeString The identifier of the property for which
    /// values are set. This must be one of the single-phase properties or derivatives
    /// that can be stored for the overall mixture. The standard identifiers are
    /// listed in sections 7.5.5 and 7.6.</param>
    /// <param name="basis">Basis of the results. Valid settings are: “Mass” for
    /// Physical Properties per unit mass or “Mole” for molar properties. Use
    /// UNDEFINED as a place holder for a Physical Property for which basis does not
    /// apply. See section 7.5.5 for details.</param>
    /// <param name="values">Values to set for the property.</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation. This method may not be required if the PME
    /// does not deal with any single-phase property.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, that is a value that does not belong to the valid list
    /// described above, for example UNDEFINED for property.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">One or more of the entries in the
    /// values argument is outside of the range of values accepted by the Material
    /// Object.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the SetSinglePhaseProp operation, are not suitable.</exception>
    [Description("method SetOverallProp")]
    [DispId(10)]
    void SetOverallProp([In] string property, [In] string basis, [In] object values);

    /// <summary>
    /// Allows the PME or the Property Package to specify the list of Phases that
    /// are currently present.
    /// </summary>
    /// <remarks>
    ///  <para>SetPresentPhases may be used:</para>
    ///  <para>• to restrict an Equilibrium Calculation (using the CalcEquilibrium
    ///  method of a component that implements the ICapeThermoEquilibriumRoutine
    ///  interface) to a subset of the Phases supported by the Property Package
    ///  component;</para>
    ///  <para>• when the component that implements the ICapeThermoEquilibriumRoutine
    ///  interface needs to specify which Phases are present in a Material Object
    ///  after an Equilibrium Calculation has been performed.</para>
    ///  <para>If a Phase in the list is already present, its Physical Properties are
    ///  unchanged by the action of this method. Any Phases not in the list when
    ///  SetPresentPhases is called are removed from the Material Object. This means
    ///  that any Physical Property values that may have been stored on the removed
    ///  Phases are no longer available (i.e. a call to GetSinglePhaseProp or
    ///  GetTwoPhaseProp including this Phase will return an exception). A call to
    ///  the GetPresentPhases method of the Material Object will return the same list
    ///  as specified by SetPresentPhases.</para>
    ///  <para>The phaseStatus argument must contain as many entries as there are
    ///  Phase labels. The valid settings are listed in the following table:</para>
    ///  <para>Cape_UnknownPhaseStatus - This is the normal setting when a Phase is
    ///  specified as being available for an Equilibrium Calculation.</para>
    ///  <para>Cape_AtEquilibrium - The Phase has been set as present as a result of
    ///  an Equilibrium Calculation.</para>
    ///  <para>Cape_Estimates - Estimates of the equilibrium state have been set in
    ///  the Material Object.</para>
    ///  <para>All the Phases with a status of Cape_AtEquilibrium must have
    ///  properties that correspond to an equilibrium state, i.e. equal temperature,
    ///  pressure and fugacities of each Compound (this does not imply that the
    ///  fugacities are set as a result of the Equilibrium Calculation). The
    ///  Cape_AtEquilibrium status should be set by the CalcEquilibrium method of a
    ///  component that implements the ICapeThermoEquilibriumRoutine interface
    ///  following a successful Equilibrium Calculation. If the temperature, pressure
    ///  or composition of an equilibrium Phase is changed, the Material Object
    ///  implementation is responsible for resetting the status of the Phase to
    ///  Cape_UnknownPhaseStatus. Other property values stored for that Phase should
    ///  not be affected.</para>
    ///  <para>Phases with an Estimates status must have values of temperature,
    /// pressure, composition and phase fraction set in the Material Object. These
    ///  values are available for use by an Equilibrium Calculator component to
    ///  initialise an Equilibrium Calculation. The stored values are available but
    ///  there is no guarantee that they will be used.</para>
    ///  </remarks>
    /// <param name="phaseLabels"> CapeArrayString The list of Phase labels for
    /// the Phases present. The Phase labels in the Material Object must be a
    /// subset of the labels returned by the GetPhaseList method of the
    /// ICapeThermoPhases interface.</param>
    /// <param name="phaseStatus">Array of Phase status flags corresponding to
    /// each of the Phase labels. See description below.</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, that is a value that does not belong to the valid list
    /// described above, for example if phaseLabels contains UNDEFINED or
    /// phaseStatus contains a value that is not in the above table.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when
    /// other error(s), specified for this operation, are not suitable.</exception>
    [Description("method SetPresentPhases")]
    [DispId(11)]
    void SetPresentPhases([In] object phaseLabels, [In] object phaseStatus);

    /// <summary>
    /// Sets single-phase non-constant property values for a mixture.
    /// </summary>
    /// <remarks>
    /// <para>The values argument of SetSinglePhaseProp is either a CapeArrayDouble
    /// that contains one or more numerical values to be set for a property, e.g.
    /// temperature, or a CapeInterface that may be used to set single-phase
    /// properties described by a more complex data structure, e.g. distributed
    /// properties.</para>
    /// <para>Although some properties set by calls to SetSinglePhaseProp will have a
    /// single numerical value, the type of the values argument for numerical values
    /// is CapeArrayDouble and in such a case the method must be called with values
    /// containing an array even if it contains only a single element.</para>
    /// <para>The property values set by SetSinglePhaseProp refer to a single Phase.
    /// Properties that depend on more than one Phase, for example surface tension or
    /// K-values, are set by the SetTwoPhaseProp method of the Material Object.</para>
    /// <para>Before SetSinglePhaseProp can be used, the phase referenced must have
    /// been created using the SetPresentPhases method.</para>
    /// </remarks>
    /// <param name="prop">The identifier of the property for which values are
    /// set. This must be one of the single-phase properties or derivatives. The
    /// standard identifiers are listed in sections 7.5.5 and 7.6.</param>
    /// <param name="phaseLabel">Phase label of the Phase for which the property is
    /// set. The phase label must be one of the strings returned by the
    /// GetPresentPhases method of this interface.</param>
    /// <param name="basis">Basis of the results. Valid settings are: “Mass” for
    /// Physical Properties per unit mass or “Mole” for molar properties. Use
    /// UNDEFINED as a place holder for a Physical Property for which basis does not
    /// apply. See section 7.5.5 for details.</param>
    /// <param name="values">Values to set for the property (CapeArrayDouble) or
    /// CapeInterface (see notes). </param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists but it is not supported by
    /// the current implementation. This method may not be required if the PME does
    /// not deal with any single-phase properties.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, that is a value that does not belong to the valid list
    /// described above, for example UNDEFINED for property.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">One or more of the entries in the
    /// values argument is outside of the range of values accepted by the Material
    /// Object.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The phase referenced has not been created using SetPresentPhases.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the SetSinglePhaseProp operation, are not suitable.</exception>
    [DispId(12)]
    [Description("method SetSinglePhaseProp")]
    void SetSinglePhaseProp([In] string prop, [In] string phaseLabel, [In] string basis, [In] object values);

    /// <summary>
    /// Sets two-phase non-constant property values for a mixture.
    /// </summary>
    /// <remarks>
    /// <para>The values argument of SetTwoPhaseProp is either a CapeArrayDouble that
    /// contains one or more numerical values to be set for a property, e.g. kvalues,
    /// or a CapeInterface that may be used to set two-phase properties described by
    /// a more complex data structure, e.g. distributed properties.</para>
    /// <para>Although some properties set by calls to SetTwoPhaseProp will have a
    /// single numerical value, the type of the values argument for numerical values
    /// is CapeArrayDouble and in such a case the method must be called with the
    /// values argument containing an array even if it contains only a single element.</para>
    /// <para>The Physical Property values set by SetTwoPhaseProp depend on two
    /// Phases, for example surface tension or K-values. Properties that depend on a
    /// single Phase are set by the SetSinglePhaseProp method.</para>
    /// <para>If a Physical Property with composition derivative is specified, the
    /// derivative values will be set for both Phases in the order in which the Phase
    /// labels are specified. The number of values returned for a composition
    /// derivative will depend on the property. For example, if there are N Compounds
    /// then the values vector for the surface tension derivative will contain N
    /// composition derivative values for the first Phase, followed by N composition
    /// derivative values for the second Phase. For K-values there will be N2
    /// derivative values for the first phase followed by N2 values for the second
    /// phase in the order defined in 7.6.2.</para>
    /// <para>Before SetTwoPhaseProp can be used, all the Phases referenced must have
    /// been created using the SetPresentPhases method</para>
    /// </remarks>
    /// <param name="property">The property for which values are set in the
    /// Material Object. This must be one of the two-phase properties or derivatives
    /// included in sections 7.5.6 and 7.6.</param>
    /// <param name="phaseLabels">Phase labels of the Phases for
    /// which the property is set. The Phase labels must be two of the identifiers
    /// returned by the GetPhaseList method of the ICapeThermoPhases interface.</param>
    /// <param name="basis">Basis of the results. Valid settings are: “Mass” for
    /// Physical Properties per unit mass or “Mole” for molar properties. Use
    /// UNDEFINED as a place holder for a Physical Property for which basis does not
    /// apply. See section 7.5.5 for details.</param>
    /// <param name="values">Value(s) to set for the property (CapeArrayDouble) or
    /// CapeInterface (see notes).</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists but it is not supported by
    /// the current implementation. This method may not be required if the PME does
    /// not deal with any single-phase properties.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value was passed, that is a value that does not belong to the valid list
    /// described above, for example UNDEFINED for property.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">One or more of the entries in the
    /// values argument is outside of the range of values accepted by the Material
    /// Object.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The pre-requisites are not
    /// valid. The phase referenced has not been created using SetPresentPhases.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the SetSinglePhaseProp operation, are not suitable.</exception>
    [DispId(13)]
    [Description("method SetTwoPhaseProp")]
    void SetTwoPhaseProp([In] string property, [In] object phaseLabels, [In] string basis, [In] object values);
  }
}
