// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeCollection
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface provides the behaviour for a read-only collection. It can be
  /// used for storing ports or parameters.
  /// </summary>
  /// <remarks>
  /// <para>The aim of the Collection interface is to give a CAPE-OPEN component
  /// the possibility to expose a list of objects to any client of the component.
  /// The client will not be able to modify the collection, i.e. removing,
  /// replacing or adding elements. However, since the client will have access to
  /// any CAPE-OPEN interface exposed by the items of the collection, it will be
  /// able to modify the state of any element.</para>
  /// <para>CAPE-OPEN Collections don’t allow exposing basic types such as
  /// numerical values or strings. Indeed, using CapeArrays is more convenient
  /// here.</para>
  /// <para>Not all the items of a collection must belong to the same class. It is
  /// enough if they implement the same interface or set of interfaces. A CAPE-OPEN
  /// specification a component that exposes a collection interface must state
  /// clearly which interfaces must be implemented by all the items of the
  /// collection.</para>
  /// <para>Reference document: Collection Common Interface</para>
  /// </remarks>
  [ComVisible(false)]
  [Guid("678c099a-0093-11d2-a67d-00105a42887f")]
  [Description("ICapeCollection Interface")]
  [ComImport]
  public interface ICapeCollection
  {
    /// <summary>
    /// Gets the specific item stored within the collection, identified by its
    /// ICapeIdentification.ComponentName or 1-based index passed as an argument
    /// to the method.
    /// </summary>
    /// <remarks>
    /// Return an element from the collection. The requested element can be
    /// identified by its actual name (e.g. type CapeString) or by its position
    /// in the collection (e.g. type CapeLong). The name of an element is the
    /// value returned by the ComponentName() method of its ICapeIdentification
    /// interface. The advantage of retrieving an item by name rather than by
    /// position is that it is much more efficient. This is because it is faster
    /// to check all names from the server part than checking then from the
    /// client, where a lot of COM/CORBA calls would be required.
    /// </remarks>
    /// <param name="index">
    /// <para>Identifier for the requested item:</para>
    /// <para>name of item (the variant contains a string)</para>
    /// <para>position in collection (it contains a long)</para>
    /// </param>
    /// <returns>
    /// System.Object containing the requested collection item.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">ECapeOutOfBounds</exception>
    [DispId(1)]
    [Description("gets an item specified by index or name")]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object Item(object index);

    /// <summary>
    /// Gets the number of items currently stored in the collection.
    /// </summary>
    /// <remarks>Return the number of items in the collection.</remarks>
    /// <returns>Return the number of items in the collection.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    [DispId(2)]
    [Description("Number of items in the collection")]
    int Count();
  }
}
