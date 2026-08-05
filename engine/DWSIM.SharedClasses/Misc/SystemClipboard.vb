'    The system clipboard, as seen by the engine.
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

''' <summary>
''' The clipboard the engine copies text through. Reaching the real one is the host's job:
''' each UI framework has its own, and none of them belongs in the engine. Until a host
''' assigns the two hooks, the text stays inside this process, so copying between two running
''' instances is what needs the host to wire them up.
''' </summary>
Public Class SystemClipboard

    Public Shared Property Writer As Action(Of String)

    Public Shared Property Reader As Func(Of String)

    Private Shared LocalText As String = ""

    Public Shared Sub SetText(text As String)

        LocalText = text

        If Writer IsNot Nothing Then Writer.Invoke(text)

    End Sub

    Public Shared Function GetText() As String

        If Reader IsNot Nothing Then Return Reader.Invoke()

        Return LocalText

    End Function

End Class
