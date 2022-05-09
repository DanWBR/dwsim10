// Decompiled with JetBrains decompiler
// Type: CapeOpen.IUnitPortEvents
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
  [Guid("3530B780-5E59-42B1-801B-3C18F2AD08EE")]
  [ComVisible(true)]
  [Description("CapeRealParameterEvents Interface")]
  [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
  public interface IUnitPortEvents
  {
    /// <summary>
    /// Occurs when the user connects a new object to a unit port.
    /// </summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnPortConnected</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnPortConnected</c> in a derived class, be sure to call the base class's <c>OnPortConnected</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.UnitPort">CapeUnitPort</see> that raised the event.</param>
    /// <param name="args">A <see cref="T:CapeOpen.PortConnectedEventArgs">ParameterValueChangedEventArgs</see> that contains information about the event.</param>
    void PortConnected(object sender, PortConnectedEventArgs args);

    /// <summary>
    /// Occurs when the user disconnets a object from a unit port.
    /// </summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnPortDisconnected</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnPortDisconnected</c> in a derived class, be sure to call the base class's <c>OnPortDisconnected</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.UnitPort">CapeUnitPort</see> that raised the event.</param>
    /// <param name="args">A <see cref="T:CapeOpen.PortDisconnectedEventArgs">ParameterValueChangedEventArgs</see> that contains information about the event.</param>
    void PortDisconnected(object sender, PortDisconnectedEventArgs args);
  }
}
