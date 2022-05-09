// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeInvalidArgument
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// An invalid argument value was passed. For instance the passed name of
  /// the phase does not belong to the CO Phase List.
  /// </summary>
  /// <remarks>
  /// An argument value of the operation is invalid. The position of the
  /// argument value within the signature of the operation. First argument is as
  /// position 1.
  /// </remarks>
  [ComVisible(false)]
  [Description("ECapeInvalidArgument Interface")]
  [Guid("678c0b17-7d66-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ECapeInvalidArgument
  {
  }
}
