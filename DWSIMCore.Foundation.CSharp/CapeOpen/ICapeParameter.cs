// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeParameter
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Interface defining the actual Parameter quantity.</summary>
  [Description("ICapeParameter Interface")]
  [ComVisible(false)]
  [Guid("678c09a0-0093-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeParameter
  {
    /// <summary>Gets the Specification for this Parameter</summary>
    /// <remarks>
    /// Gets the specification of the parameter. The Get method returns the
    /// specification as an interface to the correct specification type.
    /// </remarks>
    /// <value>
    /// An object implementing the <see cref="T:CapeOpen.ICapeParameterSpec" />, as well as the
    /// appropraite specification for the parameter type, <see cref="T:CapeOpen.ICapeRealParameterSpec" /> ,
    /// <see cref="T:CapeOpen.ICapeIntegerParameterSpec" /> , <see cref="T:CapeOpen.ICapeBooleanParameterSpec" /> ,
    /// <see cref="T:CapeOpen.ICapeOptionParameterSpec" /> , or <see cref="T:CapeOpen.ICapeArrayParameterSpec" /> .
    /// </value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Gets and sets the specification for the parameter.")]
    [DispId(1)]
    object Specification { [return: MarshalAs(UnmanagedType.IDispatch)] get; }

    /// <summary>Gets and sets the value for this Parameter</summary>
    /// <remarks>
    /// Gets and sets the value of this parameter. Passed as a CapeVariant that
    /// should be the same type as the Parameter type.
    /// </remarks>
    /// <value>The boxed value of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Get and sets the value of the parameter.")]
    [DispId(2)]
    object value { get; set; }

    /// <summary>
    /// Gets the flag to indicate parameter validation's status.
    /// </summary>
    /// <remarks>
    /// <para>Gets the flag to indicate parameter validation status. It has three
    /// possible values:</para>
    /// <para>   (i)   notValidated(CAPE_NOT_VALIDATED): The PMC's <c>Validate()</c>
    /// method has not been called after the last time that its value had been
    /// changed.</para>
    /// <para>   (ii)  invalid(CAPE_INVALID): The last time that the PMC's
    /// <c>Validate()</c> method was called it returned false.</para>
    /// <para>   (iii) valid(CAPE_VALID): the last time that the PMC's
    /// Validate() method was called it returned true.</para>
    /// </remarks>
    /// <value>The validity staus of the parameter, either valid, invalid, or "not validated".</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Get the parameter validation status")]
    [DispId(3)]
    CapeValidationStatus ValStatus { get; }

    /// <summary>Gets and sets the mode of the parameter.</summary>
    /// <remarks>
    /// <para>Modes of parameters. It allows the following values:</para>
    /// <para>   (i)   Input (CAPE_INPUT): the Unit(or whichever owner component) will use
    /// its value to calculate.</para>
    /// <para>   (ii)  Output (CAPE_OUTPUT): the Unit will place in the parameter a result
    /// of its calculations.</para>
    /// <para>   (iii) Input-Output (CAPE_INPUT_OUTPUT): the user inputs an
    /// initial estimation value and the user outputs a calculated value.</para>
    /// </remarks>
    /// <value>The mode of the parameter, input, output, or input/output.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Get the Mode - input,output - of the parameter.")]
    [DispId(4)]
    CapeParamMode Mode { get; set; }

    /// <summary>
    /// Validates the current value of the parameter against the
    /// specification of the parameter.
    /// </summary>
    /// <remarks>
    /// This method checks the current value of the parameter to determine if it is an allowed value. In the case of
    /// numeric parameters (<see cref="T:CapeOpen.ICapeRealParameterSpec" /> and <see cref="T:CapeOpen.ICapeIntegerParameterSpec" />),
    /// the value is valid if it is between the upper and lower bound. For String (<see cref="T:CapeOpen.ICapeOptionParameterSpec" />),
    /// if the <see cref="P:CapeOpen.ICapeOptionParameterSpec.RestrictedToList" /> property is true, the value must be included as one of the
    /// members of the <see cref="P:CapeOpen.ICapeOptionParameterSpec.OptionList" />. Otherwise, any string value is valid. Any boolean value (true/false)
    /// valid for the <see cref="T:CapeOpen.ICapeBooleanParameterSpec" /> paramaters.
    /// </remarks>
    /// <returns>True if the parameter is valid, false if not valid.</returns>
    /// <param name="message">The message is used to return the reason that the parameter is invalid.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("Validate the parameter's current value.")]
    [DispId(5)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Validate(ref string message);

    /// <summary>Sets the value of the parameter to its default value.</summary>
    /// <remarks>This method sets the parameter to its default value.</remarks>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    [Description("Reset the value of the parameter to its default.")]
    [DispId(6)]
    void Reset();
  }
}
