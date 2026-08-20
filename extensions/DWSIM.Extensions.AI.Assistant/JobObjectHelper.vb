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

Imports System.Runtime.InteropServices

''' <summary>
''' Wraps a Win32 Job Object configured with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE so that
''' any process assigned to it is terminated by the OS when the host process exits -
''' including crashes, Task Manager kills, and logoff. Used so the assistant program never
''' outlives DWSIM even when the host never calls ReleaseResources on shutdown.
''' </summary>
Friend NotInheritable Class JobObjectHelper

    Private Shared _jobHandle As IntPtr = IntPtr.Zero
    Private Shared ReadOnly _lock As New Object()

    Private Const JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE As UInteger = &H2000UI
    Private Const JobObjectExtendedLimitInformation As Integer = 9

    <StructLayout(LayoutKind.Sequential)>
    Private Structure IO_COUNTERS
        Public ReadOperationCount As UInt64
        Public WriteOperationCount As UInt64
        Public OtherOperationCount As UInt64
        Public ReadTransferCount As UInt64
        Public WriteTransferCount As UInt64
        Public OtherTransferCount As UInt64
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure JOBOBJECT_BASIC_LIMIT_INFORMATION
        Public PerProcessUserTimeLimit As Int64
        Public PerJobUserTimeLimit As Int64
        Public LimitFlags As UInt32
        Public MinimumWorkingSetSize As UIntPtr
        Public MaximumWorkingSetSize As UIntPtr
        Public ActiveProcessLimit As UInt32
        Public Affinity As UIntPtr
        Public PriorityClass As UInt32
        Public SchedulingClass As UInt32
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        Public BasicLimitInformation As JOBOBJECT_BASIC_LIMIT_INFORMATION
        Public IoInfo As IO_COUNTERS
        Public ProcessMemoryLimit As UIntPtr
        Public JobMemoryLimit As UIntPtr
        Public PeakProcessMemoryUsed As UIntPtr
        Public PeakJobMemoryUsed As UIntPtr
    End Structure

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateJobObject(lpJobAttributes As IntPtr, lpName As String) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SetInformationJobObject(hJob As IntPtr, infoType As Integer, lpJobObjectInfo As IntPtr, cbJobObjectInfoLength As UInt32) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function AssignProcessToJobObject(hJob As IntPtr, hProcess As IntPtr) As Boolean
    End Function

    Private Shared Function EnsureJob() As IntPtr
        If _jobHandle <> IntPtr.Zero Then Return _jobHandle
        SyncLock _lock
            If _jobHandle <> IntPtr.Zero Then Return _jobHandle
            Dim handle = CreateJobObject(IntPtr.Zero, Nothing)
            If handle = IntPtr.Zero Then Return IntPtr.Zero

            Dim info As New JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE

            Dim length As Integer = Marshal.SizeOf(GetType(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))
            Dim buffer As IntPtr = Marshal.AllocHGlobal(length)
            Try
                Marshal.StructureToPtr(info, buffer, False)
                If Not SetInformationJobObject(handle, JobObjectExtendedLimitInformation, buffer, CUInt(length)) Then
                    Return IntPtr.Zero
                End If
            Finally
                Marshal.FreeHGlobal(buffer)
            End Try

            _jobHandle = handle
            Return _jobHandle
        End SyncLock
    End Function

    ''' <summary>
    ''' Assigns the given process to a shared shutdown job. When the current DWSIM process
    ''' exits, Windows closes the job handle and kills every process inside it. Safe to call
    ''' on non-Windows or on failure - errors are swallowed and the caller's normal kill paths
    ''' still apply.
    ''' </summary>
    Public Shared Sub AssignToShutdownJob(p As Process)
        If p Is Nothing Then Return
        Try
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then Return
            Dim job = EnsureJob()
            If job = IntPtr.Zero Then Return
            AssignProcessToJobObject(job, p.Handle)
        Catch
        End Try
    End Sub

End Class
