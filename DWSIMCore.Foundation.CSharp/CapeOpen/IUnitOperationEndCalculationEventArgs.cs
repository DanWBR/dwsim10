// Decompiled with JetBrains decompiler
// Type: CapeOpen.IUnitOperationEndCalculationEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// The unit operation calculation prcess has been completed.
  /// </summary>
  /// <remarks>
  /// Provides information about the completion of the unit operation calculation process.
  /// </remarks>
  [ComVisible(true)]
  [Description("IUnitOperationEndCalculationEventArgs Interface")]
  [Guid("951D755F-8831-4691-9B54-CC9935A5B7CC")]
  public interface IUnitOperationEndCalculationEventArgs
  {
    /// <summary>The name of the unit operation being calculated.</summary>
    /// <value>The name of the unit operation being calculated.</value>
    string UnitOperationName { get; }

    /// <summary>
    /// The message from the unit operation regarding the completion of the calculation process.</summary>
    /// <remarks>The message provides information about the completion of the calculated process.</remarks>
    /// <value>Information regarding the completion of the calculated process.</value>
    string Message { get; }
  }
}
