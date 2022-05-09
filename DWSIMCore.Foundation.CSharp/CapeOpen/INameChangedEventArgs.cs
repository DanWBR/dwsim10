// Decompiled with JetBrains decompiler
// Type: CapeOpen.INameChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Event thrown to indicate that the name of a component has changed.
  /// </summary>
  [Guid("F79EA405-4002-4fb2-AED0-C1E48793637D")]
  [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
  [ComVisible(true)]
  [Description("CapeIdentificationEvents Interface")]
  public interface INameChangedEventArgs
  {
    /// <summary>The name of the PMC prior to the name change.</summary>
    /// <remarks>The former name of the unit can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The name of the unit prior to the name change.</value>
    string OldName { get; }

    /// <summary>The name of the PMC after the name change.</summary>
    /// <remarks>The new name of the unit can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The name of the unit after the name change.</value>
    string NewName { get; }
  }
}
