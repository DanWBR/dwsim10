// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterResetEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The parameter was reset.</summary>
  /// <remarks>The parameter was reset.</remarks>
  [Description("ParameterResetEventArgs Interface")]
  [ComVisible(true)]
  [Guid("12067518-B797-4895-9B26-EA71C60A8803")]
  public interface IParameterResetEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }
  }
}
