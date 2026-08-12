'Natural Gas Properties Plugin for DWSIM (cross-platform Avalonia edition)
'Copyright 2010-2026 Daniel Wagner

Imports DWSIM.Interfaces
Imports DWSIM.UI.Shared.Avalonia
Imports Avalonia.Controls

<System.Serializable()> Public Class Plugin

    Implements IUtilityPlugin, IUtilityPlugin5

    'this variable references the active flowsheet, set before the plugin window is opened.
    Public fsheet As IFlowsheet

    Public ReadOnly Property Author() As String Implements IUtilityPlugin.Author, IUtilityPlugin5.Author
        Get
            Return "Daniel Wagner"
        End Get
    End Property

    Public ReadOnly Property ContactInfo() As String Implements IUtilityPlugin.ContactInfo, IUtilityPlugin5.ContactInfo
        Get
            Return "danielwag@gmail.com"
        End Get
    End Property

    Public ReadOnly Property CurrentFlowsheet() As IFlowsheet Implements IUtilityPlugin.CurrentFlowsheet, IUtilityPlugin5.CurrentFlowsheet
        Get
            Return fsheet
        End Get
    End Property

    Public ReadOnly Property Description() As String Implements IUtilityPlugin.Description, IUtilityPlugin5.Description
        Get
            Return "Utility for calculation of Natural Gas Properties"
        End Get
    End Property

    Public ReadOnly Property DisplayMode() As IUtilityPlugin.DispMode Implements IUtilityPlugin.DisplayMode
        Get
            Return IUtilityPlugin.DispMode.Normal
        End Get
    End Property

    Public ReadOnly Property Name() As String Implements IUtilityPlugin.Name, IUtilityPlugin5.Name
        Get
            Return "Natural Gas Properties"
        End Get
    End Property

    Public Function SetFlowsheet(form As IFlowsheet) As Boolean Implements IUtilityPlugin.SetFlowsheet, IUtilityPlugin5.SetFlowsheet
        fsheet = form
        Return True
    End Function

    Public ReadOnly Property UniqueID() As String Implements IUtilityPlugin.UniqueID, IUtilityPlugin5.UniqueID
        Get
            Return "B002A8DB-0F94-48fa-8844-C6713855B1BB"
        End Get
    End Property

    'called by DWSIM to open the utility window. The results are computed once, over the
    'Material Stream currently selected in the flowsheet.
    Public ReadOnly Property UtilityForm() As Object Implements IUtilityPlugin.UtilityForm, IUtilityPlugin5.UtilityForm
        Get
            If fsheet Is Nothing Then Return Nothing

            Dim panel = AvaloniaCommon.GetDefaultContainer()
            panel.CreateAndAddLabelRow("Natural Gas Properties")

            Dim p As New Populate()
            p.Populate(fsheet, panel)

            Return AvaloniaCommon.GetDefaultEditorForm("Natural Gas Properties Plugin", 480, 700, panel)
        End Get
    End Property

    Public ReadOnly Property WebSite() As String Implements IUtilityPlugin.WebSite, IUtilityPlugin5.WebSite
        Get
            Return "https://dwsim.org"
        End Get
    End Property

    Public Function Run(args As Object) As Object Implements IUtilityPlugin5.Run
        Return Nothing
    End Function

End Class
