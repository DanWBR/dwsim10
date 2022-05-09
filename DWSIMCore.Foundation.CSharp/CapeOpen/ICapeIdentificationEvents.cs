// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeIdentificationEvents
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides methods to identify and describe a CAPE-OPEN component.
  /// </summary>
  /// <remarks>
  /// <para>As illustration, we remind requirements coming from the existing interface
  /// specification and being connected with the Identification concept:</para>
  /// <para>The Unit Operations Interfaces have the following requirements:</para>
  /// <para>* If a flowsheet contains two instances of a Unit Operation of a particular
  /// class, the COSE needs to provide the user a textual identifier to distinguish each
  /// of the instances. For instance, when the COSE requires to report about an error
  /// occurred in one of the Unit Operations.</para>
  /// <para>* When the COSE shows the user its GUI to connect the COSE’s streams to the
  /// Unit Operation ports, the COSE needs to request the Unit for its list of available
  /// ports. For the user to identify the ports, the user needs some distinctive textual
  /// information for each of them.</para>
  /// <para>* When the COSE exposes to the user its interfaces to browse or set the
  /// value of an internal parameter of a Unit Operation, the COSE needs to request the
  /// Unit for its list of available parameters. No matter if this COSE’s interface is
  /// a GUI or a programming interface, each parameter must be identified by a textual
  /// string.</para>
  /// <para>The ICapeThermoMaterialObject (used by both Unit and Thermo interfaces):</para>
  /// <para>* If a Unit Operation has encountered an error accessing a stream
  /// (<see cref="T:CapeOpen.ICapeThermoMaterialObject">ICapeThermoMaterialObject</see>), the
  /// Unit might decide to report it to the user. It would be desirable the stream to
  /// have a textual identifier for the user to be able to quickly know which stream
  /// failed.</para>
  /// <para>The Thermodynamic Interfaces have the following requirements:</para>
  /// <para>* The <see cref="T:CapeOpen.ICapeThermoSystem">ICapeThermoSystem</see>
  /// and the <see cref="T:CapeOpen.ICapeThermoPropertyPackage">ICapeThermoPropertyPackage</see>
  /// interfaces don’t require an identification interface, since both of them have been
  /// designed as singletons (a single instance of each component class is required).
  /// That means that there is no need to identify this instance: its class description
  /// would be enough. However, the user might decide anyway to assign a name or a
  /// description to the CAPE-OPEN property systems or property packages used in her/his
  /// flowsheet. Furthermore, if these interfaces evolve, the singleton approach could
  /// be removed. In this case, identifying each instance will be a must.</para>
  /// <para>The Solvers Interfaces have the following requirements:</para>
  /// <para>* Many objects should provide the functionality coming from the
  /// Identification Common Interface.</para>
  /// <para>The SMST Interfaces have the following requirements:</para>
  /// <para>* The CO SMST component package depends on the Identification Interface
  /// package. The interface ICapeSMSTFactory must provide the Identification
  /// capabilities.</para>
  /// <para>Reference document: Identification Common Interface</para>
  /// </remarks>
  [Description("CapeIdentificationEvents Interface")]
  [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
  [ComVisible(true)]
  [Guid("5F5087A7-B27B-4b4f-902D-5F66E34A0CBE")]
  public interface ICapeIdentificationEvents
  {
    /// <summary>Gets and sets the name of the component.</summary>
    /// <remarks>
    /// <para>A particular Use Case in a system may contain several CAPE-OPEN components
    /// of the same class. The user should be able to assign different names and
    /// descriptions to each instance in order to refer to them unambiguously and in a
    /// user-friendly way. Since not always the software components that are able to
    /// set these identifications and the software components that require this information
    /// have been developed by the same vendor, a CAPE-OPEN standard for setting and
    /// getting this information is required.</para>
    /// <para>So, the component will not usually set its own name and description: the
    /// user of the component will do it.</para>
    /// </remarks>
    /// <value>The unique name of the component.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    void ComponentNameChanged(object sender, object args);

    /// <summary>Gets and sets the description of the component.</summary>
    /// <remarks>
    /// <para>A particular Use Case in a system may contain several CAPE-OPEN components
    /// of the same class. The user should be able to assign different names and
    /// descriptions to each instance in order to refer to them unambiguously and in a
    /// user-friendly way. Since not always the software components that are able to
    /// set these identifications and the software components that require this information
    /// have been developed by the same vendor, a CAPE-OPEN standard for setting and
    /// getting this information is required.</para>
    /// <para>So, the component will not usually set its own name and description: the
    /// user of the component will do it.</para>
    /// </remarks>
    /// <value>The description of the component.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    void ComponentDescriptionChanged([MarshalAs(UnmanagedType.IDispatch)] object sender, [MarshalAs(UnmanagedType.IDispatch)] object args);
  }
}
