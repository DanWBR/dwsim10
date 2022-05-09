// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoMaterialObject
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Material object interface</summary>
  [ComVisible(false)]
  [Description("ICapeThermoMaterialObject Interface")]
  [Guid("678c0994-7d66-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeThermoMaterialObject
  {
    /// <summary>Get the component ids for this MO</summary>
    /// <remarks>
    /// Returns the list of components Ids of a given Material Object.
    /// </remarks>
    /// <returns>
    /// The names of the compounds in the matieral object in a String array
    /// as a System.Object, which is marshalled as a Object COM-based CAPE-OPEN.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("property ComponentIds")]
    [DispId(1)]
    object ComponentIds { get; }

    /// <summary>Get the phase ids for this MO</summary>
    /// <remarks>
    /// It returns the phases existing in the MO at that moment. The Overall phase
    /// and multiphase identifiers cannot be returned by this method. See notes on
    /// Existence of a phase for more information.
    /// </remarks>
    /// <returns>
    /// The phases present in the material in a String array as a
    /// System.Object, which is marshalled as a Object COM-based CAPE-OPEN.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [DispId(2)]
    [Description("property PhaseIds")]
    object PhaseIds { get; }

    /// <summary>Get some universal constant(s)</summary>
    /// <remarks>
    /// Retrieves universal constants from the Property Package.
    /// </remarks>
    /// <returns>
    /// Values of the requested universal constants in an array of doubles as a
    /// System.Object, which is marshalled as a Object COM-based CAPE-OPEN.
    /// </returns>
    /// <param name="props">
    /// List of universal constants to be retrieved. A System.Object containing a
    /// String array.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    [Description("method GetUniversalConstant")]
    [DispId(3)]
    object GetUniversalConstant(object props);

    /// <summary>Get some pure component constant(s)</summary>
    /// <remarks>
    /// Retrieve component constants from the Property Package. See Notes for more
    /// information.
    /// </remarks>
    /// <returns>
    /// Component Constant values returned from the Property Package for all the
    /// components in the Material Object It is a Object containing a 1 dimensional
    /// array of Objects. If we call P to the number of requested properties and C to
    /// the number requested components the array will contain C*P Objects. The C
    /// first ones (from position 0 to C-1) will be the values for the first requested
    /// property (one Object for each component). After them (from position C to 2*C-1)
    /// there will be the values of constants for the second requested property, and
    /// so on. An array of doubles as a System.Object, which is marshalled as a Object
    /// COM-based CAPE-OPEN.
    /// </returns>
    /// <param name="props">
    /// List of component constants. A System.Object containing a String array
    /// marshalled from a COM Object.
    /// </param>
    /// <param name="compIds">
    /// List of component IDs for which constants are to be retrieved. emptyObject
    /// for all components in the Material Object. A System.Object containing a String
    /// array marshalled from a COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">ECapeNoImpl</exception>
    [DispId(4)]
    [Description("method GetComponentConstant")]
    object GetComponentConstant(object props, object compIds);

    /// <summary>Calculate some properties</summary>
    /// <remarks>
    /// This method is responsible for doing all property calculations and delegating
    /// these calculations to the associated thermo system. This method is further
    /// defined in the descriptions of the CAPE-OPEN Calling Pattern and the User
    /// Guide Section. See Notes for a more detailed explanation of the arguments and
    /// CalcProp description in the notes for a general discussion of the method.
    /// </remarks>
    /// <param name="props">
    /// The List of Properties to be calculated. A System.Object containing a String
    /// array.
    /// </param>
    /// <param name="phases">
    /// List of phases for which the properties are to be calculated. A System.Object
    /// containing a String array.
    /// </param>
    /// <param name="calcType">
    /// Type of calculation: Mixture Property or Pure Component Property. For partial
    /// property, such as fugacity coefficients of components in a mixture, use
    /// “Mixture” CalcType. For pure component fugacity coefficients, use “Pure”
    /// CalcType.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">ECapeSolvingError</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">ECapeOutOfBounds</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [Description("method CalcProp")]
    [DispId(5)]
    void CalcProp(object props, object phases, string calcType);

    /// <summary>Get some pure component constant(s)</summary>
    /// <remarks>
    /// This method is responsible for retrieving the results from calculations from
    /// the MaterialObject. See Notesfor a more detailed explanation of the arguments.
    /// </remarks>
    /// <returns>
    /// Results vector containing property values in SI units arranged by the defined
    /// qualifiers. The array is one dimensional containing the properties, in order
    /// of the "props" array for each of the compounds, in order of the compIds array.
    /// An array of doubles as a System.Object, which is marshalled as a Object
    /// COM-based CAPE-OPEN.
    /// </returns>
    /// <param name="property">
    /// The Property for which results are requested from the MaterialObject.
    /// </param>
    /// <param name="phase">The qualified phase for the results.</param>
    /// <param name="compIds">
    /// The qualified components for the results. emptyObject to specify all
    /// components in the Material Object. For mixture property such as liquid
    /// enthalpy, this qualifier is not required. Use emptyObject as place holder.
    /// A System.Object containing a String array marshalled from a COM Object.
    /// </param>
    /// <param name="calcType">
    /// The qualified type of calculation for the results. (valid Calculation Types:
    /// Pure and Mixture)
    /// </param>
    /// <param name="basis">
    /// Qualifies the basis of the result (i.e., mass /mole). Default is mole. Use
    /// NULL for default or as place holder for property for which basis does not
    /// apply (see also Specific properties.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(6)]
    [Description("method GetProp")]
    object GetProp(string property, string phase, object compIds, string calcType, string basis);

    /// <summary>Get some pure component constant(s)</summary>
    /// <remarks>
    /// This method is responsible for setting the values for properties of the
    /// Material Object. See Notes for a more detailed explanation of the arguments.
    /// </remarks>
    /// <param name="property">
    /// The Property for which results are requested from the MaterialObject.
    /// </param>
    /// <param name="phase">The qualified phase for the results.</param>
    /// <param name="compIds">
    /// The qualified components for the results. emptyObject to specify all
    /// components in the Material Object. For mixture property such as liquid
    /// enthalpy, this qualifier is not required. Use emptyObject as place holder.
    /// A System.Object containing a String array marshalled from a COM Object.
    /// </param>
    /// <param name="calcType">
    /// The qualified type of calculation for the results. (valid Calculation Types:
    /// Pure and Mixture)
    /// </param>
    /// <param name="basis">
    /// Qualifies the basis of the result (i.e., mass /mole). Default is mole. Use
    /// NULL for default or as place holder for property for which basis does not
    /// apply (see also Specific properties.
    /// </param>
    /// <param name="values">Values to set for the property.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method SetProp")]
    [DispId(7)]
    void SetProp(
      string property,
      string phase,
      object compIds,
      string calcType,
      string basis,
      object values);

    /// <summary>Calculate some equilibrium values</summary>
    /// <remarks>
    /// This method is responsible for delegating flash calculations to the
    /// associated Property Package or Equilibrium Server. It must set the amounts,
    /// compositions, temperature and pressure for all phases present at equilibrium,
    /// as well as the temperature and pressure for the overall mixture, if not set
    /// as part of the calculation specifications. See CalcProp and CalcEquilibrium
    /// for more information.
    /// </remarks>
    /// <param name="flashType">The type of flash to be calculated.</param>
    /// <param name="props">
    /// Properties to be calculated at equilibrium. emptyObject for no properties.
    /// If a list, then the property values should be set for each phase present at
    /// equilibrium. A System.Object containing a String array marshalled from
    /// a COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">ECapeBadInvOrder</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">ECapeSolvingError</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfBounds">ECapeOutOfBounds</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(8)]
    [Description("method CalcEquilibrium")]
    void CalcEquilibrium(string flashType, object props);

    /// <summary>Set the independent variable for the state</summary>
    /// <remarks>
    /// Sets the independent variable for a given Material Object.
    /// </remarks>
    /// <param name="indVars">
    /// Independent variables to be set (see names for state variables for list of
    /// valid variables). A System.Object containing a String array marshalled from
    /// a COM Object.
    /// </param>
    /// <param name="values">
    /// Values of independent variables.
    /// An array of doubles as a System.Object, which is marshalled as a Object
    /// COM-based CAPE-OPEN.
    /// </param>
    [Description("method SetIndependentVar")]
    [DispId(9)]
    void SetIndependentVar(object indVars, object values);

    /// <summary>Get the independent variable for the state</summary>
    /// <remarks>
    /// Sets the independent variable for a given Material Object.
    /// </remarks>
    /// <param name="indVars">
    /// Independent variables to be set (see names for state variables for list of
    /// valid variables). A System.Object containing a String array marshalled from
    /// a COM Object.
    /// </param>
    /// <returns>
    /// Values of independent variables.
    /// An array of doubles as a System.Object, which is marshalled as a Object
    /// COM-based CAPE-OPEN.
    /// </returns>
    [DispId(10)]
    [Description("method GetIndependentVar")]
    object GetIndependentVar(object indVars);

    /// <summary>Check a property is valid</summary>
    /// <remarks>Checks to see if given properties can be calculated.</remarks>
    /// <returns>
    /// Returns Boolean List associated to list of properties to be checked.
    /// An array of booleans (VT_BOOL) as a System.Object, which is marshalled as a
    /// Object COM-based CAPE-OPEN.
    /// </returns>
    /// <param name="props">
    /// Properties to check. A System.Object containing a String array marshalled from
    /// a COM Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("method PropCheck")]
    [DispId(11)]
    object PropCheck(object props);

    /// <summary>Check which properties are available</summary>
    /// <remarks>Gets a list properties that have been calculated.</remarks>
    /// <returns>
    /// Properties for which results are available.in a String array as a
    /// System.Object, which is marshalled as a Object COM-based CAPE-OPEN.
    /// </returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(12)]
    [Description("method AvailableProps")]
    object AvailableProps();

    /// <summary>
    /// Remove any previously calculated results for given properties
    /// </summary>
    /// <remarks>
    /// Remove all or specified property results in the Material Object.
    /// </remarks>
    /// <param name="props">
    /// Properties to be removed. emptyObject to remove all properties. A
    /// System.Object containing a String array marshalled from a COM Object.
    /// </param>
    [DispId(13)]
    [Description("method RemoveResults")]
    void RemoveResults(object props);

    /// <summary>Create another empty material object</summary>
    /// <remarks>
    /// Create a Material Object from the parent Material Template of the current
    /// Material Object. This is the same as using the CreateMaterialObject method
    /// on the parent Material Template.
    /// </remarks>
    /// <returns>The created/initialized Material Object.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">ECapeOutOfResources</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(14)]
    [Description("method CreateMaterialObject")]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object CreateMaterialObject();

    /// <summary>Duplicate this material object</summary>
    /// <remarks>Create a duplicate of the current Material Object.</remarks>
    /// <returns>The created/initialized Material Object.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">ECapeOutOfResources</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(15)]
    [Description("method Duplicate")]
    [return: MarshalAs(UnmanagedType.IDispatch)]
    object Duplicate();

    /// <summary>Check the validity of the given properties</summary>
    /// <remarks>Checks the validity of the calculation.</remarks>
    /// <returns>Returns the reliability scale of the calculation.</returns>
    /// <param name="props">
    /// The properties for which reliability is checked. emptyObject to remove all
    /// properties. A System.Object containing a String array marshalled from a COM
    /// Object.
    /// </param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(16)]
    [Description("method ValidityCheck")]
    object ValidityCheck(object props);

    /// <summary>Get the list of properties</summary>
    /// <remarks>
    /// Returns list of properties supported by the property package and corresponding
    /// CO Calculation Routines. The properties TEMPERATURE, PRESSURE, FRACTION, FLOW,
    /// PHASEFRACTION, TOTALFLOW cannot be returned by GetPropList, since all
    /// components must support them. Although the property identifier of derivative
    /// properties is formed from the identifier of another property, the GetPropList
    /// method will return the identifiers of all supported derivative and
    /// non-derivative properties. For instance, a Property Package could return
    /// the following list: enthalpy, enthalpy.Dtemperature, entropy, entropy.Dpressure.
    /// </remarks>
    /// <returns>
    /// String list of all supported properties of the property package.
    /// A System.Object containing a String array marshalled from a COM Object.
    /// </returns>
    [DispId(17)]
    [Description("method GetPropList")]
    object GetPropList();

    /// <summary>Get the number of components in this material object</summary>
    /// <remarks>Returns number of components in Material Object.</remarks>
    /// <returns>Number of components in the Material Object.</returns>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("method GetNumComponents")]
    [DispId(18)]
    int GetNumComponents();
  }
}
