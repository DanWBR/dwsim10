// Decompiled with JetBrains decompiler
// Type: CapeOpen.ECapeOutsideSolverScope
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Exception thrown when the problem is outside the scope of the solver.
  /// </summary>
  /// <remarks>
  /// Exception thrown when the problem is outside the scope of the solver.
  /// </remarks>
  [Guid("678c0b0f-7d66-11d2-a67d-00105a42887f")]
  [Description("ECapeOutsideSolverScope Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ECapeOutsideSolverScope
  {
    /// <summary>Code to designate the subcategory of the error.</summary>
    /// <remarks>
    /// The assignment of values is left to each implementation. So that is a
    /// proprietary code specific to the CO component provider. By default, set to
    /// the CAPE-OPEN error HRESULT <see cref="T:CapeOpen.CapeErrorInterfaceHR" />.
    /// </remarks>
    /// <value>The HRESULT value for the exception.</value>
    [Description("Code to designate the subcategory of the error. The assignment of values is left to each implementation. So that is a proprietary code specific to the CO component provider.")]
    [DispId(1)]
    int code { get; }

    /// <summary>The description of the error.</summary>
    /// <remarks>
    /// The error description can include a more verbose description of the condition that
    /// caused the error.
    /// </remarks>
    /// <value>A string description of the exception.</value>
    [DispId(2)]
    [Description("The description of the error.")]
    string description { get; }

    /// <summary>The scope of the error.</summary>
    /// <remarks>
    /// This property provides a list of packages where the error occurs separated by '.'.
    /// For example CapeOpen.Common.Identification.
    /// </remarks>
    /// <value>The source of the error.</value>
    [Description("The scope of the error. The list of packages where the error occurs separated by '.'. For example CapeOpen.Common.Identification.")]
    [DispId(3)]
    string scope { get; }

    /// <summary>
    /// The name of the interface where the error is thrown. This is a mandatory field."
    /// </summary>
    /// <remarks>The interface that the error was thrown.</remarks>
    /// <value>The name of the interface.</value>
    [Description("The name of the interface where the error is thrown. This is a mandatory field.")]
    [DispId(4)]
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
    [DispId(6)]
    [Description("An URL to a page, document, web site,  where more information on the error can be found. The content of this information is obviously implementation dependent.")]
    string moreInfo { get; }
  }
}
