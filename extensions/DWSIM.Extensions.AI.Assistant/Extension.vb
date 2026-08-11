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

Imports System.Threading.Tasks
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums

' This is the open, cross-platform launcher for the DWSIM Assistant. It registers the menu entries,
' starts the flowsheet HTTP bridge and the assistant server, and hands the assistant's page to the
' host to show on a docked panel. The assistant server itself is a separate program reached over a
' local HTTP connection; see LICENSE-NOTICE.txt. The WinForms launcher of the Windows edition, which
' hosts the assistant in a native window, is a separate project.

''' <summary>Registers the AI Assistant in the main-window Tools menu.</summary>
Public Class Handler

    Implements IExtenderCollection, IExtenderCollection2

    Public ReadOnly Property ID As String Implements IExtenderCollection.ID
        Get
            Return "8ffa4569-421f-474b-a44c-fa0ab59920f5"
        End Get
    End Property

    Public ReadOnly Property Description As String Implements IExtenderCollection.Description
        Get
            Return "AI Assistant for DWSIM"
        End Get
    End Property

    Public ReadOnly Property DisplayText As String Implements IExtenderCollection.DisplayText
        Get
            Return "DWSIM Assistant"
        End Get
    End Property

    Public ReadOnly Property Category As ExtenderCategory Implements IExtenderCollection.Category
        Get
            Return ExtenderCategory.Tools
        End Get
    End Property

    Public ReadOnly Property Level As ExtenderLevel Implements IExtenderCollection.Level
        Get
            Return ExtenderLevel.MainWindow
        End Get
    End Property

    Public ReadOnly Property Collection As List(Of IExtender) Implements IExtenderCollection.Collection

    Public ReadOnly Property InsertAtPosition As Integer = -1 Implements IExtenderCollection2.InsertAtPosition

    Sub New()
        Collection = New List(Of IExtender)
        Collection.Add(New AIAssistantExtender())
    End Sub

    Public Sub SetMenuItem(menuitem As Object) Implements IExtenderCollection2.SetMenuItem

    End Sub

End Class

''' <summary>Opens the AI Assistant panel for the active flowsheet.</summary>
Public Class AIAssistantExtender

    Implements IExtender, IExtender3, IExtender4, IExtender6

    Private Flowsheet As IFlowsheet

    Public ReadOnly Property ID As String Implements IExtender.ID
        Get
            Return "4e5a8d1e-49a7-4017-9625-2ed5e79784b3"
        End Get
    End Property

    Public ReadOnly Property DisplayText As String Implements IExtender.DisplayText
        Get
            Return "DWSIM Assistant"
        End Get
    End Property

    Public ReadOnly Property DisplayImage As Byte() Implements IExtender.DisplayImage
        Get
            Return ExtenderImages.AIIcon
        End Get
    End Property

    Public ReadOnly Property InsertAtPosition As Integer Implements IExtender.InsertAtPosition
        Get
            Return 0
        End Get
    End Property

    Public Sub SetMainWindow(mainwindow As Object) Implements IExtender.SetMainWindow

    End Sub

    ''' <summary>Called by the cross-platform host; starts the server warming up in the background.</summary>
    Public Sub SetFlowsheetGUI(FlowsheetGUI As Object) Implements IExtender6.SetFlowsheetGUI

        ' the assistant server takes seconds to answer the first time; start it now so it is ready
        ReportExportHelper.WarmUp()

    End Sub

    Public Sub SetFlowsheet(form As IFlowsheet) Implements IExtender.SetFlowsheet

        Flowsheet = form

    End Sub

    Public Sub Run() Implements IExtender.Run

        If Flowsheet Is Nothing Then Return

        Dim fs = Flowsheet

        fs.ShowMessage("Starting the DWSIM Assistant...", IFlowsheet.MessageType.Information)

        ' waiting for the server on the thread that draws the window is what froze the interface
        Task.Run(Sub()

                     Try
                         ReportExportHelper.EnsureApiServerRunning(fs)
                     Catch ex As Exception
                         fs.ShowMessage("The assistant could not reach the simulation: " & ex.Message,
                                        IFlowsheet.MessageType.GeneralError)
                         Return
                     End Try

                     If Not ReportExportHelper.EnsurePythonServerRunning() Then
                         fs.ShowMessage(
                             "The DWSIM Assistant server did not start. Check that AIAssistantFiles " &
                             "sits next to the extension.", IFlowsheet.MessageType.GeneralError)
                         Return
                     End If

                     Dim url = "http://localhost:5834/?token=" & ReportExportHelper.AssistantToken

                     fs.DisplayWebPanel("DWSIM Assistant", url)

                 End Sub)

    End Sub

    Public Sub ReleaseResources() Implements IExtender3.ReleaseResources

        ReportExportHelper.StopPythonServer()
        Flowsheet = Nothing

    End Sub

    Public Sub SetParameter(pname As String, pvalue As Object) Implements IExtender4.SetParameter

    End Sub

End Class

''' <summary>Registers the AI Assistant settings entry in the Edit menu.</summary>
Public Class SettingsHandler

    Implements IExtenderCollection, IExtenderCollection2

    Public ReadOnly Property ID As String Implements IExtenderCollection.ID
        Get
            Return "8ffa4569-421f-474b-a44c-fa0ab59920f5-2"
        End Get
    End Property

    Public ReadOnly Property Description As String Implements IExtenderCollection.Description
        Get
            Return "AI Assistant for DWSIM"
        End Get
    End Property

    Public ReadOnly Property DisplayText As String Implements IExtenderCollection.DisplayText
        Get
            Return "DWSIM Assistant Settings"
        End Get
    End Property

    Public ReadOnly Property Category As ExtenderCategory Implements IExtenderCollection.Category
        Get
            Return ExtenderCategory.Edit
        End Get
    End Property

    Public ReadOnly Property Level As ExtenderLevel Implements IExtenderCollection.Level
        Get
            Return ExtenderLevel.MainWindow
        End Get
    End Property

    Public ReadOnly Property Collection As List(Of IExtender) Implements IExtenderCollection.Collection

    Public ReadOnly Property InsertAtPosition As Integer = -1 Implements IExtenderCollection2.InsertAtPosition

    Sub New()
        Collection = New List(Of IExtender)
        Collection.Add(New AIAssistantSettingsExtender())
    End Sub

    Public Sub SetMenuItem(menuitem As Object) Implements IExtenderCollection2.SetMenuItem

    End Sub

