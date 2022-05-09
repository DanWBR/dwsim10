// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeReactionChemistry
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides information about the reactions in the reaction package.
  /// </summary>
  /// <remarks>
  /// A component or a PME that needs to describe a set of reactions will implement this
  /// interface. A set of reactions is described in terms of the compounds that take part
  /// in the reactions and the compounds that are produced. For example, in the case of
  /// electrolyte	systems, salt complexes and ions. In the case of detailed reaction mechanisms,
  /// radicals.
  /// </remarks>
  [ComVisible(false)]
  [Description("ICapeChemistry Interface")]
  [Guid("678c0afb-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeReactionChemistry
  {
    /// <summary>
    /// Number of reactions contained within this reaction package.
    /// </summary>
    /// <remarks>
    /// Returns the number of reactions contained in this reactions package.
    /// </remarks>
    /// <returns>Returns the number of reactions contained in this reactions package.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetNumberOfReactions")]
    [DispId(1)]
    int GetNumberOfReactions();

    /// <summary>
    /// The string identifiers of the reactions contained within this reaction package.
    /// </summary>
    /// <remarks>
    /// Returns the identifiers of all the reactions contained within the Reactions Package.
    /// </remarks>
    /// <returns>Returns the string identifiers for each one of the reactions.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(2)]
    [Description("method GetReactionsIds")]
    object GetReactionsIds();

    /// <summary>
    /// The <see cref="T:CapeOpen.CapeReactionType" /> of the reaction.
    /// </summary>
    /// <remarks>
    /// Returns the <see cref="T:CapeOpen.CapeReactionType" /> of a particular reaction. Only needed for non-electrolyte
    /// reactions. It informs whether the reaction is an equilibrium or kinetic
    /// reaction
    /// </remarks>
    /// <param name="reacID">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <returns>Returns the <see cref="T:CapeOpen.CapeReactionType" /> type of a particular reaction.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetReactionType")]
    [DispId(3)]
    CapeReactionType GetReactionType(string reacID);

    /// <summary>The number of compounds in the specified reaction.</summary>
    /// <remarks>
    /// Gets the number of compounds occurring in a particular reaction within a Reactions Package.
    /// </remarks>
    /// <param name="reacID">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <returns>Returns the number of compounds participating in the specified reaction.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetNumberOfReactionCompounds")]
    [DispId(4)]
    int GetNumberOfReactionCompounds(string reacID);

    /// <summary>
    /// Get the identifiers of the components participating in the specified reaction
    /// within the reaction set defined in the Reactions Package.
    /// </summary>
    /// <remarks>
    /// This method returns both compound name and CAS registry number. The CAS Registry
    /// number should be used to identify the compounds for validation purposes because
    /// it is unambiguous.
    /// </remarks>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <param name="compIds">List of compound IDs.</param>
    /// <param name="compCharge">The charge for each compound.</param>
    /// <param name="compCASNumber">The CAS Registry numbers for the compounds.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetReactionCompoundIds")]
    [DispId(5)]
    void GetReactionCompoundIds(
      string reacId,
      ref object compIds,
      ref object compCharge,
      ref object compCASNumber);

    /// <summary>Get the stoichiometry of the specified reaction.</summary>
    /// <remarks>
    /// Returns the stoichiometric coefficients of the specified reaction (positive
    /// numbers indicate products, negative numbers indicate reactants). Stoichiometric
    /// coefficients are ordered consistently with the list of compounds returned by
    /// the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionCompoundIds(System.String,System.Object@,System.Object@,System.Object@)" /> method for the same reaction.
    /// </remarks>
    /// <returns>The stoichiometry of the specified reaction.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(6)]
    [Description("method GetStoichiometricCoefficients")]
    object GetStoichiometricCoefficients(string reacId);

    /// <summary>
    /// Gets the phase on which a particular reaction contained in the Reactions Package will take place.
    /// </summary>
    /// <remarks>
    /// The string returned by this method must match one of the phase labels known to the Property Package.
    /// </remarks>
    /// <returns>The phase label of the phase where the reaction tackes place.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(7)]
    [Description("method GetReactionPhase")]
    string GetReactionPhase(string reacId);

    /// <summary>
    /// Get the basis for the reaction rate will be expressed in (i.e. homogeneous
    /// or heterogeneous).
    /// </summary>
    /// <remarks>
    /// Gets the phase on which the reactions contained in the package will take place. The
    /// reaction rate basis (i.e. “Homogeneous” or “Heterogeneous”). Homogeneous reactions
    /// will be provided in kgmole/h/m3 and heterogeneous will be provided in
    /// kgmole/h/kg-cat.
    /// </remarks>
    /// <returns>A <see cref="T:CapeOpen.CapeReactionRateBasis" /> for the rate basis.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(8)]
    [Description("method GetReactionRateBasis")]
    CapeReactionRateBasis GetReactionRateBasis(string reacId);

    /// <summary>
    /// Get the concentration basis the reaction package will use to calculate the
    /// specified reaction rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gets the concentration basis required that will be used by a particular reaction in
    /// its rate equation.
    /// </para>
    /// <para>
    /// Qualifiers defined in the THRM spec can be used here (i.e. “fugacity”,
    /// “moleFraction”, etc).
    /// </para>
    /// </remarks>
    /// <returns>The concentration basis the reaction package will use to calculate the
    /// specified reaction rate.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetReactionConcBasis")]
    [DispId(9)]
    string GetReactionConcBasis(string reacId);

    /// <summary>Get the base reactant for the specified reaction.</summary>
    /// <remarks>
    /// Returns the name of the base reactant for a particular reaction..
    /// </remarks>
    /// <returns>The name of the base reactant for a particular reaction.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetBaseReactant")]
    [DispId(10)]
    string GetBaseReactant(string reacId);

    /// <summary>
    /// Returns the number and ids of the compounds in the specified phase.
    /// </summary>
    /// <remarks>
    /// Returns the number and ids of the compounds in the specified phase.
    /// </remarks>
    /// <returns>The name of the base reactant for a particular reaction.</returns>
    /// <param name="reacID">Label of the required phase.</param>
    /// <param name="compNo">The number of compounds in the requested phase.</param>
    /// <param name="compIds">The ids of the compounds present in the specified phase.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(11)]
    [Description("method GetPhaseCompounds")]
    void GetPhaseCompounds(string reacID, ref int compNo, ref object compIds);

    /// <summary>
    /// Returns a collection containing the rate expression parameters for a particular reaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GetReactionParameters returns a collection of CAPE-OPEN parameters [6] that
    /// characterize the rate expression used by the reaction model in a Reaction Package.
    /// For a PowerLaw model this collection would contain parameters for activation energy,
    /// pre-exponential factor and compound exponents for example. It is up to the Reactions
    /// Package implementor to decide whether a client can update the values of these
    /// parameters. If this operation is allowed, then the implementor must also provide
    /// support for persistence [5] interfaces, so that the updated values can be saved and
    /// restored. In this case the COSE is also responsible for calling the persistence
    /// methods.
    /// </para>
    /// <para>
    /// Deliberately, the standard does not define the names of the parameters that may
    /// appear in such a collection, even for well-known reaction models, such as PowerLaw
    /// and Langmuir – Hinshelwood – Hougen – Watson (LHHW). This is because the formulation
    /// of well-known models is not fixed, and because the standard needs to support custom
    /// models as well as the well-known models.
    /// </para>
    /// <para>
    /// This decision is not expected to be restrictive: in most cases the (software) client
    /// of a Reactions Package does not need to know what model the package implements and
    /// what parameters it has. However, the parameters may be of interest to an end-user who
    /// wants to adjust or estimate the parameter values. In these cases the COSE can invoke
    /// the Reaction Package’s own GUI, or, if it doesn’t have one, present the parameters in
    /// a generic grid. It is the Reaction Package implementor’s responsibility to provide
    /// documentation for the parameters so that an enduser can understand how they are used.
    /// </para>
    /// </remarks>
    /// <returns>A collection containing the rate expression parameters for a particular reaction.</returns>
    /// <param name="reacId">The name of the reaction obtained from the <see cref="M:CapeOpen.ICapeReactionChemistry.GetReactionsIds" /> method.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(12)]
    [Description("method GetReactionParameters")]
    object GetReactionParameters(string reacId);
  }
}
