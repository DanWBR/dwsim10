// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeErrorDummy
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// The ECapeErrorDummy interface is not intended to be used.
  /// </summary>
  /// <remarks>
  /// It is only here to ensure that
  /// the MIDL compiler exports the CapeErrorInterfaceHR enumeration. The compiler only exports
  /// an enumeration if it is used in a method of an exported interface.
  /// </remarks>
  [Guid("678c0b07-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeErrorDummy Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeErrorDummy
  {
    /// <summary>The HRESULT of the Dummy Error.</summary>
    /// <remarks>The HRESULT of the Dummy Error.</remarks>
    /// <value>The HRESULT of the Dummy Error.</value>
    [DispId(1)]
    [Description("property Name")]
    int dummy { get; }
  }
}
