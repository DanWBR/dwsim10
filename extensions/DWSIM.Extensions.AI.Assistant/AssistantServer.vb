'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Threading.Tasks
Imports DWSIM.Interfaces

''' <summary>
''' Starts and stops the two local servers behind the assistant: the flowsheet HTTP bridge on port
''' 5002, and the separate assistant program on port 5834. Holds the per-session auth token both
''' sides share.
''' </summary>
Friend Module ReportExportHelper

    ' the flowsheet HTTP bridge this process owns
    Private _ownedServer As Server = Nothing

    ' the assistant program this process started, so it can be stopped with us
    Private _serverProcess As System.Diagnostics.Process = Nothing

    ' one warm-up per host process
    Private _warmup As Task = Nothing

    ' Per-session auth token, exported as DWSIM_ASSISTANT_TOKEN to the assistant program and required
    ' on every call back to http://localhost:5834. Defense in depth on top of the 127.0.0.1 bind.
    Private ReadOnly _assistantToken As String = InitToken()

    Private Function InitToken() As String
        ' reuse the token the host already minted, if any, so both sides agree
        Dim existing As String = Nothing
        Try
            existing = Environment.GetEnvironmentVariable(
                "DWSIM_ASSISTANT_TOKEN", EnvironmentVariableTarget.Process)
        Catch
        End Try
        If Not String.IsNullOrEmpty(existing) Then Return existing

        Dim t As String = Guid.NewGuid().ToString("N")
        Try
            Environment.SetEnvironmentVariable("DWSIM_ASSISTANT_TOKEN", t, EnvironmentVariableTarget.Process)
        Catch
        End Try
        Return t
    End Function

    ''' <summary>The auth token expected by the assistant program on port 5834.</summary>
    Friend ReadOnly Property AssistantToken As String
        Get
            Return _assistantToken
        End Get
    End Property

    ''' <summary>Returns True when something is already listening on <paramref name="port"/>.</summary>
    Friend Function IsPortListening(port As Integer) As Boolean
        Try
            Using client As New System.Net.Sockets.TcpClient()
                client.Connect("localhost", port)
            End Using
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>Starts the flowsheet HTTP bridge on port 5002, unless it is already listening.</summary>
    Friend Sub EnsureApiServerRunning(flowsheet As IFlowsheet)
        If IsPortListening(5002) Then Return

        If _ownedServer IsNot Nothing Then
            Try
                _ownedServer.StopServer()
            Catch
            End Try
        End If

        _ownedServer = New Server() With {.Flowsheet = flowsheet}
        _ownedServer.StartServer()
    End Sub

    ''' <summary>Stops the flowsheet HTTP bridge this process started, if any.</summary>
    Friend Sub ReleaseOwnedServer()
        If _ownedServer IsNot Nothing Then
            Try
                _ownedServer.StopServer()
            Catch
            End Try
            _ownedServer = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Ensures the assistant program is reachable on port 5834. If it is not, finds the binary next
    ''' to the extension (or one directory up) and launches it in the background.
    ''' </summary>
    ''' <returns>True when the server is ready; False when it could not be started.</returns>
    Friend Function EnsurePythonServerRunning() As Boolean
        If IsPortListening(5834) Then Return True

        Dim baseDir As String = Path.GetDirectoryName(
            Reflection.Assembly.GetExecutingAssembly().Location)

        ' the binary is named dwsim-assistant.exe on Windows and dwsim-assistant elsewhere; the
        ' packaged layout puts it in AIAssistantFiles beside the extension, the flat candidates cover
        ' a development tree
        Dim exeName As String = If(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                                   "dwsim-assistant.exe", "dwsim-assistant")

        Dim candidates As String() = {
            Path.Combine(baseDir, "AIAssistantFiles", exeName),
            Path.Combine(baseDir, exeName),
            Path.Combine(Path.GetDirectoryName(baseDir), "AIAssistantFiles", exeName),
            Path.Combine(Path.GetDirectoryName(baseDir), exeName)
        }
        Dim exePath As String = candidates.FirstOrDefault(Function(p) File.Exists(p))
        If exePath Is Nothing Then Return False

        Dim psi As New System.Diagnostics.ProcessStartInfo(exePath) With {
            .WorkingDirectory = Path.GetDirectoryName(exePath),
            .WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            .UseShellExecute = False
        }
        ' UseShellExecute = False is required for EnvironmentVariables to take effect.
        psi.EnvironmentVariables("DWSIM_ASSISTANT_TOKEN") = _assistantToken
        _serverProcess = System.Diagnostics.Process.Start(psi)

        ' poll up to 20 s for the server to become ready
        Dim sw = System.Diagnostics.Stopwatch.StartNew()
        While sw.ElapsedMilliseconds < 20000
            System.Threading.Thread.Sleep(500)
            If IsPortListening(5834) Then Return True
        End While
        Return False
    End Function

    ''' <summary>Starts the assistant server in the background, once, so it answers by first use.</summary>
    Friend Sub WarmUp()

        If _warmup IsNot Nothing Then Return

        _warmup = Task.Run(Sub()
                               Try
                                   EnsurePythonServerRunning()
                               Catch
                               End Try
                           End Sub)

    End Sub

    ''' <summary>Stops the assistant server, when this process is the one that started it.</summary>
    Friend Sub StopPythonServer()

        Try
            If _serverProcess IsNot Nothing AndAlso Not _serverProcess.HasExited Then
                _serverProcess.Kill()
            End If
        Catch
        End Try

        _serverProcess = Nothing

    End Sub

End Module
