// Decompiled with JetBrains decompiler
// Type: CapeOpen.PortConnectedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>An object was connected to the port.</summary>
  /// <remarks>An object was connected to the port.</remarks>
  [ComVisible(true)]
  [Guid("962B9FDE-842E-43F8-9280-41C5BF80DDEC")]
  [ClassInterface(ClassInterfaceType.None)]
  [Serializable]
  public class PortConnectedEventArgs : EventArgs, IPortConnectedEventArgs
  {
    private string m_portName;

    /// <summary>Creates an instance of the PortConnectedEventArgs class for the port.</summary>
    /// <remarks>You can use this constructor when raising the PortConnectedEventArgs at run time to
    /// inform the system that the poert was connected.
    /// </remarks>
    public PortConnectedEventArgs(string portName) => this.m_portName = portName;

    /// <summary>The name of the port being connected.</summary>
    /// <value>The name of the port being connected.</value>
    public string PortName => this.m_portName;
  }
}
