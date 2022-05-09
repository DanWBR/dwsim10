// Decompiled with JetBrains decompiler
// Type: CapeOpen.CapeSolutionStatus
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>Indicates solution status of the monitored flowsheet.</summary>
  /// <remarks>
  /// This enumeration provides the flowsheeting monitoring object with information about the solution status of the flowsheet.
  /// </remarks>
  [ComVisible(false)]
  [Guid("D1B15843-C0F5-4CB7-B462-E1B80456808E")]
  [Serializable]
  public enum CapeSolutionStatus
  {
    /// <summary>The flowsheet solved without error.</summary>
    CAPE_SOLVED,
    /// <summary>
    /// Signifies that there has been no attempt to solve the flowsheet.
    /// </summary>
    CAPE_NOT_SOLVED,
    /// <summary>
    /// The last attempt to solve the flowsheet did not converge.
    /// </summary>
    CAPE_FAILED_TO_CONVERGE,
    /// <summary>The last attempt to solve the flowsheet timed out.</summary>
    CAPE_TIMED_OUT,
    /// <summary>
    /// The last attempt to solve the flowsheet failed to solve due to lack of memory.
    /// </summary>
    CAPE_NO_MEMORY,
    /// <summary>
    /// The last attempt to solve the flowsheet failed to initialize.
    /// </summary>
    CAPE_FAILED_INITIALIZATION,
    /// <summary>
    /// The last attempt to solve the flowsheet produced a solving error.
    /// </summary>
    CAPE_SOLVING_ERROR,
    /// <summary>
    /// The last attempt to solve the flowsheet failed due to an invalid operation.
    /// </summary>
    CAPE_INVALID_OPERATION,
    /// <summary>
    /// The last attempt to solve the flowsheet failed due to an invalid invocation order.
    /// </summary>
    CAPE_BAD_INVOCATION_ORDER,
    /// <summary>
    /// The last attempt to solve the flowsheet produced a computation error.
    /// </summary>
    CAPE_COMPUTATION_ERROR,
  }
}
