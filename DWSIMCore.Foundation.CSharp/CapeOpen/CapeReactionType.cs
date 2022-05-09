// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapeReactionType
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Enumeration for the type of reaction.</summary>
  /// <remarks>
  /// Indicates whether the reaction package is for equilibrium or kinetic type reactions.
  /// </remarks>
  [Guid("678c0b00-0100-11d2-a67d-00105a42887f")]
  [ComVisible(true)]
  public enum CapeReactionType
  {
    /// <summary>Equilibrium reactions.</summary>
    CAPE_EQUILIBRIUM,
    /// <summary>Kinetic reactions.</summary>
    CAPE_KINETIC,
  }
}
