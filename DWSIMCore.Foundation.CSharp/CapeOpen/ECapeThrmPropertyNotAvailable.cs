// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeThrmPropertyNotAvailable
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// An exception that indicates the requested thermodynamic property was not available.
  /// </summary>
  /// <remarks>
  /// At least one item in the requested properties cannot be returned. This could be
  /// because the property cannot be calculated at the specified conditions or for the
  /// specified Phase. If the property calculation is not implemented then
  /// <see cref="T:CapeOpen.ECapeLimitedImpl" /> should be returned.
  /// </remarks>
  [Description("ECapeThrmPropertyNotAvailable Interface")]
  [ComVisible(false)]
  [Guid("678C09B6-7D66-11D2-A67D-00105A42887F")]
  [ComImport]
  public interface ECapeThrmPropertyNotAvailable
  {
  }
}
