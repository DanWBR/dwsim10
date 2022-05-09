// Decompiled with JetBrains decompiler
// Type: CapeOpen.IUnitOperationValidatedEventArgs
// Assembly: CapeOpen, Version=1.0.0.0, Culture=neutral, PublicKeyToken=90d5303f0e924b64
// MVID: 4E76E984-6CC8-4C13-98A0-D3FBAF2DEB87
// Assembly location: C:\Users\Daniel\source\repos\DanWBR\dwsim\DistPackages\Windows\CapeOpen.dll

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CapeOpen
{
  /// <summary>The unit operation was validated.</summary>
  /// <remarks>
  /// Provides information about the validation of the unit operation.
  /// </remarks>
  [Description("IUnitOperationValidatedEventArgs Interface")]
  [ComVisible(true)]
  [Guid("50A759AF-5E38-4399-9050-93F823E5A6E6")]
  public interface IUnitOperationValidatedEventArgs
  {
    /// <summary>The name of the unit operation being changed.</summary>
    string UnitOperationName { get; }

    /// <summary>The message reulting from the unit operation validation.</summary>
    /// <remarks>The message provides information about the results of the validation process.</remarks>
    /// <value>Information regrading the validation process.</value>
    string Message { get; }

    /// <summary>
    /// The validation status of the unit operation prior to the validation.</summary>
    /// <remarks>Informs the user of the results of the validation process.</remarks>
    /// <value>The validation status of the unit operation prior to the validation.</value>
    CapeValidationStatus OldStatus { get; }

    /// <summary>The validation status of the unit operation after the validation.</summary>
    /// <remarks>Informs the user of the results of the validation process.</remarks>
    /// <value>The validation status of the unit operation after the validation.</value>
    CapeValidationStatus NewStatus { get; }
  }
}
