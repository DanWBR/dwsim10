// Decompiled with JetBrains decompiler
// Type: CapeOpen.IPortDisconnectedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The port was disconnected.</summary>
  /// <remarks>The port was disconnected.</remarks>
  [Description("PortDisconnectedEventArgs Interface")]
  [ComVisible(true)]
  [Guid("5EFDEE16-7858-4119-B8BB-7394FFBCC02D")]
  public interface IPortDisconnectedEventArgs
  {
    /// <summary>The name of the port being disconnected.</summary>
    string PortName { get; }
  }
}
