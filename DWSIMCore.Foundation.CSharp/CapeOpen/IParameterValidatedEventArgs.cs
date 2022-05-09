// Decompiled with JetBrains decompiler
// Type: CapeOpen.IParameterValidatedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The parameter was validated.</summary>
  /// <remarks>
  /// Provides information about the validation of the parameter.
  /// </remarks>
  [Guid("EFD819A4-E4EC-462E-90E6-5D994CA44F8E")]
  [ComVisible(true)]
  [Description("ParameterValidatedEvent Interface")]
  public interface IParameterValidatedEventArgs
  {
    /// <summary>The name of the parameter being changed.</summary>
    string ParameterName { get; }

    /// <summary>The message reulting from the parameter validation.</summary>
    /// <remarks>The message provides information about the results of the validation process.</remarks>
    /// <value>Information regrading the validation process.</value>
    string Message { get; }

    /// <summary>The validation status of the parameter prior to the validation.</summary>
    /// <remarks>Informs the user of the results of the validation process.</remarks>
    /// <value>The validation status of the parameter prior to the validation.</value>
    CapeValidationStatus OldStatus { get; }

    /// <summary>The validation status of the parameter after the validation.</summary>
    /// <remarks>Informs the user of the results of the validation process.</remarks>
    /// <value>The validation status of the parameter after the validation.</value>
    CapeValidationStatus NewStatus { get; }
  }
}
