// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeParameterEvents
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
  [Guid("3C32AD8E-490D-4822-8A8E-073F5EDFF3F5")]
  [Description("CapeParameterEvents Interface")]
  public interface ICapeParameterEvents
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
    /// <param name="args">A <see cref="M:CapeOpen.ICapeParameterEvents.ParameterValueChanged(System.Object,System.Object)">ParameterValueChanged</see> that contains information about the event.</param>
    void ParameterValueChanged(object sender, object args);

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
    /// <param name="args">A <see cref="T:CapeOpen.ParameterModeChangedEventArgs">ParameterModeChangedEventArgs</see> that contains information about the event.</param>
    void ParameterModeChanged(object sender, object args);

    /// <summary>Occurs when a parameter is validated.</summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnParameterValidated</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnParameterValidated</c> in a derived class, be sure to call the base class's <c>OnParameterValidated</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.RealParameter">RealParameter</see> that raised the event.</param>
    /// <param name="args">A <see cref="T:CapeOpen.ParameterValidatedEventArgs">ParameterValidatedEventArgs</see> that contains information about the event.</param>
    void ParameterValidated(object sender, object args);

    /// <summary>Occurs when the user resets a parameter.</summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnParameterReset</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnParameterReset</c> in a derived class, be sure to call the base class's <c>OnParameterReset</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.RealParameter">RealParameter</see> that raised the event.</param>
    /// <param name="args">A <see cref="T:CapeOpen.ParameterResetEventArgs">ParameterResetEventArgs</see> that contains information about the event.</param>
    void ParameterReset(object sender, object args);
  }
}
