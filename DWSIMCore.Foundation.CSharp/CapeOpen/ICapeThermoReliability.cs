// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoReliability
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Interface for the reliability of the Thermo Object.</summary>
  /// <remarks>
  /// The ThermoReliability object is still an uncertain
  /// interface. This object holds some measure of the reliability of
  /// the physical property calculation.  It might be a boolean.  It
  /// might be an enumerated type, or it might be a real number.
  /// </remarks>
  [Guid("678c0992-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [Description("ICapeThermoReliability Interface")]
  [ComImport]
  public interface ICapeThermoReliability
  {
  }
}
