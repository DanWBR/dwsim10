// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeReactionsPackageManager
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Similar in scope to the <see cref="T:CapeOpen.ICapeThermoSystem" />. These interfaces will be implemented by a
  /// Reactions Package Manager component.
  /// </summary>
  /// <remarks>
  /// Provides a list of all supported reaction packages and resolves the selected package.
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0afc-0100-11d2-a67d-00105a42887f")]
  [Description("ICapeReactionsPackageManager Interface")]
  [ComImport]
  public interface ICapeReactionsPackageManager
  {
    /// <summary>A list of all available reaction packages.</summary>
    /// <remarks>
    /// Returns a list of the names of all Reactions Packages available within the Reactions Package Manager..
    /// </remarks>
    /// <value>Returns a list of all available reaction packages.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetListOfReactionsPackages")]
    [DispId(1)]
    object GetListOfReactionsPackages();

    /// <summary>Resolves a reaction routine.</summary>
    /// <remarks>
    /// <para>
    /// Returns the Reactions Package specified by the client of the Reactions Package Manager.
    /// </para>
    /// </remarks>
    /// <param name="reactionsPkg">The name of the reactions routine to be resolved.</param>
    /// <returns>Returns the Reactions Package specified by the client of the Reactions Package Manager.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(2)]
    [Description("method ResolveReactionsPackage")]
    object ResolveReactionsPackage(string reactionsPkg);
  }
}
