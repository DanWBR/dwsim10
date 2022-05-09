// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeBooleanParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface is for a parameter specification when the parameter is a boolean
  /// </summary>
  [ComVisible(false)]
  [Description("ICapeBooleanParameterSpec Interface")]
  [Guid("678c09a8-0093-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeBooleanParameterSpec
  {
    /// <summary>Gets the default value of the parameter.</summary>
    /// <remarks>Gets the default value of the parameter.</remarks>
    /// <value>The default value of the parameter.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property Default")]
    [DispId(1)]
    bool DefaultValue { get; }

    /// <summary>
    /// Validates the value sent against the specification of the parameter.
    /// </summary>
    /// <remarks>
    /// Validates whether the argument is accepted by the parameter as a valid value.
    /// It returns a flag to indicate the success or failure of the validation together
    /// with a text message which can be used to convey the reasoning to the client/user.
    /// </remarks>
    /// <returns>True if the parameter is valid, false if not valid.</returns>
    /// <param name="value">Boolean value that will be validated against the parameter's current specification.</param>
    /// <param name="message">Reference to a string that will conain a message regarding the validation of the parameter.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("Check if value is OK for this spec")]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Validate(bool value, ref string message);
  }
}
