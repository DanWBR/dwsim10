// Decompiled with JetBrains decompiler
// Type: CapeOpen.IPortConnectedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>An object was connected to the port.</summary>
  /// <remarks>An object was connected to the port.</remarks>
  [ComVisible(true)]
  [Description("PortConnectedEventArgs Interface")]
  [Guid("DC735166-8008-4B39-BE1C-6E94A723AD65")]
  public interface IPortConnectedEventArgs
  {
    /// <summary>The name of the port being connected.</summary>
    string PortName { get; }
  }
}
