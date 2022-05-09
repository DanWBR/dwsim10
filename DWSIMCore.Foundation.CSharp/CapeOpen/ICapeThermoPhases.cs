// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeThermoPhases
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides information about the number and types of Phases supported by
  /// the component that implements it.
  /// </summary>
  /// <remarks>This interface is designed to provide information about the number and
  /// types of Phases supported by the component that implements it. It defines all the
  /// Phases that a component such as a Physical Property Calculator can handle. It
  /// does not provide information about the Phases that are actually present in a
  /// Material Object. This function is provided by the Get-PresentPhases method of the
  /// ICapeThermoMaterial interface.</remarks>
  [ComVisible(false)]
  [Description("ICapeThermoPhases Interface")]
  [Guid("678C0A9E-7D66-11D2-A67D-00105A42887F")]
  [ComImport]
  public interface ICapeThermoPhases
  {
    /// <summary>Returns the number of Phases.</summary>
    /// <returns>The number of Phases supported.</returns>
    /// <remarks>The number of Phases returned by this method must be equal to the
    /// number of Phase labels that are returned by the GetPhaseList method of this
    /// interface. It must be zero, or a positive number.</remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists, but it is not supported
    /// by the current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [DispId(1)]
    [Description("method GetNumPhases")]
    int GetNumPhases();

    /// <summary>Returns information on an attribute associated with a Phase for the
    /// purpose of understanding what lies behind a Phase label.</summary>
    /// <param name="phaseLabel">A (single) Phase label. This must be one of the
    /// values returned by GetPhaseList method.</param>
    /// <param name="phaseAttribute">One of the Phase attribute identifiers from the
    /// table below.</param>
    /// <returns>The value corresponding to the Phase attribute identifier – see
    /// table below.</returns>
    /// <remarks>
    /// <para>GetPhaseInfo is intended to allow a PME, or other client, to identify a
    /// Phase with an arbitrary label. A PME, or other client, will need to do this
    /// to map stream data into a Material Object, or when importing a Property
    /// Package. If the client cannot identify the Phase, it can ask the user to
    /// provide a mapping based on the values of these properties.</para>
    /// <para>The list of supported Phase attributes is defined in the following
    /// table:</para>
    /// <para>For example, the following information might be returned by a Property
    /// Package component that supports a vapour Phase, an organic liquid Phase and
    /// an aqueous liquid Phase:
    /// Phase label Gas Organic Aqueous
    /// StateOfAggregation Vapor Liquid Liquid
    /// KeyCompoundId UNDEFINED UNDEFINED Water
    /// ExcludedCompoundId UNDEFINED Water UNDEFINED
    /// DensityDescription UNDEFINED Light Heavy
    /// UserDescription The gas Phase The organic liquid
    /// Phase
    /// The aqueous liquid
    /// Phase
    /// TypeOfSolid UNDEFINED UNDEFINED UNDEFINED</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if
    /// this method can be called for reasons of compatibility with the CAPE-OPEN
    /// standards. That is to say that the operation exists but it is not supported
    /// by the current implementation..</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument"> – phaseLabel is not recognised, or
    /// UNDEFINED, or phaseAttribute is not recognised.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable..</exception>
    [Description("method GetPhaseInfo")]
    [DispId(2)]
    object GetPhaseInfo([In] string phaseLabel, [In] string phaseAttribute);

    /// <summary>
    /// Returns Phase labels and other important descriptive information for all the
    /// Phases supported.
    /// </summary>
    /// <param name="phaseLabels">The list of Phase labels for the Phases supported.
    /// A Phase label can be any string but each Phase must have a unique label. If,
    /// for some reason, no Phases are supported an UNDEFINED value should be returned
    /// for the phaseLabels. The number of Phase labels must also be equal to the
    /// number of Phases returned by the GetNumPhases method.
    /// </param>
    /// <param name="stateOfAggregation">The physical State of Aggregation associated
    /// with each of the Phases. This must be one of the following strings: ”Vapor”,
    /// “Liquid”, “Solid” or “Unknown”. Each Phase must have a single State of
    /// Aggregation. The value must not be left undefined, but may be set to “Unknown”.
    /// </param>
    /// <param name="keyCompoundId">The key Compound for the Phase. This must be the
    /// Compound identifier (as returned by GetCompoundList), or it may be undefined
    /// in which case a UNDEFINED value is returned. The key Compound is an indication
    /// of the Compound that is expected to be present in high concentration in the
    /// Phase, e.g. water for an aqueous liquid phase. Each Phase can have a single
    /// key Compound.
    /// </param>
    /// <remarks>
    /// <para>The Phase label allows the phase to be uniquely identified in methods of
    /// the ICapeThermoPhases interface and other CAPE-OPEN interfaces. The State of
    /// Aggregation and key Compound provide a way for the PME, or other client, to
    /// interpret the meaning of a Phase label in terms of the physical characteristics
    /// of the Phase.</para>
    /// <para>All arrays returned by this method must be of the same length, i.e.
    /// equal to the number of Phase labels.</para>
    /// <para>To get further information about a Phase, use the GetPhaseInfo method.
    /// </para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeNoImpl">The operation is “not” implemented even if this
    /// method can be called for reasons of compatibility with the CAPE-OPEN standards.
    /// That is to say that the operation exists, but it is not supported by the
    /// current implementation.</exception>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),
    /// specified for this operation, are not suitable.</exception>
    [Description("method GetPhaseList")]
    [DispId(3)]
    void GetPhaseList(
      [In, Out] ref object phaseLabels,
      [In, Out] ref object stateOfAggregation,
      [In, Out] ref object keyCompoundId);
  }
}
