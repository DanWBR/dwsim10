// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeUser
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The base interface of the CO errors hierarchy.</summary>
  /// <remarks>
  /// The ECapeUser interface defines the minimum state of a CO error.
  /// </remarks>
  [ComVisible(false)]
  [Guid("678C0B11-7D66-11D2-A67D-00105A42887F")]
  [Description("ECapeUser Interface")]
  [ComImport]
  public interface ECapeUser
  {
    /// <summary>Code to designate the subcategory of the error.</summary>
    /// <remarks>
    /// <para>The error code is used as the function return HRESULT in the COM calling pattern.
    /// When a .Net-based component throws an exception, the HRESULT assigned to the
    /// exception is returned to the COM-based caller. It is important to set the
    /// exception HRESULT value to provide HRESULT information to a COM caller.
    /// </para>
    /// <para>The assignment of values is left to each implementation. So that is a
    /// proprietary code specific to the CO component provider. By default, set to
    /// the CAPE-OPEN error HRESULT <see cref="T:CapeOpen.CapeErrorInterfaceHR" />.</para>
    /// </remarks>
    /// <value>The HRESULT value for the exception.</value>
    [DispId(1)]
    [Description("Code to designate the subcategory of the error. The assignment of values is left to each implementation. So that is a proprietary code specific to the CO component provider.")]
    int code { get; }

    /// <summary>The description of the error.</summary>
    /// <remarks>
    /// The error description can include a more verbose description of the condition that
    /// caused the error.
    /// </remarks>
    /// <value>A string description of the exception.</value>
    [Description("The description of the error.")]
    [DispId(2)]
    string description { get; }

    /// <summary>The scope of the error.</summary>
    /// <remarks>
    /// This property provides a list of packages where the error occurs separated by '.'.
    /// For example CapeOpen.Common.Identification.
    /// </remarks>
    /// <value>The source of the error.</value>
    [DispId(3)]
    [Description("The scope of the error. The list of packages where the error occurs separated by '.'. For example CapeOpen.Common.Identification.")]
    string scope { get; }

    /// <summary>
    /// The name of the interface where the error is thrown. This is a mandatory field."
    /// </summary>
    /// <remarks>The interface that the error was thrown.</remarks>
    /// <value>The name of the interface.</value>
    [DispId(4)]
    [Description("The name of the interface where the error is thrown. This is a mandatory field.")]
    string interfaceName { get; }

    /// <summary>
    /// The name of the operation where the error is thrown. This is a mandatory field.
    /// </summary>
    /// <remarks>
    /// This field provides the name of the operation being perfomed when the exception was raised.
    /// </remarks>
    /// <value>The operation name.</value>
    [Description("The name of the operation where the error is thrown. This is a mandatory field.")]
    [DispId(5)]
    string operation { get; }

    /// <summary>
    /// An URL to a page, document, web site,  where more information on the error can be found. The content of this information is obviously implementation dependent.
    /// </summary>
    /// <remarks>
    /// This field provides an internet URL where more information about the error can be found.
    /// </remarks>
    /// <value>The URL.</value>
    [Description("An URL to a page, document, web site,  where more information on the error can be found. The content of this information is obviously implementation dependent.")]
    [DispId(6)]
    string moreInfo { get; }
  }
}
