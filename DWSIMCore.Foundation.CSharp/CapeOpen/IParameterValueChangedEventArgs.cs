// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterValueChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides data for the value changed event associated with the parameters.
  /// </summary>
  /// <remarks>
  /// The IParameterValueChangedEventArgs interface specifies the old and new value of the parameter.
  /// </remarks>
  [Guid("41E1A3C4-F23C-4B39-BC54-39851A1D09C9")]
  [ComVisible(true)]
  [Description("CapeIdentificationEvents Interface")]
  public interface IParameterValueChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The value of the parameter prior to the change.</summary>
    /// <remarks>The former value of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The value of the parameter prior to the change.</value>
    object OldValue { get; }

    /// <summary>The value of the parameter after the change.</summary>
    /// <remarks>The new nvalue of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The value of the parameter after the change.</value>
    object NewValue { get; }
  }
}
