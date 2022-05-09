// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeUnitPort
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface represents the behaviour of a Unit
  /// Operation connection point (Unit Operation Port). It provides different
  /// attributes for configuring the port as well as to connect
  /// it to a material, energy or information object.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The unit port provides the the means by which a Flowsheet Unit is connected to its streams.
  /// Streams are implemented by means of material objects.
  /// </para>
  /// <para>
  /// The three types of port: material, energy and
  /// information, have a lot of functionality in common. By combining the three into one we can simplify
  /// the interface to a useful degree. Each port type is to be distinguished by the value of an attribute.
  /// </para>
  /// </remarks>
  [Guid("678c0999-0093-11d2-a67d-00105a42887f")]
  [Description("ICapeUnitPort Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeUnitPort
  {
    /// <summary>Returns port type.</summary>
    /// <remarks>
    /// Returns the type of this port. Allowed types are among
    /// the ones included in the CapePortType type.
    /// </remarks>
    /// <value>The type of the port.</value>
    /// <see cref="T:CapeOpen.CapePortType">CapePortType</see>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    [DispId(1)]
    [Description("type of port, e.g. material, energy or information")]
    CapePortType portType { get; }

    /// <summary>Returns port direction.</summary>
    /// <remarks>
    /// Returns the direction in which the object connected to this
    /// port is expected to flow. Allowed values are among those included
    /// in the CapePortDirection type.
    /// </remarks>
    /// <value>The direction of the port.</value>
    /// <see cref="T:CapeOpen.CapePortDirection">CapePortDirection</see>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    [Description("direction of port, e.g. input, output or unspecified")]
    [DispId(2)]
    CapePortDirection direction { get; }

    /// <summary>
    /// Returns to the client the object that is connected to this port.
    /// </summary>
    /// <remarks>
    /// Returns the object that is connected to the Port. A client is provided with the
    /// Material, Energy or Information object that was previously connected to the Port,
    /// using the Connect method.
    /// </remarks>
    /// <value>The object connected to the port.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    [Description("gets the objet connected to the port, e.g. material, energy or information")]
    [DispId(3)]
    object connectedObject { [return: MarshalAs(UnmanagedType.IDispatch)] get; }

    /// <summary>
    /// Connects an object to the port. For a material port it must
    /// be an object implementing the ICapeThermoMaterialObject interface,
    /// for Energy and Information ports it must be an object implementing
    /// the ICapeParameter interface.
    /// </summary>
    /// <remarks>
    /// Method used by clients, when they request that a Port connect itself with the object
    /// that is passed in as argument of the method. Probably, before accepting the connection,
    /// a Port will check that the Object sent as argument is of the expected type and
    /// according to the value of its attribute portType.
    /// </remarks>
    /// <param name="objectToConnect">The object to connect to the port.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(4)]
    [Description("connects the port to the object sent as argument, e.g. material, energy or information")]
    void Connect([MarshalAs(UnmanagedType.IDispatch)] object objectToConnect);

    /// <summary>
    /// Disconnects whatever object is connected to this port.
    /// </summary>
    /// <remarks>
    /// <para>Disconnects the port from whichever object is connected to it.</para>
    /// <para>There are no input or output arguments for this method.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("disconnects the port")]
    [DispId(5)]
    void Disconnect();
  }
}
