// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeBadCOParameter
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// A parameter, which is an object from the Parameter Common Interface, has an invalid status.
  /// </summary>
  /// <remarks>
  /// The name of the invalid parameter, along with the parameter itself are available from the exception.
  /// </remarks>
  [Description("ECapeBadCOParameter Interface")]
  [Guid("678c0b15-7d66-11d2-a67d-00105a42887f")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeBadCOParameter
  {
    /// <summary>
    /// The name of the CO parameter that is throwing the exception.
    /// </summary>
    /// <remarks>
    /// This provides the name of the parameter that threw the exception.
    /// </remarks>
    /// <value>The name of the parameter that threw the exception.</value>
    [DispId(1)]
    [Description("The name of the CO parameter")]
    string parameterName { get; }

    /// <summary>The parameter that threw the exception.</summary>
    /// <remarks>
    /// This method provides access directly to the parameter that threw the exception.
    /// </remarks>
    /// <value>A reference to the exception taht threw the exception.</value>
    [Description("The parameter")]
    [DispId(2)]
    object parameter { get; }
  }
}
