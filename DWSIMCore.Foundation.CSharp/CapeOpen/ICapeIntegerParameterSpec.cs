// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeIntegerParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface is for a parameter specification
  /// when the parameter is an integer value.
  /// </summary>
  [Description("ICapeIntegerParameterSpec Interface")]
  [ComVisible(false)]
  [Guid("678c099e-0093-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeIntegerParameterSpec
  {
    /// <summary>Gets the default value of the parameter.</summary>
    /// <remarks>A default value for the parameter.</remarks>
    /// <value>The default value of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property Default")]
    [DispId(1)]
    int DefaultValue { get; }

    /// <summary>Gets the lower bound of the parameter.</summary>
    /// <remarks>A lower bound value for the parameter.</remarks>
    /// <value>The lower bound of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property LowerBound")]
    [DispId(2)]
    int LowerBound { get; }

    /// <summary>Gets the upper bound of the parameter.</summary>
    /// <remarks>A upper bound value for the parameter.</remarks>
    /// <value>The upper bound of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(3)]
    [Description("property UpperBound")]
    int UpperBound { get; }

    /// <summary>
    /// Validates the value sent against the specification of the parameter.
    /// </summary>
    /// <remarks>
    /// The parameter is considered valid if the current value is between
    /// the upper and lower bound. The message is used to return the reason
    /// that the parameter is invalid.
    /// </remarks>
    /// <returns>True if the parameter is valid, false if not valid.</returns>
    /// <param name="value">Integer value that will be validated against the parameter's current specification.</param>
    /// <param name="message">Reference to a string that will conain a message regarding the validation of the parameter.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(4)]
    [Description("Check if value is OK for this spec as double")]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Validate(int value, ref string message);
  }
}