End Class

''' <summary>Opens the AI Assistant settings page.</summary>
Public Class AIAssistantSettingsExtender

    Implements IExtender, IExtender3, IExtender6

    Private Flowsheet As IFlowsheet

    Public ReadOnly Property ID As String Implements IExtender.ID
        Get
            Return "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"
        End Get
    End Property

    Public ReadOnly Property DisplayText As String Implements IExtender.DisplayText
        Get
            Return "DWSIM Assistant Settings"
        End Get
    End Property

    Public ReadOnly Property DisplayImage As Byte() Implements IExtender.DisplayImage
        Get
            Return ExtenderImages.AIIcon
        End Get
    End Property

    Public ReadOnly Property InsertAtPosition As Integer Implements IExtender.InsertAtPosition
        Get
            Return 1
        End Get
    End Property

    Public Sub SetMainWindow(mainwindow As Object) Implements IExtender.SetMainWindow

    End Sub

    Public Sub SetFlowsheet(form As IFlowsheet) Implements IExtender.SetFlowsheet

        Flowsheet = form

    End Sub

    Public Sub SetFlowsheetGUI(FlowsheetGUI As Object) Implements IExtender6.SetFlowsheetGUI

    End Sub

    Public Sub Run() Implements IExtender.Run

        Dim fs = Flowsheet
        If fs Is Nothing Then Return

        Task.Run(Sub()
                     If Not ReportExportHelper.EnsurePythonServerRunning() Then Return

                     Dim url = "http://localhost:5834/settings?token=" & ReportExportHelper.AssistantToken

                     fs.DisplayWebPanel("DWSIM Assistant Settings", url)
                 End Sub)

    End Sub

    Public Sub ReleaseResources() Implements IExtender3.ReleaseResources

    End Sub

End Class
