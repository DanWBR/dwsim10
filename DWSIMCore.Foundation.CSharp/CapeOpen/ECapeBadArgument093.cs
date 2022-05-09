// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeBadArgument093
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>An invalid argument value was passed.</summary>
  /// <remarks>
  /// The function call includes an invalid argument value. For instance the passed name of the phase
  /// does not belong to the CO Phase List.
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c0b16-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeBadArgument Interface")]
  [ComImport]
  public interface ECapeBadArgument093
  {
    /// <summary>
    /// The position of the argument value within the signature of the operation. First argument is as position 1.
    /// </summary>
    /// <remarks>
    /// This provides the location of the invalid argument in the argument list for the function call.
    /// </remarks>
    /// <value>The position of the argument that is bad. The first argument is 1.</value>
    [DispId(1)]
    [Description("The position of the argument value within the signature of the operation. First argument is as position 1.")]
    int position { get; }
  }
}
