'    DWSIM Interface definitions
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

Imports System.Collections.Generic

''' <summary>
''' Implemented by a flowsheet host that can tell what the user is looking at, so a helper such as the
''' AI assistant can fit its suggestions to the task at hand.
''' </summary>
Public Interface IUIContextProvider

    ''' <summary>
    ''' Returns what the user last worked on in this flowsheet, leaving out the assistant's own panel.
    ''' The host may be called from any thread and answers on its own UI thread.
    ''' </summary>
    ''' <remarks>
    ''' Keys, all optional:
    ''' "host" ("classic" or "crossplatform");
    ''' "selected" (a list with the names of the selected flowsheet objects);
    ''' "area" (what had the focus: "flowsheet", "editor", "tool" or the id of a host panel);
    ''' "editor" (a dictionary with "object", the name of the object whose editor is open, and "tab");
    ''' "window" (a dictionary with "id", "title" and whatever an <see cref="IUIContextSource"/> adds).
    ''' </remarks>
    Function GetUIContext() As Dictionary(Of String, Object)

End Interface

''' <summary>
''' Implemented by a window or a panel (an extension's tool window, for one) that describes the task it
''' shows better than its type name and title would.
''' </summary>
Public Interface IUIContextSource

    ''' <summary>
    ''' Returns the task this window shows: "id" (a stable name such as "ots.instructor"), "title", and
    ''' any details worth knowing (the exercise running, the object being edited). Returns Nothing when
    ''' the window shouldn't count as the user's task, as the assistant's own panel does.
    ''' </summary>
    ''' <remarks>Called on the UI thread.</remarks>
    Function DescribeUIContext() As Dictionary(Of String, Object)

End Interface
