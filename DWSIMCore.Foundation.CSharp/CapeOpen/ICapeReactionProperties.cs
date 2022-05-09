// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeReactionProperties
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides access to the properties of a particular reaction.
  /// </summary>
  /// <remarks>
  /// Similar in scope to ICapeThermoMaterialObject. A component or a PME that needs to
  /// provide access to the properties of a particular reaction will implement this interface.
  /// </remarks>
  [Description("ICapeReactionProperties Interface")]
  [Guid("678c0afa-0100-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeReactionProperties
  {
    /// <summary>
    /// Gets the value of the specified reaction property within a reactions object.
    /// </summary>
    /// <remarks>
    /// The qualifiers passed in determine the reactions, phase and calculation basis for
    /// which the property will be got. The order of the array is the same as in the passed
    /// in reacIds array (i.e. property value for reaction reacIds[1] will be stored in
    /// property[1]).
    /// </remarks>
    /// <returns>The name of the base reactant for a particular reaction.</returns>
    /// <param name="property">The Reaction Property to be retrieved.</param>
    /// <param name="phase">The qualified phase for the Reaction Property.</param>
    /// <param name="reacIds">The qualified reactions for the Reaction Property. NULL to
    /// specify all reactions in the set.</param>
    /// <param name="basis"><para>Qualifies the basis of the Reaction Property (i.e., mass
    /// /mole). Default is mole. Use NULL only as a placeholder for property for which basis
    /// does not apply.</para>
    /// <para> This qualifier could be extended with values such as activity, fugacity,
    /// fractions, molality…This way when an equilibrium constant is requested its basis can
    /// be specified.</para>
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetReactionProp")]
    [DispId(1)]
    object GetReactionProp(string property, string phase, object reacIds, string basis);

    /// <summary>
    /// Sets the values of the specified reaction property within a reactions object.
    /// </summary>
    /// <remarks>
    /// The qualifiers passed in determine the reactions, phase and calculation basis for
    /// which the property will be retrieved.
    /// </remarks>
    /// <returns>The name of the base reactant for a particular reaction.</returns>
    /// <param name="property">The Reaction Property to be retrieved.</param>
    /// <param name="phase">The qualified phase for the Reaction Property.</param>
    /// <param name="reacIds">The qualified reactions for the Reaction Property. NULL to
    /// specify all reactions in the set.</param>
    /// <param name="basis"><para>Qualifies the basis of the Reaction Property (i.e., mass
    /// /mole). Default is mole. Use NULL only as a placeholder for property for which basis
    /// does not apply.</para>
    /// <para> This qualifier could be extended with values such as activity, fugacity,
    /// fractions, molality…This way when an equilibrium constant is requested its basis can
    /// be specified.</para>
    /// </param>
    /// <param name="propVals">The values of the requested reaction property. The order of
    /// the array is the same as in the passed in reacIds array (i.e. property value for
    /// reaction reacIds[1] will be stored in property[1]).</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(2)]
    [Description("method SetReactionProp")]
    void SetReactionProp(
      string property,
      string phase,
      object reacIds,
      string basis,
      object propVals);
  }
}
