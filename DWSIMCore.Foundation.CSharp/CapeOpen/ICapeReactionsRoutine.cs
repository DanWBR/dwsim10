// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeReactionsRoutine
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Calculates the values of reaction (or reaction related) properties.
  /// </summary>
  /// <remarks>
  /// Similar in scope to ICapeThermoPropertyPackage. A software component or a PME that can
  /// calculate values of reaction (or reaction related) properties will implement this
  /// interface. It may also be implemented by a Physical Property package component
  /// that deals with electrolytes.
  /// </remarks>
  [ComVisible(false)]
  [Description("ICapeReactionsRoutine Interface")]
  [Guid("678c0af9-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeReactionsRoutine
  {
    /// <summary>
    /// Sets the values of the specified reaction property within a reactions object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Reactions Package is passed a list of reaction properties to be calculated, the
    /// reaction IDS for which the properties are required, and the calculation basis for the
    /// reaction properties (i.e. mole or mass). A material object containing the
    /// thermodynamic state variables that need to be used for calculating the reaction
    /// properties (e.g. T, P and compositions) is passed separately via a call to the
    /// setMaterial method of the Reaction Package’s <see cref="T:CapeOpen.ICapeThermoContext" /> interface.
    /// </para>
    /// <para>
    /// The results of the calculation will be written to the reaction object passed to the
    /// Reactions Package via either the <see cref="T:CapeOpen.ICapeKineticReactionContext" /> interface for a
    /// kinetic reaction package, or the <see cref="T:CapeOpen.ICapeElectrolyteReactionContext" /> interface for an
    /// Electrolyte Property Package.
    /// </para>
    /// </remarks>
    /// <returns>The name of the base reactant for a particular reaction.</returns>
    /// <param name="props">The Reaction Property to be calculated.</param>
    /// <param name="phase">The qualified phase for the results.</param>
    /// <param name="reacIds">The qualified reactions for the Reaction Property. NULL to
    /// specify all reactions in the set.</param>
    /// <param name="basis">Qualifies the basis of the Reaction Property (i.e., mass
    /// /mole). Default is mole. Use NULL only as a placeholder for property for which basis
    /// does not apply.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method CalcReactionProp")]
    [DispId(1)]
    void CalcReactionProp(object props, string phase, object reacIds, string basis);
  }
}
