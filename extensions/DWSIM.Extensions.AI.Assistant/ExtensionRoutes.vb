'    DWSIM Assistant — routes contributed by other extensions
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

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports DWSIM.Interfaces

''' <summary>
''' Lets another extender answer <c>/api/ext/&lt;prefix&gt;/...</c> requests without this assembly
''' knowing it exists.
''' </summary>
''' <remarks>
''' <para>
''' An extender contributes routes by exposing one public static class with a
''' <c>RoutePrefix</c> string field and a
''' <c>Handle(IFlowsheet, String method, String subPath, String body, Action refreshCanvas) As String</c>
''' method that returns the JSON body. Nothing has to be registered: the first request for a prefix
''' scans the loaded assemblies once and caches what it finds, so the extender can be loaded in any
''' order and can be missing altogether (the caller gets a clear "not installed" answer).
''' </para>
''' <para>
''' The MCP side mirrors this with one tool module per prefix, the way <c>dynamics_tools.py</c>
''' mirrors <see cref="DynamicsRoutes"/>.
''' </para>
''' </remarks>
Public Module ExtensionRoutes

    Private Const PathRoot As String = "/api/ext/"

    Private ReadOnly Handlers As New ConcurrentDictionary(Of String, MethodInfo)(StringComparer.OrdinalIgnoreCase)
    Private Scanned As Boolean = False
    Private ReadOnly ScanLock As New Object()

    ''' <summary>True when the path belongs to this module.</summary>
    Public Function Owns(path As String) As Boolean
        Return path.StartsWith(PathRoot, StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Routes one request to the extender that owns its prefix.</summary>
    Public Function Handle(fs As IFlowsheet, method As String, path As String, body As String,
                           refreshCanvas As Action) As DynamicsRoutes.RouteResult

        Dim outcome As New DynamicsRoutes.RouteResult()
        Dim rest = path.Substring(PathRoot.Length)
        Dim slash = rest.IndexOf("/"c)
        Dim prefix = If(slash < 0, rest, rest.Substring(0, slash))
        Dim subPath = If(slash < 0, "", rest.Substring(slash + 1))

        If prefix = "" Then
            outcome.StatusCode = 404
            outcome.Body = "{""success"":false,""error"":""no extension prefix in the path""}"
            Return outcome
        End If

        Dim handler = Find(prefix)
        If handler Is Nothing Then
            outcome.StatusCode = 404
            outcome.Body = "{""success"":false,""error"":""not_installed"",""message"":""No loaded extension answers '" & prefix &
                "'. It is either not installed or not available at this subscription level.""}"
            Return outcome
        End If

        Try
            outcome.Body = CStr(handler.Invoke(Nothing, New Object() {fs, method, subPath, body, refreshCanvas}))
        Catch ex As TargetInvocationException
            Dim inner = If(ex.InnerException, ex)
            outcome.StatusCode = 500
            outcome.Body = "{""success"":false,""error"":""" & Esc(inner.Message) & """}"
        Catch ex As Exception
            outcome.StatusCode = 500
            outcome.Body = "{""success"":false,""error"":""" & Esc(ex.Message) & """}"
        End Try
        Return outcome

    End Function

    ''' <summary>The prefixes currently answered, for /api/check and diagnostics.</summary>
    Public Function Installed() As List(Of String)
        Scan(force:=True)
        Return Handlers.Keys.OrderBy(Function(k) k).ToList()
    End Function

    Private Function Find(prefix As String) As MethodInfo
        Dim m As MethodInfo = Nothing
        If Handlers.TryGetValue(prefix, m) Then Return m
        Scan(force:=Not Scanned)
        If Handlers.TryGetValue(prefix, m) Then Return m
        ' the extender may have been loaded after the first scan
        Scan(force:=True)
        Handlers.TryGetValue(prefix, m)
        Return m
    End Function

    Private Sub Scan(force As Boolean)
        SyncLock ScanLock
            If Scanned AndAlso Not force Then Return
            Scanned = True
            For Each asm In AppDomain.CurrentDomain.GetAssemblies()
                If asm.IsDynamic Then Continue For
                Dim name = asm.GetName().Name
                ' only extenders contribute; the engine and the frameworks are skipped for speed
                If name.IndexOf("Extensions", StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
                If name.Equals("DWSIM.Extensions.AI.Assistant", StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim types As Type()
                Try
                    types = asm.GetTypes()
                Catch ex As ReflectionTypeLoadException
                    types = ex.Types.Where(Function(t) t IsNot Nothing).ToArray()
                Catch
                    Continue For
                End Try
                For Each t In types
                    If Not (t.IsClass AndAlso t.IsPublic) Then Continue For
                    Dim prefixField = t.GetField("RoutePrefix", BindingFlags.Public Or BindingFlags.Static)
                    If prefixField Is Nothing OrElse prefixField.FieldType IsNot GetType(String) Then Continue For
                    Dim handle = t.GetMethod("Handle", BindingFlags.Public Or BindingFlags.Static, Nothing,
                                             New Type() {GetType(IFlowsheet), GetType(String), GetType(String), GetType(String), GetType(Action)}, Nothing)
                    If handle Is Nothing OrElse handle.ReturnType IsNot GetType(String) Then Continue For
                    Dim prefix = TryCast(prefixField.GetValue(Nothing), String)
                    If String.IsNullOrWhiteSpace(prefix) Then Continue For
                    Handlers(prefix) = handle
                Next
            Next
        End SyncLock
    End Sub

    Private Function Esc(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n")
    End Function

End Module
