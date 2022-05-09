// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeRoot
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The root CAPE-OPEN Exception interface.</summary>
  /// <remarks>
  /// The interface of the CAPE-OPEN errors hierarchy. The System package and the ECapeUser
  /// interface depend on this error.
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0b10-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeRoot Interface")]
  [ComImport]
  public interface ECapeRoot
  {
    /// <summary>The name of the error. This is a mandatory field.</summary>
    /// <remarks>The name of the error. This is a mandatory field.</remarks>
    /// <value>The name of the error. This is a mandatory field.</value>
    [Description("error Name")]
    [DispId(1)]
    string Name { get; }
  }
}
