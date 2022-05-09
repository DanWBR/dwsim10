// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterLowerBoundChangedEventArgs
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
  /// The IParameterLowerBoundChangedEventArgs interface specifies the old and new lower bound of the parameter.
  /// </remarks>
  [Guid("FBCE7FC9-0F58-492B-88F9-8A23A23F93B1")]
  [ComVisible(true)]
  [Description("CapeIdentificationEvents Interface")]
  public interface IParameterLowerBoundChangedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The lower bound of the parameter prior to the change.</summary>
    /// <remarks>The former lower bound of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The lower bound of the parameter prior to the change.</value>
    object OldLowerBound { get; }

    /// <summary>The lower bound of the parameter after to the change.</summary>
    /// <remarks>The former lower bound of the parameter can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The lower bound of the parameter after to the change.</value>
    object NewLowerBound { get; }
  }
}
