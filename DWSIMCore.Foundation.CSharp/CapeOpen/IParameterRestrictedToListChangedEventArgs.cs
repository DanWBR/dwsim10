// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterRestrictedToListChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// The restiction to the options list of a parameter was changed.
  /// </summary>
  /// <remarks>
  /// The restiction to the options list of a parameter was changed.
  /// </remarks>
  [ComVisible(true)]
  [Description("ParameterOptionListChangedEventArgs Interface")]
  [Guid("7F357261-095A-4FD4-99C1-ACDAEDA36141")]
  public interface IParameterRestrictedToListChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }
  }
}
