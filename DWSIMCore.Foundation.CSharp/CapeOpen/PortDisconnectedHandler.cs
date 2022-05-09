// Decompiled with JetBrains decompiler
// Type: CapeOpen.PortDisconnectedHandler
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Represents the method that will handle disconnecting an object from a unit port.
  /// </summary>
  [ComVisible(false)]
  public delegate void PortDisconnectedHandler(object sender, PortDisconnectedEventArgs args);
}
