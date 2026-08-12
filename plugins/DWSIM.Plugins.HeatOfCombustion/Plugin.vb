'Heat of Combustion Calculator Plugin for DWSIM (cross-platform Avalonia edition)
'Copyright 2020-2026 Daniel Wagner

Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.ExtensionMethods
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
            Return "Utility for calculation of Heat of Combustion of a Material Stream"
        End Get
    End Property

    Public ReadOnly Property DisplayMode() As IUtilityPlugin.DispMode Implements IUtilityPlugin.DisplayMode
        Get
            Return IUtilityPlugin.DispMode.Normal
        End Get
    End Property

    Public ReadOnly Property Name() As String Implements IUtilityPlugin.Name, IUtilityPlugin5.Name
        Get
            Return "Heat of Combustion Calculator"
        End Get
    End Property

    Public Function SetFlowsheet(form As IFlowsheet) As Boolean Implements IUtilityPlugin.SetFlowsheet, IUtilityPlugin5.SetFlowsheet
        fsheet = form
        Return True
    End Function

    Public ReadOnly Property UniqueID() As String Implements IUtilityPlugin.UniqueID, IUtilityPlugin5.UniqueID
        Get
            Return "46BB84DD-88C1-46AB-A66A-17089904FA7F"
        End Get
    End Property

    'called by DWSIM to open the utility window.
    Public ReadOnly Property UtilityForm() As Object Implements IUtilityPlugin.UtilityForm, IUtilityPlugin5.UtilityForm
        Get
            If fsheet Is Nothing Then Return Nothing
            Return BuildWindow()
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

    Private Function BuildWindow() As Window

        Dim su = fsheet.FlowsheetOptions.SelectedUnitSystem

        Dim panel = AvaloniaCommon.GetDefaultContainer()
        panel.CreateAndAddLabelRow("Heat of Combustion Calculator")
        panel.CreateAndAddLabelRow2("Select a calculated Material Stream to compute its net heat of combustion (LHV).")
        panel.CreateAndAddEmptySpace()

        Dim streams = fsheet.SimulationObjects.Values.
            Where(Function(x) TypeOf x Is IMaterialStream).
            Select(Function(x2) x2.GraphicObject.Tag).ToList()
        streams.Insert(0, "")

        Dim lblMass As TextBlock = Nothing
        Dim lblMolar As TextBlock = Nothing

        panel.CreateAndAddDropDownRow("Material Stream", streams, 0,
            Sub(cb, e)
                Compute(TryCast(cb.SelectedItem, String), lblMass, lblMolar)
            End Sub)

        panel.CreateAndAddEmptySpace()
        lblMass = panel.CreateAndAddTwoLabelsRow("Mass LHV (" & su.enthalpy & ")", "")
        lblMolar = panel.CreateAndAddTwoLabelsRow("Molar LHV (" & su.molar_enthalpy & ")", "")

        Return AvaloniaCommon.GetDefaultEditorForm("Heat of Combustion Calculator", 460, 240, panel)

    End Function

    Private Sub Compute(tag As String, lblMass As TextBlock, lblMolar As TextBlock)

        If String.IsNullOrEmpty(tag) Then
            lblMass.Text = ""
            lblMolar.Text = ""
            Return
        End If

        Dim stream = fsheet.GetFlowsheetSimulationObject(tag)
        If stream Is Nothing Then Return

        Dim ms = DirectCast(stream, IMaterialStream)
        Dim pp = DirectCast(ms.GetPropertyPackageObject(), IPropertyPackage)

        Dim compounds = fsheet.SelectedCompounds.Values.ToList()
        Dim molar_composition = ms.GetOverallComposition()
        Dim mass_composition = ms.GetOverallComposition()
        pp.CurrentMaterialStream = ms
        Dim mw = pp.AUX_MMM(Enums.PhaseLabel.Mixture)

        Dim lhv_mass, lhv_molar, t1, t2 As Double

        'CxHyNzOn (std.) + O2 (g, xs.) -> x CO2 (g) + y/2 H2O (l) + z/2 N2 (g)

        Dim n As Integer = compounds.Count - 1
        For i As Integer = 0 To n
            If molar_composition(i) > 0.0 Then
                If compounds(i).StandardHeatOfCombustion_LHV = 0.0 Then
                    fsheet.ShowMessage(String.Format("Warning: {0} Standard Net Heat of Combustion = 0", compounds(i).Name), IFlowsheet.MessageType.Warning)
                End If
                mass_composition(i) = molar_composition(i) * compounds(i).Molar_Weight / mw
                t1 = molar_composition(i) * compounds(i).StandardHeatOfCombustion_LHV * compounds(i).Molar_Weight
                t2 = mass_composition(i) * compounds(i).StandardHeatOfCombustion_LHV
                lhv_molar += t1
                lhv_mass += t2
            End If
        Next

        Dim su = fsheet.FlowsheetOptions.SelectedUnitSystem
        Dim nf = fsheet.FlowsheetOptions.NumberFormat
        lblMass.Text = (-lhv_mass.ConvertFromSI(su.enthalpy)).ToString(nf)
        lblMolar.Text = (-lhv_molar.ConvertFromSI(su.molar_enthalpy)).ToString(nf)

    End Sub

End Class
