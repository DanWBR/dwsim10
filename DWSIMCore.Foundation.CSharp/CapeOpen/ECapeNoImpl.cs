// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeNoImpl
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// An exception that indicates that the requested operation has not been implemented by the current object.
  /// </summary>
  /// <remarks>
  /// The operation is “not” implemented even if this operation can be called due
  /// to the compatibility with the CO standard. That is to say that the operation
  /// exists but it is not supported by the current implementation.
  /// </remarks>
  [Description("ECapeNoImpl Interface")]
  [ComVisible(false)]
  [Guid("678c0b1a-7d66-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ECapeNoImpl
  {
  }
}
