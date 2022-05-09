// Decompiled with JetBrains decompiler
// Type: CapeOpen.ICapeDiagnostic
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>
  /// Provides a mechanism to provide verbose messages to the user.
  /// </summary>
  /// <remarks>
  ///  The communication of verbose information from the PMC to the PME (and hence to the
  /// user). PMCs should be able to log or display information to the user while it is executing
  ///  a flowsheet. Rather than each PMC performing these tasks by the means of different
  ///  mechanisms, it is much preferable to redirect them all to the PME services for
  ///  communicating with the user. The Error Common Interfaces do not fulfil these requirements,
  ///  since they stop the execution of the PMC code and signal an abnormal situation to the PME.
  ///  The document deals with the transferral of simple informative or warning messages.
  ///  </remarks>
  [Guid("678c0a9d-0100-11d2-a67d-00105a42887f")]
  [Description("ICapeDiagnostic Interface")]
  [ComVisible(false)]
  [ComImport]
  public interface ICapeDiagnostic
  {
    /// <summary>Writes a message to the terminal.</summary>
    /// <remarks>
    /// <para>Write a string to the terminal.</para>
    /// <para>This method is called when a message needs to be brought to the user’s attention.
    /// The implementation should ensure that the string is written out to a dialogue box or
    /// to a message list that the user can easily see.</para>
    /// <para>A priori this message has to be displayed as soon as possible to the user.</para>
    /// </remarks>
    /// <param name="message">The text to be displayed.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(1)]
    [Description("method PopUpMessage")]
    void PopUpMessage(string message);

    /// <summary>Writes a string to the PME's log file.</summary>
    /// <remarks>
    /// <para>Write a string to a log.</para>
    /// <para>This method is called when a message needs to be recorded for logging purposes.
    /// The implementation is expected to write the string to a log file or other journaling
    /// device.</para>
    /// </remarks>
    /// <param name="message">The text to be logged.</param>
    /// <exception cref="T:CapeOpen.ECapeUnknown">The error to be raised when other error(s),  specified for this operation, are not suitable.</exception>
    /// <exception cref="T:CapeOpen.ECapeInvalidArgument">To be used when an invalid argument value is passed, for example, an unrecognised Compound identifier or UNDEFINED for the props argument.</exception>
    [DispId(2)]
    [Description("method LogMessage")]
    void LogMessage(string message);
  }
}
