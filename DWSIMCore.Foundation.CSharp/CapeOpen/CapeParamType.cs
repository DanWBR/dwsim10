// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapeParamType
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;

namespace CapeOpen
{
  /// <summary>
  /// Gets the type of the parameter for which this is a specification:
  /// </summary>
  /// <remarks>
  ///    double-precision Real (CAPE_REAL),
  ///    integer(CAPE_INT),
  ///    String (or option)(CAPE_OPTION),
  ///    boolean(CAPE_BOOLEAN)
  ///    array(CAPE_ARRAY)
  /// Reference document: Parameter Common Interface
  /// </remarks>
  [Serializable]
  public enum CapeParamType
  {
    /// <summary>Double-precision real-valued parameter</summary>
    /// <value>0</value>
    CAPE_REAL,
    /// <summary>Integer-valued parameter</summary>
    CAPE_INT,
    /// <summary>String/option parameter</summary>
    CAPE_OPTION,
    /// <summary>Boolean-valued parameter</summary>
    CAPE_BOOLEAN,
    /// <summary>Array parameter</summary>
    CAPE_ARRAY,
  }
}
