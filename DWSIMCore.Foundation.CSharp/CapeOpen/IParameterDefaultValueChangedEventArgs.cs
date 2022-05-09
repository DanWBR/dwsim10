// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterDefaultValueChangedEventArgs
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
  /// The IParameterDefaultValueChangedEventArgs interface specifies the old and new default value of the parameter.
  /// </remarks>
  [Description("CapeIdentificationEvents Interface")]
  [Guid("E5D9CE6A-9B10-4A81-9E06-1B6C6C5257F3")]
  [ComVisible(true)]
  public interface IParameterDefaultValueChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The default value of the parameter prior to the change.</summary>
    /// <remarks>The default value of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The default value of the parameter prior to the change.</value>
    object OldDefaultValue { get; }

    /// <summary>The default value of the parameter  after the name change.</summary>
    /// <remarks>The new default value of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The default value of the parameter after the change.</value>
    object NewDefaultValue { get; }
  }
}
