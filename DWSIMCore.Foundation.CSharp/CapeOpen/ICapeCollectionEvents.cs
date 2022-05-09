// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeCollectionEvents
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// </summary>
  /// <remarks>
  /// </remarks>
  [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
  [ComVisible(true)]
  [Description("CapeCollectionEvents Interface")]
  [Guid("DE9CDE6E-A2D4-4BFF-AA3A-8699FCF3E0EB")]
  public interface ICapeCollectionEvents
  {
    /// <summary>
    /// Occurs when the user changes of the value of a paramter.
    /// </summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnComponentNameChanged</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnParameterValueChanged</c> in a derived class, be sure to call the base class's <c>OnParameterValueChanged</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.RealParameter">RealParameter</see> that raised the event.</param>
    /// <param name="args">A <see cref="M:CapeOpen.ICapeCollectionEvents.CollectionAddingNew(System.Object,System.Object)">CollectionAddingNew</see> that contains information about the event.</param>
    void CollectionAddingNew(object sender, object args);

    /// <summary>
    /// Occurs when the user changes of the mode of a parameter.
    /// </summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnParameterModeChanged</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnParameterModeChanged</c> in a derived class, be sure to call the base class's <c>OnParameterModeChanged</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.RealParameter">RealParameter</see> that raised the event.</param>
    /// <param name="args">A <see cref="M:CapeOpen.ICapeCollectionEvents.CollectionListChanged(System.Object,System.Object)">CollectionListChanged</see> that contains information about the event.</param>
    void CollectionListChanged(object sender, object args);
  }
}
