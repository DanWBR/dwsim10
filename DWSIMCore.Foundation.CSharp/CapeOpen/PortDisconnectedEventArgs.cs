// Decompiled with JetBrains decompiler
// Type: CapeOpen.PortDisconnectedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The port was disconnected.</summary>
  /// <remarks>The port was disconnected.</remarks>
  [ClassInterface(ClassInterfaceType.None)]
  [Guid("693F33AA-EE4A-4CDF-9BA1-8889086BC8AB")]
  [ComVisible(true)]
  [Serializable]
  public class PortDisconnectedEventArgs : EventArgs, IPortDisconnectedEventArgs
  {
    private string m_portName;

    /// <summary>Creates an instance of the PortDisconnectedEventArgs class for the port.</summary>
    /// <remarks>You can use this constructor when raising the PortDisconnectedEventArgs at run time to
    /// inform the system that the port was disconnected.
    /// </remarks>
    public PortDisconnectedEventArgs(string portName) => this.m_portName = portName;

    /// <summary>The name of the port being disconnected.</summary>
    /// <value>The name of the port being disconnected.</value>
    public string PortName => this.m_portName;
  }
}
