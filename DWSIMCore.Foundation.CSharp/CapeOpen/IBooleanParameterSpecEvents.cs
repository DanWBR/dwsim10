// Decompiled with JetBrains decompiler
// Type: CapeOpen.IBooleanParameterSpecEvents
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
  [Guid("07D17ED3-B25A-48EA-8261-5ED2D076ABDD")]
  [Description("CapeRealParameterEvents Interface")]
  public interface IBooleanParameterSpecEvents
  {
    /// <summary>
    /// Occurs when the user changes of the default value of a parameter.
    /// </summary>
    /// <remarks><para>Raising an event invokes the event handler through a delegate.</para>
    /// <para>The <c>OnParameterDefaultValueChanged</c> method also allows derived classes to handle the event without attaching a delegate. This is the preferred
    /// technique for handling the event in a derived class.</para>
    /// <para>Notes to Inheritors: </para>
    /// <para>When overriding <c>OnParameterDefaultValueChanged</c> in a derived class, be sure to call the base class's <c>OnParameterDefaultValueChanged</c> method so that registered
    /// delegates receive the event.</para>
    /// </remarks>
    /// <param name="sender">The <see cref="T:CapeOpen.RealParameter">RealParameter</see> that raised the event.</param>
    /// <param name="args">A <see cref="M:CapeOpen.IBooleanParameterSpecEvents.ParameterDefaultValueChanged(System.Object,System.Object)">ParameterDefaultValueChanged</see> that contains information about the event.</param>
    void ParameterDefaultValueChanged(object sender, object args);

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
  }
}
