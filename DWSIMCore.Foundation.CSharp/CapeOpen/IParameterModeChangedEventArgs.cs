// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterModeChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides data for the mode changed event associated with the parameters.
  /// </summary>
  /// <remarks>
  /// The IParameterModeChangedEventArgs interface specifies the old and new mode of the parameter.
  /// </remarks>
  [Guid("5405E831-4B5F-4A57-A410-8E91BBF9FFD3")]
  [ComVisible(true)]
  [Description("CapeIdentificationEvents Interface")]
  public interface IParameterModeChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The mode of the parameter prior to the change.</summary>
    /// <remarks>The former mode of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The mode of the parameter prior to the change.</value>
    object OldMode { get; }

    /// <summary>The mode of the parameter after to the change.</summary>
    /// <remarks>The former mode of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The mode of the parameter after to the change.</value>
    object NewMode { get; }
  }
}
