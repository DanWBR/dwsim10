// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterUpperBoundChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides data for the upper bound changed event associated with the parameters.
  /// </summary>
  /// <remarks>
  /// The IParameterUpperBoundChangedEventArgs interface specifies the old and new lower bound of the parameter.
  /// </remarks>
  [Description("CapeIdentificationEvents Interface")]
  [Guid("A2D0FAAB-F30E-48F5-82F1-4877F61950E9")]
  [ComVisible(true)]
  public interface IParameterUpperBoundChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The upper bound of the parameter prior to the change.</summary>
    /// <remarks>The former upper bound of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The upper bound of the parameter prior to the change.</value>
    object OldUpperBound { get; }

    /// <summary>The upper bound of the parameter after to the change.</summary>
    /// <remarks>The former upper bound of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The upper bound of the parameter after to the change.</value>
    object NewUpperBound { get; }
  }
}
