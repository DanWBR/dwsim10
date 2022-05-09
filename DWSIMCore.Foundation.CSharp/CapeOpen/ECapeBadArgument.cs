// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeBadArgument
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
  [Description("ECapeBadArgument Interface")]
  [ComVisible(false)]
  [Guid("E29E42B3-E481-45c6-A737-78F4A7FC0391")]
  [ComImport]
  public interface ECapeBadArgument
  {
    /// <summary>
    /// The position of the argument value within the signature of the operation. First argument is as position 1.
    /// </summary>
    /// <remarks>
    /// This provides the location of the invalid argument in the argument list for the function call.
    /// </remarks>
    /// <value>The position of the argument that is bad. The first argument is 1.</value>
    [Description("The position of the argument value within the signature of the operation. First argument is as position 1.")]
    [DispId(1)]
    short position { get; }
  }
}
