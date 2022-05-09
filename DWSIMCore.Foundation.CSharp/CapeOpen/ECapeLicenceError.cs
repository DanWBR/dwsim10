// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeLicenceError
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// An operation can not be completed because the licence agreement is not respected.
  /// </summary>
  /// <remarks>
  /// Of course, this type of error could also appear outside the CO scope. In this case,
  /// the error does not belong to the CO error handling. It is specific to the platform.
  /// </remarks>
  [Guid("678c0b14-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeLicenceError Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeLicenceError
  {
  }
}
