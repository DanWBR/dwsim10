// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeUnit
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// This interface handles most of the interaction with the Flowsheet Unit.
  /// </summary>
  /// <remarks>
  /// This interface provides the basic funcational requirements for a unit operation
  /// component that can be inserted into a flowsheeting package.
  /// </remarks>
  [Description("ICapeUnit Interface")]
  [ComVisible(false)]
  [Guid("678c0998-0100-11d2-a67d-00105a42887f")]
  [ComImport]
  public interface ICapeUnit
  {
    /// <summary>Gets the collection of unit operation ports.</summary>
    /// <remarks>
    /// <para>Return an interface to a collection containing the list of unit ports (e.g.
    /// <see name="ICapeCollection" />).</para>
    /// <para>Return the collection of unit ports (i.e. ICapeUnitCollection). These are
    /// delivered as a collection of elements exposing the interfaces <see name="ICapeUnitPort" />
    /// </para>
    /// </remarks>
    /// <value>The port collection of the unit operation.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeFailedInitialisation">ECapeFailedInitialisation</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">ECapeBadInvOrder</exception>
    [Description("Gets the whole list of ports")]
    [DispId(1)]
    object ports { [return: MarshalAs(UnmanagedType.IDispatch)] get; }

    /// <summary>
    /// Gets the flag to indicate the unit operation's validation status
    /// <see cref="T:CapeOpen.CapeValidationStatus">CapeValidationStatus</see>.
    /// </summary>
    /// <remarks>
    /// <para>Get the flag that indicates whether the Flowsheet Unit is valid (e.g. some
    /// parameter values have changed but they have not been validated by using Validate).
    /// It has three possible values:</para>
    /// <para>   (i)   notValidated(CAPE_NOT_VALIDATED): The PMC's <c>Validate()</c>
    /// method has not been called after the last time that its value had been
    /// changed.</para>
    /// <para>   (ii)  invalid(CAPE_INVALID): The last time that the PMC's
    /// <c>Validate()</c> method was called it returned false.</para>
    /// <para>   (iii) valid(CAPE_VALID): the last time that the PMC's
    /// Validate() method was called it returned true.</para>
    /// </remarks>
    /// <value>A flag that indiciates the validation status of the unit operation.</value>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("Get the unit's validation status")]
    CapeValidationStatus ValStatus { get; }

    /// <summary>
    /// Executes the necessary calculations involved in the unit operation model.
    /// </summary>
    /// <remarks>
    /// <para>The Flowsheet Unit performs its calculation, that is, computes the variables
    /// that are missing at this stage in the complete description of the input and output
    /// streams and computes any public parameter value that needs to be displayed. Calculate
    /// will be able to do progress monitoring and checks for interrupts as required using
    /// the simulation context. At present, there are no standards agreed for this.</para>
    /// <para>It is recommended that Flowsheet Units perform a suitable flash calculation on
    /// all output streams. In some cases a Simulation Executive will be able to perform a
    /// flash calculation but the writer of a Flowsheet Unit is in the best position to
    /// decide the correct flash to use. </para>
    /// <para>Before performing the calculation, this method should perform any final
    /// validation tests that are required. For example, at this point the validity of
    /// Material Objects connected to ports can be checked.</para>
    /// <para>There are no input or output arguments for this method.</para>
    /// </remarks>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">ECapeBadInvOrder</exception>
    /// <exception cref="T:CapeOpen.ECapeOutOfResources">ECapeOutOfResources</exception>
    /// <exception cref="T:CapeOpen.ECapeTimeOut">ECapeTimeOut</exception>
    /// <exception cref="T:CapeOpen.ECapeSolvingError">ECapeSolvingError</exception>
    /// <exception cref="T:CapeOpen.ECapeLicenceError">ECapeLicenceError</exception>
    [DispId(3)]
    [Description("Performs unit calculations")]
    void Calculate();

    /// <summary>
    /// Validate the unit operation to verify that the parameters and ports are
    /// all valid. If invalid, this method returns a message indicating the
    /// reason that the unit is invalid.
    /// </summary>
    /// <remarks>
    /// <para>Sets the flag that indicates whether the Flowsheet Unit is valid by validating
    /// the ports and parameters of the Flowsheet Unit. For example, this method could check
    /// that all mandatory ports have connections and that the values of all parameters are
    /// within bounds.</para>
    /// <para>Note that the Simulation Executive can call the Validate routine at any time,
    /// in particular it may be called before the executive is ready to call the Calculate
    /// method. This means that Material Objects connected to unit ports may not be correctly
    /// configured when Validate is called. The recommended approach is for this method to
    /// validate parameters and ports but not Material Object configuration. A second level
    /// of validation to check Material Objects can be implemented as part of Calculate, when
    /// it is reasonable to expect that the Material Objects connected to ports will be
    /// correctly configured.</para>
    /// </remarks>
    /// <returns>
    /// <para>true, if the unit is valid.</para>
    /// <para>false, if the unit is not valid.</para>
    /// </returns>
    /// <param name="message">Reference to a string that will conain a message regarding the validation of the parameter.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeBadCOParameter">ECapeBadCOParameter</exception>
    /// <exception cref="T:CapeOpen.ECapeBadInvOrder">ECapeBadInvOrder</exception>
    [DispId(4)]
    [Description("Validate the Unit")]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool Validate(ref string message);
  }
}
