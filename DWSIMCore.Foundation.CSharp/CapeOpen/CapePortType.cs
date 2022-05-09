// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapePortType
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;

namespace CapeOpen
{
  /// <summary>
  /// The type of objects or information that can flow into the unit operation from
  /// the connected object.
  /// </summary>
  /// <remarks>
  /// This enumeration provide the flowsheeting tool with information related to the type of the port, that is, whether the unit operation uses the object attaches to the port as a
  /// material, information, or energy. This can be used to by the flowsheet to
  /// aid in the selection of the port to which to attach the material, information or energy object.
  /// </remarks>
  [Serializable]
  public enum CapePortType
  {
    /// <summary>
    /// Indicates that a material flow is expected through this port to the unit operation.
    /// </summary>
    CAPE_MATERIAL,
    /// <summary>
    /// Indicates that an energy flow is expected through this port to the unit operation.
    /// </summary>
    CAPE_ENERGY,
    /// <summary>
    /// Indicates that an information flow is expected through this port to the unit operation.
    /// </summary>
    CAPE_INFORMATION,
    /// <summary>
    /// Indicates that either material, energy, or information can flow through this port to the unit operation.
    /// </summary>
    CAPE_ANY,
  }
}
