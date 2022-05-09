// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapePortDirection
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;

namespace CapeOpen
{
  /// <summary>
  /// The direction that objects or information connected to the port is expected to flow (e.g. material, energy or information objects).
  /// </summary>
  /// <remarks>
  /// This enumeration provide the flowsheeting tool with information related to the direction of the port, that is, whether the port take in
  /// material, information, or energy; or outputs a material, information of energy. This can be used to by the flowsheet to
  /// aid in the selection of the port to which to attach the material, information or energy object.
  /// </remarks>
  [Serializable]
  public enum CapePortDirection
  {
    /// <summary>Signifies an inlet port to the unit operation.</summary>
    CAPE_INLET,
    /// <summary>Signifies an outlet port to the unit operation.</summary>
    CAPE_OUTLET,
    /// <summary>
    /// Signifies a port that can be either an inlet or an outlet to the unit operation.
    /// </summary>
    CAPE_INLET_OUTLET,
  }
}
