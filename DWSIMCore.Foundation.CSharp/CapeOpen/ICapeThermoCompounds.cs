// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoCompounds
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>When implemented by a Property Package, this
  /// interface is used to access the list of Compounds that the Property Package can
  /// deal with, as well as the Compounds Physical Properties. When implemented by a
  /// Material Object, the interface is used for the same purpose but is applied to
  /// the Compounds present in the Material.</summary>
  /// <remarks><para>Any component or object that maintains a list of Compounds must
  /// implement the ICapeThermoCompounds interface. Within the scope of this
  /// specification this means that it must be implemented by Property Package
  /// components and Material Objects. When implemented by a Property Package, this
  /// interface is used to access the list of Compounds that the Property Package can
  /// deal with, as well as the Compounds Physical Properties. When implemented by a
  /// Material Object, the interface is used for the same purpose but is applied to
  /// the Compounds present in the Material.</para>
  /// <para>It is recommended for the SetMaterial method of the ICapeThermoMaterialContext
  /// interface to be called prior to calling any of the methods described below. A
  /// Property Package may contain Physical Property values for all the Compounds that
  /// it supports or it may rely on the PME to provide these data through the Material
  /// Object.</para>
  /// </remarks>
  [ComVisible(false)]
  [Guid("678C0A9D-7D66-11D2-A67D-00105A42887F")]
  [Description("ICapeThermoCompounds Interface")]
  [ComImport]
  public interface ICapeThermoCompounds
  {
    /// <summary>Returns the values of constant Physical Properties for the specified Compounds.</summary>
    /// <remarks><para>The GetConstPropList method can be used in order to check
    /// which constant Physical Properties are available.</para>
    /// <para>If the number of requested Physical Properties is P and the number of
    /// Compounds is C, the propvals array will contain C*P variants. The first C
    /// variants will be the values for the first requested Physical Property (one
    /// variant for each Compound) followed by C values of constants for the second
    /// Physical Property, and so on. The actual type of values returned (Double,
    /// String, etc.) depends on the Physical Property as specified in section 7.5.2.</para>
    /// <para>Physical Properties are returned in a fixed set of units as specified
    /// in section 7.5.2.</para>
    /// <para>If the compIds argument is set to UNDEFINED this is a request to return
    /// property values for all compounds in the component that implements the
    /// ICapeThermoCompounds interface with the compound order the same as that
    /// returned by the GetCompoundList method. For example, if the interface is
    /// implemented by a Property Package component the property request with compIds
    /// set to UNDEFINED means all compounds in the Property Package rather than all
    /// compounds in the Material Object passed to the Property package.</para>
    /// <para>If any Physical Property is not available for one or more Compounds,
    /// then undefined values must be returned for those combinations and an
    /// ECapeThrmPropertyNotAvailable exception must be raised. If the exception is
    /// raised, the client should check all the values returned to determine which
    /// is undefined.</para>
    /// </remarks>
    /// <param name="props">The list of Physical Property identifiers. Valid
    /// identifiers for constant Physical Properties are listed in
    /// section 7.5.2.</param>
    /// <param name="compIds">List of Compound identifiers for which constants are
    /// to be retrieved. Set compIds = UNDEFINED to denote all Compounds in the
    /// component that implements the ICapeThermoCompounds interface.</param>
    /// <returns>Values of constants for the specified Compounds.</returns>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetCompoundConstant is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists, but
    /// it is not supported by the current implementation. This exception should be
    /// raised if no compounds or no properties are supported.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">At least one item in the
    /// list of Physical Properties is not available for a particular Compound. This
    /// exception is meant to be treated as a warning rather than as an error.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">One or more Physical Properties are not
    /// supported by the component that implements this interface. This exception
    /// should also be raised if any element of the props argument is not recognised
    /// since the list of Physical Properties in section 7.5.2 is not intended to be
    /// exhaustive and an unrecognised Physical Property identifier may be valid. If
    /// no Physical Properties at all are supported ECapeNoImpl should be raised
    /// (see above).</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example, an unrecognised Compound identifier or
    /// UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the
    /// Property Package required the SetMaterial method to be called before calling
    /// the GetCompoundConstant method. The error would not be raised when the
    /// GetCompoundConstant method is implemented by a Material Object.</exception>
    [DispId(1)]
    [Description("method GetCompoundConstant")]
    object GetCompoundConstant([In] object props, [In] object compIds);

    /// <summary>Returns the list of all Compounds. This includes the Compound
    /// identifiers recognised and extra information that can be used to further
    /// identify the Compounds.</summary>
    /// <remarks><para>If any item cannot be returned then the value should be set
    ///  to UNDEFINED. The same information can also be extracted using the
    ///  GetCompoundConstant method. The equivalences between GetCompoundList
    ///  arguments and Compound constant Physical Properties, as specified in section
    ///  7.5.2, is as follows:</para>
    ///  <para>compIds - No equivalence. compIds is an artefact, which is assigned by
    ///  the component that implements the GetCompoundList method. This string will
    ///  normally contain a unique Compound identifier such as "benzene". It must be
    ///  used in all the arguments which are named “compIds” in the methods of the
    /// ICapeThermoCompounds and ICapeThermoMaterial interfaces.</para>
    ///  <para>Formulae - chemicalFormula</para>
    ///  <para>names - iupacName</para>
    ///  <para>boilTemps - normalBoilingPoint</para>
    ///  <para>molwts - molecularWeight</para>
    ///  <para>casnos casRegistryNumber</para>
    ///  <para>When the ICapeThermoCompounds interface is implemented by a Material
    ///  Object, the list of Compounds returned is fixed when the Material Object is
    ///  configured.</para>
    ///  <para>For a Property Package component, the Property Package will normally
    ///  contain a limited set of Compounds selected for a particular application,
    ///  rather than all possible Compounds that could be available to a proprietary
    ///  Properties System.</para>
    ///  <para>In order to identify the Compounds of a Property Package, the PME, or
    ///  other client, will use the casnos argument rather than the compIds. This is
    ///  because different PMEs may give different names to the same Compounds and the
    ///  casnos is (almost always) unique. If the casnos is not available (e.g. for
    ///  petroleum fractions), or not unique, the other pieces of information returned
    ///  by GetCompoundList can be used to distinguish the Compounds. It should be
    ///  noted, however, that for communication with a Property Package a client must
    ///  use the Compound identifiers returned in the compIds argument.</para>
    ///  </remarks>
    /// <param name="compIds">List of Compound identifiers</param>
    /// <param name="formulae">List of Compound formulae</param>
    /// <param name="names">List of Compound names.</param>
    /// <param name="boilTemps">List of boiling point temperatures.</param>
    /// <param name="molwts">List of molecular weights.</param>
    /// <param name="casnos">List of Chemical Abstract Service (CAS) Registry
    /// numbers.</param>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetCompoundList is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists, but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the GetCompoundList operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the Property
    /// Package required the SetMaterial method to be called before calling the
    /// GetCompoundList method. The error would not be raised when the
    /// GetCompoundList method is implemented by a Material Object.</exception>
    [DispId(2)]
    [Description("method GetCompoundList")]
    void GetCompoundList(
      [In, Out] ref object compIds,
      [In, Out] ref object formulae,
      [In, Out] ref object names,
      [In, Out] ref object boilTemps,
      [In, Out] ref object molwts,
      [In, Out] ref object casnos);

    /// <summary>
    /// Returns the list of supported constant Physical Properties.
    /// </summary>
    /// <returns>List of identifiers for all supported constant Physical Properties.
    /// The standard constant property identifiers are listed in section 7.5.2.
    /// </returns>
    /// <remarks>
    /// <para>MGetConstPropList returns identifiers for all the constant Physical
    /// Properties that can be retrieved by the GetCompoundConstant method. If no
    /// properties are supported, UNDEFINED should be returned. The CAPE-OPEN
    /// standards do not define a minimum list of Physical Properties to be made
    /// available by a software component that implements the ICapeThermoCompounds
    /// interface.</para>
    /// <para>A component that implements the ICapeThermoCompounds interface may
    /// return constant Physical Property identifiers which do not belong to the
    /// list defined in section 7.5.2.</para>
    /// <para>However, these proprietary identifiers may not be understood by most
    /// of the clients of this component.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation GetConstPropList is “not”
    /// implemented even if this method can be called for reasons of compatibility
    /// with the CAPE-OPEN standards. That is to say that the operation exists, but
    /// it is not supported by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the Get-ConstPropList operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the
    /// Property Package required the SetMaterial method to be called before calling
    /// the GetConstPropList method. The error would not be raised when the
    /// GetConstPropList method is implemented by a Material Object.</exception>
    [DispId(3)]
    [Description("method GetConstPropList")]
    object GetConstPropList();

    /// <summary>Returns the number of Compounds supported.</summary>
    /// <returns>Number of Compounds supported.</returns>
    /// <remarks>The number of Compounds returned by this method must be equal to
    /// the number of Compound identifiers that are returned by the GetCompoundList
    /// method of this interface. It must be zero or a positive number.</remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the
    /// Property Package required the SetMaterial method to be called before calling
    /// the GetNumCompounds method. The error would not be raised when the
    /// GetNumCompounds method is implemented by a Material Object.</exception>
    [Description("method GetNumCompounds")]
    [DispId(4)]
    int GetNumCompounds();

    /// <summary>Returns the values of pressure-dependent Physical Properties for
    /// the specified pure Compounds.</summary>
    /// <param name="props">The list of Physical Property identifiers. Valid
    /// identifiers for pressure-dependent Physical Properties are listed in section
    /// 7.5.4</param>
    /// <param name="pressure">Pressure (in Pa) at which Physical Properties are
    /// evaluated</param>
    /// <param name="compIds">List of Compound identifiers for which Physical
    /// Properties are to be retrieved. Set compIds = UNDEFINED to denote all
    /// Compounds in the component that implements the ICapeThermoCompounds
    /// interface.</param>
    /// <param name="propVals">&gt;Property values for the Compounds specified.</param>
    /// <remarks><para>The GetPDependentPropList method can be used in order to
    /// check which Physical Properties are available.</para>
    /// <para>If the number of requested Physical Properties is P and the number
    /// Compounds is C, the propvals array will contain C*P values. The first C
    /// will be the values for the first requested Physical Property followed by C
    /// values for the second Physical Property, and so on.</para>
    /// <para>Physical Properties are returned in a fixed set of units as specified
    /// in section 7.5.4.</para>
    /// <para>If the compIds argument is set to UNDEFINED this is a request to return
    /// property values for all compounds in the component that implements the
    /// ICapeThermoCompounds interface with the compound order the same as that
    /// returned by the GetCompoundList method. For example, if the interface is
    /// implemented by a Property Package component the property request with compIds
    /// set to UNDEFINED means all compounds in the Property Package rather than all
    /// compounds in the Material Object passed to the Property package.</para>
    /// <para>If any Physical Property is not available for one or more Compounds,
    /// then undefined valuesm must be returned for those combinations and an
    /// ECapeThrmPropertyNotAvailable exception must be raised. If the exception is
    /// raised, the client should check all the values returned to determine which is
    /// undefined.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation. This exception should be raised if no Compounds
    /// or no Physical Properties are supported.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">One or more Physical Properties are not
    /// supported by the component that implements this interface. This exception
    /// should also be raised (rather than ECapeInvalidArgument) if any element of
    /// the props argument is not recognised since the list of Physical Properties
    /// in section 7.5.4 is not intended to be exhaustive and an unrecognised
    /// Physical Property identifier may be valid. If no Physical Properties at all
    /// are supported, ECapeNoImpl should be raised (see above).</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example UNDEFINED for argument props.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">The value of the pressure is outside of
    /// the range of values accepted by the Property Package.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">At least one item in the
    /// properties list is not available for a particular compound.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the
    /// Property Package required the SetMaterial method to be called before calling
    /// the GetPDependentProperty method. The error would not be raised when the
    /// GetPDependentProperty method is implemented by a Material Object.</exception>
    [DispId(5)]
    [Description("method GetPDependentProperty")]
    void GetPDependentProperty([In] object props, [In] double pressure, [In] object compIds, [In, Out] ref object propVals);

    /// <summary>Returns the list of supported pressure-dependent properties.</summary>
    /// <returns>The list of Physical Property identifiers for all supported
    /// pressure-dependent properties. The standard identifiers are listed in
    /// section 7.5.4</returns>
    /// <remarks>
    /// <para>GetPDependentPropList returns identifiers for all the pressure-dependent
    /// properties that can be retrieved by the GetPDependentProperty method. If no
    /// properties are supported UNDEFINED should be returned. The CAPE-OPEN standards
    /// do not define a minimum list of Physical Properties to be made available by
    /// a software component that implements the ICapeThermoCompounds interface.</para>
    /// <para>A component that implements the ICapeThermoCompounds interface may
    /// return identifiers which do not belong to the list defined in section 7.5.4.
    /// However, these proprietary identifiers may not be understood by most of the
    /// clients of this component.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the Property
    /// Package required the SetMaterial method to be called before calling the
    /// GetPDependentPropList method. The error would not be raised when the
    /// GetPDependentPropList method is implemented by a Material Object.</exception>
    [Description("method GetPDependentPropList")]
    [DispId(6)]
    object GetPDependentPropList();

    /// <summary>Returns the values of temperature-dependent Physical Properties for
    /// the specified pure Compounds.</summary>
    /// <param name="props">The list of Physical Property identifiers. Valid
    /// identifiers for temperature-dependent Physical Properties are listed in
    /// section 7.5.3</param>
    /// <param name="temperature">Temperature (in K) at which properties are
    /// evaluated.</param>
    /// <param name="compIds">List of Compound identifiers for which Physical
    /// Properties are to be retrieved. Set compIds = UNDEFINED to denote all
    /// Compounds in the component that implements the ICapeThermoCompounds
    /// interface .</param>
    /// <param name="propVals">Physical Property values for the Compounds specified.</param>
    /// <remarks> <para>The GetTDependentPropList method can be used in order to
    /// check which Physical Properties are available.</para>
    /// <para>If the number of requested Physical Properties is P and the number of
    /// Compounds is C, the propvals array will contain C*P values. The first C will
    /// be the values for the first requested Physical Property followed by C values
    /// for the second Physical Property, and so on.</para>
    /// <para>Properties are returned in a fixed set of units as specified in
    /// section 7.5.3.</para>
    /// <para>If the compIds argument is set to UNDEFINED this is a request to return
    /// property values for all compounds in the component that implements the
    /// ICapeThermoCompounds interface with the compound order the same as that
    /// returned by the GetCompoundList method. For example, if the interface is
    /// implemented by a Property Package component the property request with compIds
    /// set to UNDEFINED means all compounds in the Property Package rather than all
    /// compounds in the Material Object passed to the Property package.</para>
    /// <para>If any Physical Property is not available for one or more Compounds,
    /// then undefined values must be returned for those combinations and an
    /// ECapeThrmPropertyNotAvailable exception must be raised. If the exception is
    /// raised, the client should check all the values returned to determine which is
    /// undefined.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl"> – The operation is “not” implemented even
    /// if this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation. This exception should be raised if no
    /// Compounds or no Physical Properties are supported.</exception>
    /// <exception cref="T:CapeOpen.ECapeLimitedImpl">One or more Physical Properties are not
    /// supported by the component that implements this interface. This exception
    /// should also be raised (rather than ECapeInvalidArgument) if any element of
    /// the props argument is not recognised since the list of properties in section
    /// 7.5.3 is not intended to be exhaustive and an unrecognised Physical Property
    /// identifier may be valid. If no properties at all are supported ECapeNoImpl
    /// should be raised (see above).</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument
    /// value is passed, for example UNDEFINED for argument props.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">The value of the temperature is outside
    /// of the range of values accepted by the Property Package.</exception>
    /// <exception cref="T:CapeOpen.ECapeThrmPropertyNotAvailable">at least one item in the
    /// properties list is not available for a particular compound.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder"> The error to be raised if the
    /// Property Package required the SetMaterial method to be called before calling
    /// the GetTDependentProperty method. The error would not be raised when the
    /// GetTDependentProperty method is implemented by a Material Object.</exception>
    [DispId(7)]
    [Description("method GetTDependentProperty")]
    void GetTDependentProperty(
      [In] object props,
      [In] double temperature,
      [In] object compIds,
      [In, Out] ref object propVals);

    /// <summary>Returns the list of supported temperature-dependent Physical
    /// Properties.</summary>
    /// <returns>The list of Physical Property identifiers for all supported
    /// temperature-dependent properties. The standard identifiers are listed in
    /// section 7.5.3</returns>
    /// <remarks><para>GetTDependentPropList returns identifiers for all the
    /// temperature-dependent Physical Properties that can be retrieved by the
    /// GetTDependentProperty method. If no properties are supported UNDEFINED
    /// should be returned. The CAPE-OPEN standards do not define a minimum list of
    /// properties to be made available by a software component that implements the
    /// ICapeThermoCompounds interface.</para>
    /// <para>A component that implements the ICapeThermoCompounds interface may
    /// return identifiers which do not belong to the list defined in section
    /// 7.5.3. However, these proprietary identifiers may not be understood by most
    /// of the clients of this component.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for the operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">The error to be raised if the Property
    /// Package required the SetMaterial method to be called before calling the
    /// GetTDependentPropList method. The error would not be raised when the
    /// GetTDependentPropList method is implemented by a Material Object.</exception>
    [Description("method GetTDependentPropList")]
    [DispId(8)]
    object GetTDependentPropList();
  }
}
