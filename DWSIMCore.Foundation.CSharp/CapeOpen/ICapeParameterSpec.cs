// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeParameterSpec
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <remarks>Reference document: Parameter Common Interface</remarks>
  [ComVisible(false)]
  [Guid("678c099c-0093-11d2-a67d-00105a42887f")]
  [Description("ICapeParameterSpec Interface")]
  [ComImport]
  public interface ICapeParameterSpec
  {
    /// <summary>Gets the type of the parameter.</summary>
    /// <remarks>
    /// Gets the <see cref="T:CapeOpen.CapeParamType" /> of the parameter for which this is a specification: real
    /// (CAPE_REAL), integer(CAPE_INT), option(CAPE_OPTION), boolean(CAPE_BOOLEAN)
    /// or array(CAPE_ARRAY).
    /// </remarks>
    /// <value>The parameter type. </value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property Type")]
    [DispId(1)]
    CapeParamType Type { get; }

    /// <summary>Gets the dimensionality of the parameter.</summary>
    /// <remarks>
    /// <para>Gets the dimensionality of the parameter for which this is the
    /// specification. The dimensionality represents the physical dimensional
    /// axes of this parameter. It is expected that the dimensionality must cover
    /// at least 6 fundamental axes (length, mass, time, angle, temperature and
    /// charge). A possible implementation could consist in being a constant
    /// length array vector that contains the exponents of each basic SI unit,
    /// following directives of SI-brochure (from http://www.bipm.fr/). So if we
    /// agree on order &lt;m kg s A K,&gt; ... velocity would be
    /// &lt;1,0,-1,0,0,0&gt;: that is m1 * s-1 =m/s. We have suggested to the
    /// CO Scientific Committee to use the SI base units plus the SI derived units
    /// with special symbols (for a better usability and for allowing the
    /// definition of angles).</para>
    /// </remarks>
    /// <value>an integer array indicating the exponents of the various dimensional axes.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [Description("property Dimensionality")]
    [DispId(2)]
    object Dimensionality { get; }
  }
}
