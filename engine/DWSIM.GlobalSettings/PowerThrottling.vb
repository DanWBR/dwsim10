Imports System.Runtime.InteropServices

''' <summary>
''' Windows 11 runs the threads of a process whose windows are not in front on the efficiency cores (EcoQoS).
''' A dynamic run there went 3 to 5 times slower (2.2 to 3.6 s per 1 s step against 0.5 to 0.7 s on an
''' i9-13900KF), so the process opts out the first time the flowsheet solver runs and stays out.
''' </summary>
Public Class PowerThrottling

    <StructLayout(LayoutKind.Sequential)>
    Private Structure ProcessPowerThrottlingState
        Public Version As UInteger
        Public ControlMask As UInteger
        Public StateMask As UInteger
    End Structure

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SetProcessInformation(process As IntPtr, infoClass As Integer, ByRef info As ProcessPowerThrottlingState, size As Integer) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentProcess() As IntPtr
    End Function

    Private Shared _done As Boolean

    ''' <summary>Keeps the process off EcoQoS (Windows 10 1709 and later; nothing elsewhere). Only the first call acts.</summary>
    Public Shared Sub KeepFullSpeed()
        If _done Then Return
        _done = True
        If Environment.OSVersion.Platform <> PlatformID.Win32NT Then Return
        Try
            'control the execution-speed throttling, with its state off: never throttled
            Dim state As New ProcessPowerThrottlingState With {.Version = 1, .ControlMask = 1, .StateMask = 0}
            SetProcessInformation(GetCurrentProcess(), 4, state, Marshal.SizeOf(state))
        Catch
            'Windows before 8 has no such setting
        End Try
    End Sub

End Class
