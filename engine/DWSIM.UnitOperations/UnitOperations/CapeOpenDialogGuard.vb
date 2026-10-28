'    Closes the error dialogs a CAPE-OPEN unit operation's solver process shows while it calculates
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

Imports System.Diagnostics
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading

Namespace UnitOperations

    ''' <summary>
    ''' Watches, while a CAPE-OPEN unit operation calculates, for windows shown by the processes DWSIM started (ChemSep
    ''' runs its solver, wincs.exe, as a child process, and a failed solve stops on a message box until someone clicks
    ''' OK), and for the message boxes the calculating thread itself opens (ChemSep's CAPE-OPEN library asks to retry or
    ''' cancel when its solver stops answering). Each such window is read and closed, which is a click on OK or Cancel; a
    ''' child process that keeps its window is ended, and a message box of DWSIM's own that stays open is cancelled. What
    ''' the windows said is kept in <see cref="Dialogs"/>. Windows only; elsewhere it does nothing.
    ''' </summary>
    Friend NotInheritable Class CapeOpenDialogGuard

        Implements IDisposable

        Private ReadOnly _dialogs As New List(Of String)
        Private ReadOnly _closedAt As New Dictionary(Of IntPtr, DateTime)
        Private ReadOnly _stop As New ManualResetEventSlim(False)
        Private ReadOnly _thread As Thread
        Private ReadOnly _self As Integer
        ''' <summary>The native thread that calculates the unit, whose message boxes are the unit's.</summary>
        Private ReadOnly _caller As UInteger

        ''' <summary>How often the windows are looked for, in milliseconds.</summary>
        Private Const Interval As Integer = 200
        ''' <summary>How long a process may keep a window after it was asked to close it, before it is ended.</summary>
        Private Const Grace As Double = 3.0

        Public Sub New()
            _self = Process.GetCurrentProcess().Id
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then Return
            _caller = GetCurrentThreadId()
            _thread = New Thread(AddressOf Watch) With {.IsBackground = True, .Name = "CAPE-OPEN dialog guard"}
            _thread.Start()
        End Sub

        ''' <summary>The title and text of each window that was closed, in the order they appeared.</summary>
        Public ReadOnly Property Dialogs As List(Of String)
            Get
                SyncLock _dialogs
                    Return _dialogs.ToList()
                End SyncLock
            End Get
        End Property

        Private Sub Watch()
            Do
                Try
                    Sweep()
                Catch ex As Exception
                    ' a failed look is tried again at the next interval
                End Try
            Loop Until _stop.Wait(Interval)
            ' a last look, for a window that opened just before the calculation returned
            Try
                Sweep()
            Catch ex As Exception
            End Try
        End Sub

        Private Sub Sweep()
            Dim children = Descendants(_self)
            For Each hwnd In VisibleWindowsOf(children, CUInt(_self), _caller)
                Dim pid As UInteger
                GetWindowThreadProcessId(hwnd, pid)
                Dim own = pid = CUInt(_self)
                If _closedAt.ContainsKey(hwnd) Then
                    If (DateTime.UtcNow - _closedAt(hwnd)).TotalSeconds > Grace Then
                        If own Then
                            ' DWSIM's own process is never ended: the message box gets its Cancel button
                            PostMessage(hwnd, WM_COMMAND, New IntPtr(IDCANCEL), IntPtr.Zero)
                        Else
                            Try
                                Process.GetProcessById(CInt(pid)).Kill()
                            Catch ex As Exception
                            End Try
                        End If
                    End If
                    Continue For
                End If
                ' the solver's own main window, titled with its name, is closed without a message
                Dim text = Describe(hwnd)
                Dim exe = ""
                Try
                    exe = Process.GetProcessById(CInt(pid)).ProcessName
                Catch ex As Exception
                End Try
                If text.Length > 0 AndAlso Not text.Equals(exe, StringComparison.OrdinalIgnoreCase) Then
                    SyncLock _dialogs
                        _dialogs.Add(text)
                    End SyncLock
                End If
                _closedAt(hwnd) = DateTime.UtcNow
                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
            Next
        End Sub

        ''' <summary>The processes started by <paramref name="root"/>, and the ones they started, at any depth.</summary>
        Private Shared Function Descendants(root As Integer) As HashSet(Of UInteger)
            Dim parentOf As New Dictionary(Of UInteger, UInteger)
            Dim snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
            If snap = INVALID_HANDLE_VALUE Then Return New HashSet(Of UInteger)
            Try
                Dim pe As New PROCESSENTRY32W With {.dwSize = CUInt(Marshal.SizeOf(GetType(PROCESSENTRY32W)))}
                If Process32FirstW(snap, pe) Then
                    Do
                        parentOf(pe.th32ProcessID) = pe.th32ParentProcessID
                    Loop While Process32NextW(snap, pe)
                End If
            Finally
                CloseHandle(snap)
            End Try
            Dim found As New HashSet(Of UInteger)
            Dim frontier As New HashSet(Of UInteger) From {CUInt(root)}
            Do While frontier.Count > 0
                Dim nextLevel As New HashSet(Of UInteger)
                For Each kv In parentOf
                    If kv.Key <> CUInt(root) AndAlso frontier.Contains(kv.Value) AndAlso found.Add(kv.Key) Then nextLevel.Add(kv.Key)
                Next
                frontier = nextLevel
            Loop
            Return found
        End Function

        ''' <summary>
        ''' The visible windows of the child processes, and the message boxes (dialog class #32770) of the calculating
        ''' thread of DWSIM's own process.
        ''' </summary>
        Private Shared Function VisibleWindowsOf(pids As HashSet(Of UInteger), self As UInteger, caller As UInteger) As List(Of IntPtr)
            Dim list As New List(Of IntPtr)
            EnumWindows(Function(hwnd, lParam)
                            If Not IsWindowVisible(hwnd) Then Return True
                            Dim pid As UInteger
                            Dim tid = GetWindowThreadProcessId(hwnd, pid)
                            If pids.Contains(pid) Then
                                list.Add(hwnd)
                            ElseIf pid = self AndAlso tid = caller Then
                                Dim cls As New StringBuilder(16)
                                GetClassName(hwnd, cls, cls.Capacity)
                                If cls.ToString() = "#32770" Then list.Add(hwnd)
                            End If
                            Return True
                        End Function, IntPtr.Zero)
            Return list
        End Function

        ''' <summary>The window's title and the texts inside it, buttons left out.</summary>
        Private Shared Function Describe(hwnd As IntPtr) As String
            Dim parts As New List(Of String)
            ' a title that ends in a colon ("Chemsep Cape Open:") reads the same without it, before the joining one
            Dim title = TextOf(hwnd).TrimEnd(":"c).Trim()
            If title.Length > 0 Then parts.Add(title)
            EnumChildWindows(hwnd, Function(child, lParam)
                                       Dim cls As New StringBuilder(64)
                                       GetClassName(child, cls, cls.Capacity)
                                       Dim name = cls.ToString()
                                       If name.IndexOf("button", StringComparison.OrdinalIgnoreCase) < 0 AndAlso name.IndexOf("btn", StringComparison.OrdinalIgnoreCase) < 0 Then
                                           Dim t = TextOf(child)
                                           If t.Length > 0 Then parts.Add(t)
                                       End If
                                       Return True
                                   End Function, IntPtr.Zero)
            Return String.Join(": ", parts)
        End Function

        Private Shared Function TextOf(hwnd As IntPtr) As String
            Dim n = GetWindowTextLength(hwnd)
            If n <= 0 Then Return ""
            Dim sb As New StringBuilder(n + 1)
            GetWindowText(hwnd, sb, sb.Capacity)
            Return sb.ToString().Trim()
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _stop.Set()
            _thread?.Join(2 * Interval + 1000)
            _stop.Dispose()
        End Sub

        ' ---------- Win32

        Private Const WM_CLOSE As UInteger = &H10
        Private Const WM_COMMAND As UInteger = &H111
        Private Const IDCANCEL As Integer = 2
        Private Const TH32CS_SNAPPROCESS As UInteger = &H2
        Private Shared ReadOnly INVALID_HANDLE_VALUE As New IntPtr(-1)

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
        Private Structure PROCESSENTRY32W
            Public dwSize As UInteger
            Public cntUsage As UInteger
            Public th32ProcessID As UInteger
            Public th32DefaultHeapID As IntPtr
            Public th32ModuleID As UInteger
            Public cntThreads As UInteger
            Public th32ParentProcessID As UInteger
            Public pcPriClassBase As Integer
            Public dwFlags As UInteger
            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)> Public szExeFile As String
        End Structure

        Private Delegate Function EnumWindowsProc(hwnd As IntPtr, lParam As IntPtr) As Boolean

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function CreateToolhelp32Snapshot(flags As UInteger, processId As UInteger) As IntPtr
        End Function

        <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Private Shared Function Process32FirstW(snapshot As IntPtr, ByRef entry As PROCESSENTRY32W) As Boolean
        End Function

        <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Unicode)>
        Private Shared Function Process32NextW(snapshot As IntPtr, ByRef entry As PROCESSENTRY32W) As Boolean
        End Function

        <DllImport("kernel32.dll", SetLastError:=True)>
        Private Shared Function CloseHandle(handle As IntPtr) As Boolean
        End Function

        <DllImport("kernel32.dll")>
        Private Shared Function GetCurrentThreadId() As UInteger
        End Function

        <DllImport("user32.dll")>
        Private Shared Function EnumWindows(callback As EnumWindowsProc, lParam As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function EnumChildWindows(parent As IntPtr, callback As EnumWindowsProc, lParam As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll")>
        Private Shared Function GetWindowThreadProcessId(hwnd As IntPtr, ByRef processId As UInteger) As UInteger
        End Function

        <DllImport("user32.dll")>
        Private Shared Function IsWindowVisible(hwnd As IntPtr) As Boolean
        End Function

        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetWindowTextLength(hwnd As IntPtr) As Integer
        End Function

        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetWindowText(hwnd As IntPtr, text As StringBuilder, maxCount As Integer) As Integer
        End Function

        <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetClassName(hwnd As IntPtr, className As StringBuilder, maxCount As Integer) As Integer
        End Function

        <DllImport("user32.dll")>
        Private Shared Function PostMessage(hwnd As IntPtr, msg As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
        End Function

    End Class

End Namespace
