// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeOptionParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface is for a parameter specification
  /// when the parameter is an option, which represents
  /// a list of strings from which one is selected.
  /// </summary>
  [Description("ICapeOptionParameterSpec Interface")]
  [ComVisible(false)]
  [Guid("678c099f-0093-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeOptionParameterSpec
  {
    /// <summary>Gets the default value of the parameter.</summary>
    /// <remarks>A default string value for the parameter.</remarks>
    /// <value>The default value of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(1)]
    [Description("property Default")]
    string DefaultValue { get; }

    /// <summary>
    /// Gets the list of valid values for the parameter if 'RestrictedtoList' property is true.
    /// </summary>
    /// <remarks>
    /// Used in validating the parameter if the <see cref="P:CapeOpen.ICapeOptionParameterSpec.RestrictedToList">RestrictedToList</see>
    /// is set to <c>true</c>.
    /// </remarks>
    /// <value>
    /// String array as a System.Object, COM Variant containing a SafeArray of BSTR.
    /// </value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("The list of names of the items")]
    object OptionList { get; }

    /// <summary>
    /// A list of Strings that the valueo f the parameter will be validated against.
    /// </summary>
    /// <remarks>
    /// If <c>true</c>, the parameter's value will be validated against the Strings
    /// in the <see cref="P:CapeOpen.ICapeOptionParameterSpec.OptionList">OptionList</see>.
    /// </remarks>
    /// <value>
    /// Converted by COM interop to a COM-based CAPE-OPEN VARIANT_BOOL.
    /// </value>
    [DispId(3)]
    [Description("True if it only accepts values from the option list.")]
    bool RestrictedToList { get; }

    /// <summary>
    /// Validates the value against the parameter's specification.
    /// </summary>
    /// <remarks>
    /// If the value of the <see cref="P:CapeOpen.ICapeOptionParameterSpec.RestrictedToList">RestrictedToList</see>
    /// is set to <c>true</c>, the value is valid is valid value for the
    /// parameter if it is included in the
    /// <see cref="P:CapeOpen.ICapeOptionParameterSpec.OptionList">OptionList</see>. If the
    /// value of <see cref="P:CapeOpen.ICapeOptionParameterSpec.RestrictedToList">RestrictedToList</see> is <c>false</c>
    /// any valid String is a valid value for the parameter.
    /// </remarks>
    /// <returns>True if the parameter is valid, false if not valid.</returns>
    /// <param name="value">A candidate value for the parameter to be tested to determine whether the value is valid.</param>
    /// <param name="message">Reference to a string that will conain a message regarding the validation of the parameter.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(4)]
    [Description("Check if value is OK for this spec as string")]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Validate(string value, ref string message);
  }
}
