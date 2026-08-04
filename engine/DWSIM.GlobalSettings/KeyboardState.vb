''' <summary>
''' Cross-platform replacement for My.Computer.Keyboard. The UI host (WinForms, Eto,
''' Avalonia, ...) is responsible for updating these flags whenever modifier keys change.
''' The engine reads them to make selection / multi-select decisions without depending on
''' the VisualBasic Forms runtime (which is Windows-only on .NET 5+).
''' </summary>
Public Class KeyboardState

    Public Shared Property IsShiftDown As Boolean = False
    Public Shared Property IsCtrlDown As Boolean = False
    Public Shared Property IsAltDown As Boolean = False

    ''' <summary>Convenience setter for hosts that already have modifier state as flags.</summary>
    Public Shared Sub SetState(shiftDown As Boolean, ctrlDown As Boolean, altDown As Boolean)
        IsShiftDown = shiftDown
        IsCtrlDown = ctrlDown
        IsAltDown = altDown
    End Sub

End Class
