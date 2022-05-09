// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeImplementation
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// The base class of the errors hierarchy related to the current implementation.
  /// </summary>
  /// <remarks>
  /// This class is used to indicate that an error occurred in the with the implementation of an object.
  /// The implemenation-related classes such as
  /// <see cref="T:CapeOpen.ECapeNoImpl" /> and
  /// <see cref="T:CapeOpen.ECapeLimitedImpl" />
  /// derive from this class.
  /// </remarks>
  [Description("ECapeImplementation Interface")]
  [Guid("678c0b19-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeImplementation
  {
  }
}
