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
Imports System.Reflection

Namespace Scripting

    ''' <summary>
    ''' The setup every embedded IronPython engine in DWSIM gets: the standard library that ships
    ''' with the application, and the interpreter state a hosted engine leaves empty.
    ''' </summary>
    Public Module IronPythonHost

        ''' <summary>
        ''' Prepares a freshly created engine. Call it right after CreateEngine, before running
        ''' any script.
        ''' </summary>
        Public Sub Prepare(engine As Microsoft.Scripting.Hosting.ScriptEngine)

            If engine Is Nothing Then Exit Sub

            AddStandardLibraryPaths(engine)
            SetExecutable(engine)

        End Sub

        ''' <summary>
        ''' Puts the standard library that ships alongside the application on the search path, so a
        ''' script can import stdlib modules such as pathlib (issue #46). The folder is "Lib" on the
        ''' Windows build and "lib" on the cross-platform one, and Linux is case-sensitive, so both
        ''' go in: the import machinery ignores a path that does not exist.
        ''' </summary>
        Public Sub AddStandardLibraryPaths(engine As Microsoft.Scripting.Hosting.ScriptEngine)

            Try
                Dim paths = engine.GetSearchPaths().ToList()
                For Each folder In BaseDirectories()
                    paths.Add(Path.Combine(folder, "Lib"))
                    paths.Add(Path.Combine(folder, "lib"))
                Next
                engine.SetSearchPaths(paths.Distinct().ToList())
            Catch ex As Exception
            End Try

        End Sub

        ''' <summary>
        ''' Fills in sys.executable, which a hosted engine leaves as None. The standard library's
        ''' site.py takes the absolute path of it while it loads, and on Linux and macOS that raises
        ''' "'NoneType' object has no attribute 'startswith'" for any script that imports site,
        ''' directly or through a module that does (issue #85).
        ''' </summary>
        Public Sub SetExecutable(engine As Microsoft.Scripting.Hosting.ScriptEngine)

            Try
                Dim sys = IronPython.Hosting.Python.GetSysModule(engine)

                Dim current As Object = Nothing
                If sys.TryGetVariable("executable", current) Then
                    If TypeOf current Is String AndAlso DirectCast(current, String) <> "" Then Exit Sub
                End If

                Dim exepath = HostExecutablePath()
                If exepath <> "" Then sys.SetVariable("executable", exepath)
            Catch ex As Exception
            End Try

        End Sub

        ''' <summary>The file the process was started from, empty when it cannot be determined.</summary>
        Private Function HostExecutablePath() As String

            Try
                Dim main = Process.GetCurrentProcess().MainModule
                If main IsNot Nothing AndAlso Not String.IsNullOrEmpty(main.FileName) Then Return main.FileName
            Catch ex As Exception
            End Try

            Try
                Dim entry = Assembly.GetEntryAssembly()
                If entry IsNot Nothing AndAlso Not String.IsNullOrEmpty(entry.Location) Then Return entry.Location
            Catch ex As Exception
            End Try

            Return ""

        End Function

        ''' <summary>
        ''' Where to look for the standard library: the application folder first. A single-file
        ''' build reports an empty assembly location, so AppContext is the reliable one, and the
        ''' folder of this assembly is kept for the side-by-side layouts.
        ''' </summary>
        Private Function BaseDirectories() As List(Of String)

            Dim folders As New List(Of String)

            Try
                If Not String.IsNullOrEmpty(AppContext.BaseDirectory) Then folders.Add(AppContext.BaseDirectory)
            Catch ex As Exception
            End Try

            Try
                Dim here = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                If Not String.IsNullOrEmpty(here) AndAlso Not folders.Contains(here) Then folders.Add(here)
            Catch ex As Exception
            End Try

            Return folders

        End Function

    End Module

End Namespace
