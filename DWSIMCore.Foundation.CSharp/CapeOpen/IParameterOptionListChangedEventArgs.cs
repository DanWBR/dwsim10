// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterOptionListChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The parameter was reset.</summary>
  /// <remarks>The parameter was reset.</remarks>
  [ComVisible(true)]
  [Description("ParameterOptionListChangedEventArgs Interface")]
  [Guid("78E06E7B-00AB-4295-9915-546DC1CD64A6")]
  public interface IParameterOptionListChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }
  }
}
