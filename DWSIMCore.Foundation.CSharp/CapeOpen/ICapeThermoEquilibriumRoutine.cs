// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoEquilibriumRoutine
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Implemented by any component or object that can perform an Equilibrium Calculation.
  /// </summary>
  /// <remarks>
  /// <para>Any component or object that can perform an Equilibrium Calculation must
  /// implement the ICapeThermoEquilibriumRoutine interface. Within the scope of this
  /// specification, this means that it must be implemented by Equilibrium Calculator
  /// components, Property Package components and by Material Object implementations
  /// that will be passed to clients which may need to perform Equilibrium Calculations,
  /// such as Unit Operations [2].</para>
  /// <para>When a Material Object implements the ICapeThermoEquilibriumRoutine
  /// interface, it is expected that the methods will be delegated either to proprietary
  /// methods within a PME, or to methods in an associated CAPE-OPEN Property Package or
  /// Equilibrium Calculator component.</para>
  /// </remarks>
  [Description("ICapeThermoEquilibriumRoutine Interface")]
  [ComVisible(false)]
  [Guid("678C0AA0-7D66-11D2-A67D-00105A42887F")]
  [ComImport]
  public interface ICapeThermoEquilibriumRoutine
  {
    /// <summary> CalcEquilibrium is used to calculate the amounts and compositions
    /// of Phases at equilibrium. CalcEquilibrium will calculate temperature and/or
    /// pressure if these are not among the two specifications that are mandatory for
    /// each Equilibrium Calculation considered.</summary>
    /// <remarks>
    /// <para>The specification1 and specification2 arguments provide the information
    /// necessary to retrieve the values of two specifications, for example the
    /// pressure and temperature, for the Equilibrium Calculation. The CheckEquilibriumSpec
    /// method can be used to check for supported specifications. Each specification
    /// variable contains a sequence of strings in the order defined in the following
    /// table (hence, the specification arguments may have 3 or 4 items):<para>
    /// <para>property identifier The property identifier can be any of the identifiers
    /// listed in section 7.5.5 but only certain property specifications will normally
    /// be supported by any Equilibrium Routine.</para>
    /// basis The basis for the property value. Valid settings for basis are given in
    /// section 7.4. Use UNDEFINED as a placeholder for a property for which basis does
    /// not apply. For most Equilibrium Specifications, the result of the calculation
    /// is not dependent on the basis, but, for example, for phase fraction
    /// specifications the basis (Mole or Mass) does make a difference.</para>
    /// <para>phase label The phase label denotes the Phase to which the specification
    /// applies. It must either be one of the labels returned by GetPresentPhases, or
    /// the special value “Overall”.</para>
    /// compound identifier (optional)The compound identifier allows for specifications
    /// that depend on a particular Compound. This item of the specification array is
    /// optional and may be omitted. In case of a specification without compound
    /// identifier, the array element may be present and empty, or may be absent.</para>
    /// <para>Some examples of typical phase equilibrium specifications are given in
    /// the table below.</para>
    /// <para>The values corresponding to the specifications in the argument list and
    /// the overall composition of the mixture must be set in the associated Material
    /// Object before a call to CalcEquilibrium.</para>
    /// <para>Components such as a Property Package or an Equilibrium Calculator must
    /// implement the ICapeThermoMaterialContext interface, so that an
    /// ICapeThermoMaterial interface can be passed via the SetMaterial method. It is
    /// the responsibility of the implementation of CalcEquilibrium to validate the
    /// Material Object before attempting a calculation.</para>
    /// <para>The Phases that will be considered in the Equilibrium Calculation are
    /// those that exist in the Material Object, i.e. the list of phases specified in
    /// a SetPresentPhases call. This provides a way for a client to specify whether,
    /// for example, a vapour-liquid, liquid-liquid, or vapourliquid-liquid calculation
    /// is required. CalcEquilibrium must use the GetPresentPhases method to retrieve
    /// the list of Phases and the associated Phase status flags. The Phase status
    /// flags may be used by the client to provide information about the Phases, for
    /// example whether estimates of the equilibrium state are provided. See the
    /// description of the GetPresentPhases and SetPresentPhases methods of the
    /// ICapeThermoMaterial interface for details. When the Equilibrium Calculation
    /// has been completed successfully, the SetPresentPhases method must be used to
    /// specify which Phases are present at equilibrium and the Phase status flags for
    /// the phases should be set to Cape_AtEquilibrium. This must include any Phases
    /// that are present in zero amount such as the liquid Phase in a dew point
    /// calculation.</para>
    /// <para>Some types of Phase equilibrium specifications may result in more than
    /// one solution. A common example of this is the case of a dew point calculation.
    /// However, CalcEquilibrium can provide only one solution through the Material
    /// Object. The solutionType argument allows the “Normal” or “Retrograde” solution
    /// to be explicitly requested. When none of the specifications includes a phase
    /// fraction, the solutionType argument should be set to “Unspecified”.</para>
    /// <para>The definition of “Normal” is</para>
    /// <para>where V F is the vapour phase fraction and the derivatives are at
    /// equilibrium states. For “Retrograde” behaviour,</para>
    /// <para>CalcEquilibrium must set the amounts, compositions, temperature and
    /// pressure for all Phases present at equilibrium, as well as the temperature and
    /// pressure for the overall mixture if not set as part of the calculation
    /// specifications. CalcEquilibrium must not set any other Physical Properties.</para>
    /// <para>As an example, the following sequence of operations might be performed
    /// by CalcEquilibrium in the case of an Equilibrium Calculation at fixed pressure
    /// and temperature:</para>
    /// <para>- With the ICapeThermoMaterial interface of the supplied Material Object:
    /// </para>
    /// <para>- Use the GetPresentPhases method to find the list of Phases that the
    /// Equilibrium Calculation should consider.</para>
    /// <para>- With the ICapeThermoCompounds interface of the Material Object use the
    /// GetCompoundIds method to find which Compounds are present.</para>
    /// <para>- Use the GetOverallProp method to get the temperature, pressure and
    /// composition for the overall mixture.</para>
    /// <para>- Perform the Equilibrium Calculation.</para>
    /// <para>- Use SetPresentPhases to specify the Phases present at equilibrium and
    /// set the Phase status flags to Cape_AtEquilibrium.</para>
    /// <para>- Use SetSinglePhaseProp to set pressure, temperature, Phase amount
    /// (or Phase fraction) and composition for all Phases present.</para>
    /// </remarks>
    /// <param name="specification1">First specification for the Equilibrium
    /// Calculation. The specification information is used to retrieve the value of
    /// the specification from the Material Object. See below for details.</param>
    /// <param name="specification2">Second specification for the Equilibrium
    /// Calculation in the same format as specification1.</param>
    /// <param name="solutionType"><para>The identifier for the required solution type.
    /// The standard identifiers are given in the following list:</para>
    /// <para>Unspecified</para>
    /// <para>Normal</para>
    /// <para>Retrograde</para>
    /// <para>The meaning of these terms is defined below in the notes. Other
    /// identifiers may be supported but their interpretation is not part of the CO
    /// standard.</para></param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The necessary pre-requisite operation has
    /// not been called prior to the operation request. The ICapeThermoMaterial interface
    /// has not been passed via a SetMaterial call prior to calling this method.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">The Equilibrium Calculation could not be
    /// solved. For example if the solver has run out of iterations, or has converged
    /// to a trivial solution.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">Would be raised if the Equilibrium Routine
    /// is not able to perform the flash it has been asked to perform. For example,
    /// the values given to the input specifications are valid, but the routine is not
    /// able to perform a flash given a temperature and a Compound fraction. That
    /// would imply a bad usage or no usage of CheckEquilibriumSpec method, which is
    /// there to prevent calling CalcEquilibrium for a calculation which cannot be
    /// performed.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed. It would be raised, for example, if a specification
    /// identifier does not belong to the list of recognised identifiers. It would
    /// also be raised if the value given to argument solutionType is not among
    /// the three defined, or if UNDEFINED was used instead of a specification identifier.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation"><para>The pre-requisites for the Equilibrium
    /// Calculation are not valid. For example:</para>
    /// <para>• The overall composition of the mixture is not defined.</para>
    /// <para>• The Material Object (set by a previous call to the SetMaterial method of the
    /// ICapeThermoMaterialContext interface) is not valid. This could be because no
    /// Phases are present or because the Phases present are not recognised by the
    /// component that implements the ICapeThermoEquilibriumRoutine interface.</para>
    /// <para>• Any other necessary input information is not available.</para></exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [DispId(1)]
    [Description("method CalcEquilibrium")]
    void CalcEquilibrium([In] object specification1, [In] object specification2, [In] string solutionType);

    /// <summary>Checks whether the Property Package can support a particular type of
    /// Equilibrium Calculation.</summary>
    /// <remarks>
    /// <para>The meaning of the specification1, specification2 and solutionType
    /// arguments is the same as for the CalcEquilibrium method.</para>
    /// <para>The result of the check should only depend on the capabilities and
    /// configuration (compounds and phases present) of the component that implements
    /// the ICapeThermoEquilibriumRoutine interface (eg. a Property package). It should
    /// not depend on whether a Material Object has been set nor on the state
    /// (temperature, pressure, composition etc.) or configuration of a Material
    /// Object that might be set.</para>
    /// <para>If solutionType, specification1 and specification2 arguments appear
    /// valid but the actual specifications are not supported or not recognised a
    /// False value should be returned.</para>
    /// </remarks>
    /// <param name="specification1">First specification for the Equilibrium
    /// Calculation.</param>
    /// <param name="specification2">Second specification for the Equilibrium
    /// Calculation.</param>
    /// <param name="solutionType">The required solution type.</param>
    /// <returns>Set to True if the combination of specifications and solutionType is
    /// supported or False if not supported.</returns>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example UNDEFINED for solutionType, specification1 or
    /// specification2 argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [Description("method CheckEquilibriumSpec")]
    [DispId(2)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool CheckEquilibriumSpec([In] object specification1, [In] object specification2, [In] string solutionType);
  }
}
