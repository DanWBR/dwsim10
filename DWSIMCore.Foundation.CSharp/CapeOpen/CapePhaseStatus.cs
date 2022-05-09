// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapePhaseStatus
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;

namespace CapeOpen
{
  /// <summary>Status of the phases present in the material object.</summary>
  /// <remarks>All the Phases with a status of Cape_AtEquilibrium have values of
  /// temperature, pressure, composition and Phase fraction set that correspond to an
  /// equilibrium state, i.e. equal temperature, pressure and fugacities of each
  /// Compound. Phases with a Cape_Estimates status have values of temperature, pressure,
  /// composition and Phase fraction set in the Material Object. These values are
  /// available for use by an Equilibrium Calculator component to initialise an
  /// Equilibrium Calculation. The stored values are available but there is no guarantee
  /// that they will be used.
  /// </remarks>
  [Serializable]
  public enum CapePhaseStatus
  {
    /// <summary>
    /// This is the normal setting when a Phase is specified as being available for
    /// an Equilibrium Calculation.
    /// </summary>
    CAPE_UNKNOWNPHASESTATUS,
    /// <summary>
    /// The Phase has been set as present as a result of an Equilibrium Calculation.
    /// </summary>
    CAPE_ATEQUILIBRIUM,
    /// <summary>
    /// Estimates of the equilibrium state have been set in the Material Object.
    /// </summary>
    CAPE_ESTIMATES,
  }
}
