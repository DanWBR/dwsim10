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

Imports DWSIM.Interfaces

''' <summary>
''' Analyses a flowsheet and, in the Windows edition, inserts Recycle convergence blocks at the
''' best tear points to break infinite loops. That routine works directly on the WinForms flowsheet
''' form and its drawing surface, so it is not available in this cross-platform edition: the endpoint
''' that used it reports the loop instead of tearing it automatically. Porting the tear-point search
''' to the UI-agnostic flowsheet API is a separate task.
''' </summary>
Public Class FlowsheetAnalyzer

    Public Property Diagram As IFlowsheet

    Public Sub ProcessInfiniteLoops()
        Throw New NotSupportedException(
            "Automatic tear-stream insertion is only available in the Windows edition of DWSIM.")
    End Sub

End Class
