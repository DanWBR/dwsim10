// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeSimulationContext
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Encloses the diagnostic functionality.</summary>
  /// <remarks>
  /// An intferace to be supported by the PME in order to pass a reference to the
  /// ICapeUtilities:SetSimulation to the PMC. The PMC may then
  /// use any of the PME COSE interfaces.
  /// </remarks>
  [Description("ICapeSimulation Context Interface")]
  [ComVisible(false)]
  [Guid("678c0a9c-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeSimulationContext
  {
  }
}
