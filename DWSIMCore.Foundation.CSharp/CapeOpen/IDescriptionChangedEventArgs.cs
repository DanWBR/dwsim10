// Decompiled with JetBrains decompiler
// Type: CapeOpen.IDescriptionChangedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Event thrown to indicate that the description of a component has changed.
  /// </summary>
  [Description("CapeIdentificationEvents Interface")]
  [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
  [ComVisible(true)]
  [Guid("34C43BD3-86B2-46d4-8639-E0FA5721EC5C")]
  public interface IDescriptionChangedEventArgs
  {
    /// <summary>The description of the PMC prior to the name change.</summary>
    /// <remarks>The former description of the unit can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The description of the unit prior to the description change.</value>
    string OldDescription { get; }

    /// <summary>The name of the PMC after the name change.</summary>
    /// <remarks>The description name of the unit can be used to update GUI inforamtion about the PMC.</remarks>
    /// <value>The description of the unit after the description change.</value>
    string Newdescription { get; }
  }
}
