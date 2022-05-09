// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeLimitedImpl
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The limit of the implementation has been violated.</summary>
  /// <remarks>
  /// <para>An operation may be partially implemented for example a Property Package could
  /// implement TP flash but not PH flash. If a caller requests for a PH flash, then
  /// this error indicates that some flash calculations are supported but not the
  /// requested one.
  /// </para>
  /// <para>The factory can only create one instance (because the component is an
  /// evaluation copy), when the caller requests for a second creation this error shows
  /// that this implementation is limited.
  /// </para>
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0b1b-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeLimitedImpl Interface")]
  [ComImport]
  public interface ECapeLimitedImpl
  {
  }
}
