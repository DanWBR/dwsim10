// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeUnknown
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This exception is raised when other error(s), specified by the operation, do not suit.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A standard exception that can be thrown by a CAPE-OPEN object to indicate that the error
  /// that occurred was not one that was suitable for any of the other errors supported by the object. </para>
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0b12-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeUnknown Interface")]
  [ComImport]
  public interface ECapeUnknown
  {
  }
}
