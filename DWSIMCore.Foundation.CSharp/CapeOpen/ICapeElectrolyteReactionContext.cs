// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeElectrolyteReactionContext
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides access to the properties of a set of electrolyte reactions.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This interface allows a reaction object to be passed to a
  /// component that needs access to the properties of a set of equilibrium reactions.
  /// </para>
  /// <para>
  /// This interface is used to set the reaction object upon which reaction calculations
  /// will take place. Calculated reaction properties will be stored in this reaction object.
  /// </para>
  /// </remarks>
  [ComVisible(false)]
  [Description("ICapeElectrolyteReactionContext Interface")]
  [Guid("678c0afd-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeElectrolyteReactionContext
  {
    /// <summary>
    /// Provides access to the properties of a set of equilibrium reactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used to pass the <see cref="T:CapeOpen.ICapeReactionProperties" /> interface of a reaction object to a
    /// component that needs to access the properties of a set of kinetic reactions.
    /// </para>
    /// </remarks>
    /// <param name="reactionsObject">The ICapeReactionProperties interface of a reaction object.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("SetMaterial")]
    [DispId(1)]
    void SetReactionObject(ref object reactionsObject);
  }
}
