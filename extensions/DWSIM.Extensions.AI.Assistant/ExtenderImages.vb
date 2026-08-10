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
Imports System.Reflection

''' <summary>
''' The extender interface hands the host the bytes of a PNG, and this reads them straight out of
''' the assembly.
''' </summary>
''' <remarks>
''' The icon used to be pulled from the resource file as a <c>System.Drawing.Image</c> and encoded
''' with a call into GDI+. That works under the Windows interface and throws under the
''' cross-platform one, which carries no drawing library of that kind, and the host that asks for
''' the icon gets nothing to put on its toolbar. The file is already a PNG: it is embedded as it is
''' and handed over unchanged.
''' </remarks>
Public Module ExtenderImages

    Private _ai As Byte()

    ''' <summary>The assistant icon, as the bytes of a PNG.</summary>
    Public ReadOnly Property AIIcon As Byte()
        Get
            If _ai Is Nothing Then _ai = Read("AI.png")
            Return _ai
        End Get
    End Property

    ''' <summary>
    ''' Reads an embedded file by its name alone. The resource is named after the root namespace
    ''' and the file, with the folder flattened away, so matching on the end of the name survives
    ''' the file being moved.
    ''' </summary>
    Private Function Read(fileName As String) As Byte()

        Dim asm = Assembly.GetExecutingAssembly()

        Dim name = asm.GetManifestResourceNames().
                       FirstOrDefault(Function(n) n.EndsWith("." & fileName, StringComparison.OrdinalIgnoreCase))

        If name Is Nothing Then Return New Byte() {}

        Using stream = asm.GetManifestResourceStream(name)

            If stream Is Nothing Then Return New Byte() {}

            Using buffer As New MemoryStream()
                stream.CopyTo(buffer)
                Return buffer.ToArray()
            End Using

        End Using

    End Function

End Module
