// Decompiled with JetBrains decompiler
// Type: CapeOpen.IUnitOperationBeginCalculationEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Event fired at the start of a unit operation was calculation.
  /// </summary>
  /// <remarks>
  /// Provides information about the start of the calculation of the unit operation.
  /// </remarks>
  [Description("IUnitOperationBeginCalculationEventArgs Interface")]
  [ComVisible(true)]
  [Guid("3E827FD8-5BDB-41E4-81D9-AC438BC9B957")]
  public interface IUnitOperationBeginCalculationEventArgs
  {
    /// <summary>The name of the unit operation being calculated.</summary>
    string UnitOperationName { get; }

    /// <summary>
    /// The message reulting from the start of the unit operation calculation.</summary>
    /// <remarks>The message provides information about the start of the unit operation calculation process.</remarks>
    /// <value>Information regrading the validation process.</value>
    string Message { get; }
  }
}
