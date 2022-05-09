// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeComputation
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// The base interface of the errors hierarchy related to calculations.
  /// </summary>
  /// <remarks>
  /// This class is used to indicate that an error occurred in the performance of a calculation.
  /// Other calculation-related classes such as
  /// <see cref="T:CapeOpen.ECapeFailedInitialisation" />,
  /// <see cref="T:CapeOpen.ECapeOutOfResources" />,
  /// <see cref="T:CapeOpen.ECapeSolvingError" />,
  /// <see cref="T:CapeOpen.ECapeBadInvOrder" />,
  /// <see cref="T:CapeOpen.ECapeInvalidOperation" />,
  /// <see cref="T:CapeOpen.ECapeNoMemory" />, and
  /// <see cref="T:CapeOpen.ECapeTimeOut" />
  /// derive from this class.
  /// </remarks>
  [Guid("678c0b1c-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeComputation Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeComputation
  {
  }
}
