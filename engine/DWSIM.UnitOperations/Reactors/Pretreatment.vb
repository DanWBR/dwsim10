'    Biomass Pretreatment Reactor - Calculation Routines
'    Copyright 2026 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.

Imports DWSIM.Thermodynamics.BaseClasses
Imports System.Math
Imports System.Linq
Imports DWSIM.Interfaces
Imports DWSIM.Interfaces.Enums
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports SkiaSharp
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports DWSIM.UnitOperations.Streams
Imports System.Collections.Generic
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>Pretreatment technology selector.</summary>
    Public Enum PretreatmentType
        DiluteAcid = 0
        SteamExplosion = 1
        Alkaline = 2
        Organosolv = 3
    End Enum

    ''' <summary>
    ''' Biomass pretreatment reactor. Converts a lignocellulosic slurry (cellulose + hemicellulose + lignin)
    ''' into a pretreated slurry containing sugars (glucose, xylose) and inhibitors (furfural, HMF,
    ''' acetic acid), using user-specified conversion fractions per reaction. Defaults are keyed to
    ''' the selected PretreatmentType.
    ''' Reactions:
    '''   (C6H10O5)n + H2O -> C6H12O6                  (cellulose â†’ glucose)
    '''   C6H12O6       -> C6H6O3 + 3 H2O             (glucose â†’ HMF)
    '''   (C5H8O4)n  + H2O -> C5H10O5                  (xylan â†’ xylose)
    '''   C5H10O5       -> C5H4O2 + 3 H2O             (xylose â†’ furfural)
    ''' Acetic acid is released from acetyl groups in the hemicellulose, proportionally to the
    ''' hemicellulose mass consumed.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_Pretreatment

        Inherits Reactor

        Implements IExternalUnitOperation
        Public ReadOnly Property IsBio As Boolean = True

        Public Overrides Property ObjectClass As SimulationObjectClass
            Get
                Return SimulationObjectClass.Reactors
            End Get
            Set(value As SimulationObjectClass)
                MyBase.ObjectClass = value
            End Set
        End Property

        ''' <summary>Gets or sets the display name for this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description for this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ' -------- CONFIG --------

        Public Property Technology As PretreatmentType = PretreatmentType.DiluteAcid
        Public Property SeverityLogR0 As Double = 3.5
        Public Property ResidenceTime_s As Double = 600.0
        Public Property SolidsLoading_wfrac As Double = 0.20

        ' -------- COMPOUND ROLES --------

        Public Property CelluloseCompound As String = ""
        Public Property HemicelluloseCompound As String = ""
        Public Property LigninCompound As String = ""
        Public Property GlucoseCompound As String = ""
        Public Property XyloseCompound As String = ""
        Public Property FurfuralCompound As String = ""
        Public Property HMFCompound As String = ""
        Public Property AceticAcidCompound As String = ""
        Public Property WaterCompound As String = "Water"
        Public Property SolubleLigninCompound As String = ""

        ' -------- CONVERSION FRACTIONS --------

        ''' <summary>Fraction of cellulose converted to glucose (0â€“1). Typical dilute acid: 0.05â€“0.15 (main conversion is in EH downstream).</summary>
        Public Property CelluloseConversion As Double = 0.10

        ''' <summary>Fraction of glucose generated that further degrades to HMF (0â€“1). Typical dilute acid: 0.02â€“0.05.</summary>
        Public Property GlucoseToHMF As Double = 0.03

        ''' <summary>Fraction of hemicellulose converted to xylose (0â€“1). Typical dilute acid: 0.80â€“0.95.</summary>
        Public Property HemicelluloseConversion As Double = 0.90

        ''' <summary>Fraction of xylose generated that further degrades to furfural (0â€“1). Typical dilute acid: 0.05â€“0.10.</summary>
        Public Property XyloseToFurfural As Double = 0.07

        ''' <summary>Fraction of lignin solubilized (0â€“1). Strong for alkaline/organosolv; low for dilute acid.</summary>
        Public Property LigninSolubilization As Double = 0.15

        ''' <summary>g acetic acid released per g hemicellulose consumed (mass fraction). Default 0.12.</summary>
        Public Property AceticAcidYieldOnHemi As Double = 0.12

        ' -------- RESULTS --------

        Public Property Result_GlucoseProduced_kgs As Double = 0.0
        Public Property Result_XyloseProduced_kgs As Double = 0.0
        Public Property Result_FurfuralProduced_kgs As Double = 0.0
        Public Property Result_HMFProduced_kgs As Double = 0.0
        Public Property Result_AceticAcidProduced_kgs As Double = 0.0
        Public Property Result_LigninSolubilized_kgs As Double = 0.0
        Public Property Result_CelluloseConsumed_kgs As Double = 0.0
        Public Property Result_HemicelluloseConsumed_kgs As Double = 0.0

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)
            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_Pretreatment()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_Pretreatment)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Preset default conversions for a given pretreatment technology.</summary>
        Public Sub ApplyTechnologyDefaults()
            Select Case Technology
                Case PretreatmentType.DiluteAcid
                    CelluloseConversion = 0.08 : GlucoseToHMF = 0.03
                    HemicelluloseConversion = 0.90 : XyloseToFurfural = 0.07
                    LigninSolubilization = 0.10 : AceticAcidYieldOnHemi = 0.12
                Case PretreatmentType.SteamExplosion
                    CelluloseConversion = 0.05 : GlucoseToHMF = 0.02
                    HemicelluloseConversion = 0.80 : XyloseToFurfural = 0.05
                    LigninSolubilization = 0.05 : AceticAcidYieldOnHemi = 0.10
                Case PretreatmentType.Alkaline
                    CelluloseConversion = 0.02 : GlucoseToHMF = 0.0
                    HemicelluloseConversion = 0.55 : XyloseToFurfural = 0.0
                    LigninSolubilization = 0.70 : AceticAcidYieldOnHemi = 0.15
                Case PretreatmentType.Organosolv
                    CelluloseConversion = 0.05 : GlucoseToHMF = 0.01
                    HemicelluloseConversion = 0.75 : XyloseToFurfural = 0.03
                    LigninSolubilization = 0.85 : AceticAcidYieldOnHemi = 0.05
            End Select
        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Not Me.GraphicObject.InputConnectors(0).IsAttached Then _
                Throw New Exception("Pretreatment: Biomass slurry inlet not connected.")
            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then _
                Throw New Exception("Pretreatment: Pretreated slurry outlet not connected.")

            Dim ims As MaterialStream =
                DirectCast(FlowSheet.SimulationObjects(Me.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Name), MaterialStream).Clone
            ims.SetFlowsheet(Me.FlowSheet)
            ims.SetPropertyPackage(PropertyPackage)
            PropertyPackage.CurrentMaterialStream = ims
            ims.DefinedFlow = FlowSpec.Mass

            Dim T As Double = ims.Phases(0).Properties.temperature.GetValueOrDefault
            Dim P0 As Double = ims.Phases(0).Properties.pressure.GetValueOrDefault
            Dim P As Double = P0 - DeltaP.GetValueOrDefault
            ims.Phases(0).Properties.pressure = P

            Dim compounds = ims.Phases(0).Compounds
            Dim newMass As New Dictionary(Of String, Double)
            For Each kvp In compounds
                newMass(kvp.Key) = kvp.Value.MassFlow.GetValueOrDefault
            Next

            Dim m_cell_in As Double = 0.0, m_hemi_in As Double = 0.0, m_lignin_in As Double = 0.0
            If Not String.IsNullOrEmpty(CelluloseCompound) AndAlso compounds.ContainsKey(CelluloseCompound) Then _
                m_cell_in = compounds(CelluloseCompound).MassFlow.GetValueOrDefault
            If Not String.IsNullOrEmpty(HemicelluloseCompound) AndAlso compounds.ContainsKey(HemicelluloseCompound) Then _
                m_hemi_in = compounds(HemicelluloseCompound).MassFlow.GetValueOrDefault
            If Not String.IsNullOrEmpty(LigninCompound) AndAlso compounds.ContainsKey(LigninCompound) Then _
                m_lignin_in = compounds(LigninCompound).MassFlow.GetValueOrDefault

            ' Cellulose â†’ glucose (with subsequent glucose â†’ HMF)
            Dim dm_cell = m_cell_in * Max(0.0, Min(1.0, CelluloseConversion))
            ' 1 g cellulose (162.14) + H2O (18.02) â†’ 1.111 g glucose (180.16)
            Dim dm_glu_gross = dm_cell * 1.111
            Dim dm_h2o_cell = dm_cell * 0.111 ' water consumed by cellulose hydrolysis
            Dim dm_hmf = dm_glu_gross * Max(0.0, Min(1.0, GlucoseToHMF))
            ' glucose (180.16) â†’ HMF (126.11) + 3 H2O (54.05); 1 g glu â†’ 0.70 g HMF + 0.30 g H2O
            Dim dm_glu_net = dm_glu_gross - dm_hmf / 0.70
            Dim dm_h2o_hmf_release = dm_hmf * 0.30 / 0.70 ' water released by glucose â†’ HMF

            ' Hemicellulose â†’ xylose (with subsequent xylose â†’ furfural) + acetic acid
            Dim dm_hemi = m_hemi_in * Max(0.0, Min(1.0, HemicelluloseConversion))
            ' 1 g xylan (132.12) + H2O (18.02) â†’ 1.136 g xylose (150.13)
            Dim dm_xyl_gross = dm_hemi * 1.1364
            Dim dm_h2o_hemi = dm_hemi * 0.1364
            Dim dm_fur = dm_xyl_gross * Max(0.0, Min(1.0, XyloseToFurfural))
            ' xylose (150.13) â†’ furfural (96.08) + 3 H2O (54.05); 1 g xyl â†’ 0.64 g fur + 0.36 g H2O
            Dim dm_xyl_net = dm_xyl_gross - dm_fur / 0.64
            Dim dm_h2o_fur_release = dm_fur * 0.36 / 0.64

            Dim dm_acetic = dm_hemi * Max(0.0, AceticAcidYieldOnHemi)

            ' Lignin solubilization (mass conservative: lignin â†’ soluble lignin; if no soluble form chosen, just convert in place)
            Dim dm_lignin_sol = m_lignin_in * Max(0.0, Min(1.0, LigninSolubilization))

            ' Apply mass balances
            If Not String.IsNullOrEmpty(CelluloseCompound) AndAlso newMass.ContainsKey(CelluloseCompound) Then _
                newMass(CelluloseCompound) = Max(newMass(CelluloseCompound) - dm_cell, 0.0)
            If Not String.IsNullOrEmpty(HemicelluloseCompound) AndAlso newMass.ContainsKey(HemicelluloseCompound) Then _
                newMass(HemicelluloseCompound) = Max(newMass(HemicelluloseCompound) - dm_hemi, 0.0)
            If Not String.IsNullOrEmpty(LigninCompound) AndAlso newMass.ContainsKey(LigninCompound) Then _
                newMass(LigninCompound) = Max(newMass(LigninCompound) - dm_lignin_sol, 0.0)
            If Not String.IsNullOrEmpty(SolubleLigninCompound) AndAlso newMass.ContainsKey(SolubleLigninCompound) Then _
                newMass(SolubleLigninCompound) += dm_lignin_sol

            If Not String.IsNullOrEmpty(GlucoseCompound) AndAlso newMass.ContainsKey(GlucoseCompound) Then _
                newMass(GlucoseCompound) += dm_glu_net
            If Not String.IsNullOrEmpty(XyloseCompound) AndAlso newMass.ContainsKey(XyloseCompound) Then _
                newMass(XyloseCompound) += dm_xyl_net
            If Not String.IsNullOrEmpty(HMFCompound) AndAlso newMass.ContainsKey(HMFCompound) Then _
                newMass(HMFCompound) += dm_hmf
            If Not String.IsNullOrEmpty(FurfuralCompound) AndAlso newMass.ContainsKey(FurfuralCompound) Then _
                newMass(FurfuralCompound) += dm_fur
            If Not String.IsNullOrEmpty(AceticAcidCompound) AndAlso newMass.ContainsKey(AceticAcidCompound) Then _
                newMass(AceticAcidCompound) += dm_acetic

            Dim dm_h2o_net = -(dm_h2o_cell + dm_h2o_hemi) + dm_h2o_hmf_release + dm_h2o_fur_release
            If Not String.IsNullOrEmpty(WaterCompound) AndAlso newMass.ContainsKey(WaterCompound) Then _
                newMass(WaterCompound) = Max(newMass(WaterCompound) + dm_h2o_net, 0.0)

            Result_CelluloseConsumed_kgs = dm_cell
            Result_HemicelluloseConsumed_kgs = dm_hemi
            Result_GlucoseProduced_kgs = dm_glu_net
            Result_XyloseProduced_kgs = dm_xyl_net
            Result_HMFProduced_kgs = dm_hmf
            Result_FurfuralProduced_kgs = dm_fur
            Result_AceticAcidProduced_kgs = dm_acetic
            Result_LigninSolubilized_kgs = dm_lignin_sol

            Dim totalNewMass As Double = 0.0
            For Each v In newMass.Values : totalNewMass += v : Next
            If totalNewMass <= 0 Then totalNewMass = ims.Phases(0).Properties.massflow.GetValueOrDefault

            For Each comp In compounds.Values
                comp.MassFraction = newMass(comp.Name) / totalNewMass
            Next
            Dim invMWsum As Double = 0.0
            For Each comp In compounds.Values
                invMWsum += comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight
            Next
            If invMWsum > 0 Then
                For Each comp In compounds.Values
                    comp.MoleFraction = (comp.MassFraction.GetValueOrDefault / comp.ConstantProperties.Molar_Weight) / invMWsum
                Next
            End If
            ims.Phases(0).Properties.massflow = totalNewMass
            ims.DefinedFlow = FlowSpec.Mass

            ' Outlet temperature: if user set OutletTemperature via ReactorOperationMode, honour; else keep T
            Select Case ReactorOperationMode
                Case OperationMode.OutletTemperature
                    If OutletTemperature > 0 Then ims.Phases(0).Properties.temperature = OutletTemperature
            End Select

            ims.SpecType = StreamSpec.Temperature_and_Pressure
            PropertyPackage.CurrentMaterialStream = ims
            ims.Calculate(True, True)

            ' Push to outlet
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms_out As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms_out
                    .ClearAllProps()
                    .Phases(0).Properties.temperature = ims.Phases(0).Properties.temperature
                    .Phases(0).Properties.pressure = ims.Phases(0).Properties.pressure
                    For Each c In .Phases(0).Compounds.Values
                        If ims.Phases(0).Compounds.ContainsKey(c.Name) Then
                            c.MassFraction = ims.Phases(0).Compounds(c.Name).MassFraction
                            c.MoleFraction = ims.Phases(0).Compounds(c.Name).MoleFraction
                        End If
                    Next
                    .Phases(0).Properties.massflow = totalNewMass
                    .DefinedFlow = FlowSpec.Mass
                    .SpecType = StreamSpec.Temperature_and_Pressure
                End With
            End If

        End Sub

        Public Overrides Sub DeCalculate()
            Dim cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                Dim ms As MaterialStream = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    For Each c In .Phases(0).Compounds.Values
                        c.MoleFraction = 0
                        c.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .GraphicObject.Calculated = False
                End With
            End If
        End Sub

        Public Overrides Function GetIconBitmapBytes() As Byte()
            Return UnitOperations.BioOpsDrawHelper.RenderIconToPngBytes(64, 64, AddressOf DrawIcon)
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Biomass pretreatment reactor (dilute-acid / steam-explosion / alkaline / organosolv)"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Pretreatment Reactor"
        End Function

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String
            Dim s As New Text.StringBuilder
            s.AppendLine("Pretreatment: " & Me.GraphicObject.Tag)
            s.AppendLine("Technology:        " & Technology.ToString())
            s.AppendLine("Severity (log R0): " & SeverityLogR0.ToString(numberformat, ci))
            s.AppendLine()
            s.AppendLine("Conversions:")
            s.AppendLine("  Cellulose â†’ glucose:     " & (CelluloseConversion * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Glucose â†’ HMF:           " & (GlucoseToHMF * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Hemicellulose â†’ xylose:  " & (HemicelluloseConversion * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Xylose â†’ furfural:       " & (XyloseToFurfural * 100).ToString(numberformat, ci) & " %")
            s.AppendLine("  Lignin solubilization:   " & (LigninSolubilization * 100).ToString(numberformat, ci) & " %")
            s.AppendLine()
            s.AppendLine("Results (kg/s):")
            s.AppendLine("  Cellulose consumed:     " & Result_CelluloseConsumed_kgs.ToString(numberformat, ci))
            s.AppendLine("  Hemicellulose consumed: " & Result_HemicelluloseConsumed_kgs.ToString(numberformat, ci))
            s.AppendLine("  Glucose produced:       " & Result_GlucoseProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Xylose produced:        " & Result_XyloseProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  HMF produced:           " & Result_HMFProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Furfural produced:      " & Result_FurfuralProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Acetic acid produced:   " & Result_AceticAcidProduced_kgs.ToString(numberformat, ci))
            s.AppendLine("  Lignin solubilized:     " & Result_LigninSolubilized_kgs.ToString(numberformat, ci))
            Return s.ToString()
        End Function

        Private Shared ReadOnly _inputProps As String() = {
            "Technology", "Severity Log R0", "Residence Time", "Solids Loading",
            "Cellulose Compound", "Hemicellulose Compound", "Lignin Compound",
            "Glucose Compound", "Xylose Compound", "Furfural Compound", "HMF Compound",
            "Acetic Acid Compound", "Water Compound", "Soluble Lignin Compound",
            "Cellulose Conversion", "Glucose to HMF", "Hemicellulose Conversion",
            "Xylose to Furfural", "Lignin Solubilization", "Acetic Acid Yield on Hemi"
        }

        Private Shared ReadOnly _outputProps As String() = {
            "Cellulose Consumed", "Hemicellulose Consumed", "Glucose Produced",
            "Xylose Produced", "HMF Produced", "Furfural Produced",
            "Acetic Acid Produced", "Lignin Solubilized"
        }

        Public Overrides Function GetProperties(proptype As PropertyType) As String()
            Dim baseprops = MyBase.GetProperties(proptype)
            Select Case proptype
                Case PropertyType.WR : Return _inputProps
                Case PropertyType.RO : Return _outputProps
                Case Else : Return _inputProps.Concat(_outputProps).Concat(baseprops).ToArray()
            End Select
        End Function

        Public Overrides Function GetPropertyValue(prop As String, Optional su As IUnitsOfMeasure = Nothing) As Object
            Select Case prop
                Case "Technology" : Return Technology.ToString()
                Case "Severity Log R0" : Return SeverityLogR0
                Case "Residence Time" : Return ResidenceTime_s
                Case "Solids Loading" : Return SolidsLoading_wfrac
                Case "Cellulose Compound" : Return CelluloseCompound
                Case "Hemicellulose Compound" : Return HemicelluloseCompound
                Case "Lignin Compound" : Return LigninCompound
                Case "Glucose Compound" : Return GlucoseCompound
                Case "Xylose Compound" : Return XyloseCompound
                Case "Furfural Compound" : Return FurfuralCompound
                Case "HMF Compound" : Return HMFCompound
                Case "Acetic Acid Compound" : Return AceticAcidCompound
                Case "Water Compound" : Return WaterCompound
                Case "Soluble Lignin Compound" : Return SolubleLigninCompound
                Case "Cellulose Conversion" : Return CelluloseConversion
                Case "Glucose to HMF" : Return GlucoseToHMF
                Case "Hemicellulose Conversion" : Return HemicelluloseConversion
                Case "Xylose to Furfural" : Return XyloseToFurfural
                Case "Lignin Solubilization" : Return LigninSolubilization
                Case "Acetic Acid Yield on Hemi" : Return AceticAcidYieldOnHemi
                Case "Cellulose Consumed" : Return Result_CelluloseConsumed_kgs
                Case "Hemicellulose Consumed" : Return Result_HemicelluloseConsumed_kgs
                Case "Glucose Produced" : Return Result_GlucoseProduced_kgs
                Case "Xylose Produced" : Return Result_XyloseProduced_kgs
                Case "HMF Produced" : Return Result_HMFProduced_kgs
                Case "Furfural Produced" : Return Result_FurfuralProduced_kgs
                Case "Acetic Acid Produced" : Return Result_AceticAcidProduced_kgs
                Case "Lignin Solubilized" : Return Result_LigninSolubilized_kgs
                Case Else : Return MyBase.GetPropertyValue(prop, su)
            End Select
        End Function

        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String
            Select Case prop
                Case "Residence Time" : Return "s"
                Case "Cellulose Consumed", "Hemicellulose Consumed", "Glucose Produced",
                     "Xylose Produced", "HMF Produced", "Furfural Produced",
                     "Acetic Acid Produced", "Lignin Solubilized" : Return "kg/s"
                Case Else : Return "-"
            End Select
        End Function

        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean
            Dim d As Double = 0.0
            If TypeOf propval Is Double Then
                d = CDbl(propval)
            ElseIf TypeOf propval Is String Then
                Double.TryParse(CStr(propval), Globalization.NumberStyles.Any, Globalization.CultureInfo.CurrentCulture, d)
            End If
            Select Case prop
                Case "Technology"
                    Dim t As PretreatmentType
                    If [Enum].TryParse(Of PretreatmentType)(propval?.ToString(), t) Then Technology = t
                    Return True
                Case "Severity Log R0" : SeverityLogR0 = d : Return True
                Case "Residence Time" : ResidenceTime_s = d : Return True
                Case "Solids Loading" : SolidsLoading_wfrac = d : Return True
                Case "Cellulose Compound" : CelluloseCompound = propval?.ToString() : Return True
                Case "Hemicellulose Compound" : HemicelluloseCompound = propval?.ToString() : Return True
                Case "Lignin Compound" : LigninCompound = propval?.ToString() : Return True
                Case "Glucose Compound" : GlucoseCompound = propval?.ToString() : Return True
                Case "Xylose Compound" : XyloseCompound = propval?.ToString() : Return True
                Case "Furfural Compound" : FurfuralCompound = propval?.ToString() : Return True
                Case "HMF Compound" : HMFCompound = propval?.ToString() : Return True
                Case "Acetic Acid Compound" : AceticAcidCompound = propval?.ToString() : Return True
                Case "Water Compound" : WaterCompound = propval?.ToString() : Return True
                Case "Soluble Lignin Compound" : SolubleLigninCompound = propval?.ToString() : Return True
                Case "Cellulose Conversion" : CelluloseConversion = d : Return True
                Case "Glucose to HMF" : GlucoseToHMF = d : Return True
                Case "Hemicellulose Conversion" : HemicelluloseConversion = d : Return True
                Case "Xylose to Furfural" : XyloseToFurfural = d : Return True
                Case "Lignin Solubilization" : LigninSolubilization = d : Return True
                Case "Acetic Acid Yield on Hemi" : AceticAcidYieldOnHemi = d : Return True
                Case Else : Return MyBase.SetPropertyValue(prop, propval, su)
            End Select
        End Function

        ' IExternalUnitOperation
        Private ReadOnly Property IEUO_Name As String Implements IExternalUnitOperation.Name
            Get
                Return GetDisplayName()
            End Get
        End Property
        Private ReadOnly Property IEUO_Description As String Implements IExternalUnitOperation.Description
            Get
                Return GetDisplayDescription()
            End Get
        End Property
        Public ReadOnly Property Prefix As String Implements IExternalUnitOperation.Prefix
            Get
                Return "PRE-"
            End Get
        End Property
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance
            Return New Reactor_Pretreatment()
        End Function
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            Dim nf = FlowSheet.FlowsheetOptions.NumberFormat
            Dim compIds = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()

            container.CreateAndAddLabelRow("Pretreatment Technology")

            container.CreateAndAddDropDownRow("Technology",
                                              New List(Of String)({"Dilute Acid", "Steam Explosion", "Alkaline", "Organosolv"}),
                                              CInt(Technology),
                                              Sub(dd, e)
                                                  Technology = CType(dd.SelectedIndex, PretreatmentType)
                                                  FlowSheet.RequestCalculation()
                                              End Sub)

            container.CreateAndAddLabelRow("Operating Conditions")

            container.CreateAndAddTextBoxRow(nf, "Severity log(R0)", SeverityLogR0,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SeverityLogR0 = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Residence Time (s)", ResidenceTime_s,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     ResidenceTime_s = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Solids Loading (w. frac.)", SolidsLoading_wfrac,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     SolidsLoading_wfrac = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Reaction Conversions (0-1)")

            container.CreateAndAddTextBoxRow(nf, "Cellulose to Glucose", CelluloseConversion,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     CelluloseConversion = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Glucose to HMF (side)", GlucoseToHMF,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     GlucoseToHMF = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Hemicellulose to Xylose", HemicelluloseConversion,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     HemicelluloseConversion = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Xylose to Furfural (side)", XyloseToFurfural,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     XyloseToFurfural = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Lignin Solubilization", LigninSolubilization,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     LigninSolubilization = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddTextBoxRow(nf, "Acetic Acid Yield on Hemicellulose", AceticAcidYieldOnHemi,
                                             Sub(tb, e)
                                                 If tb.Text.IsValidDoubleExpression() Then
                                                     AceticAcidYieldOnHemi = tb.Text.ParseExpressionToDouble()
                                                     FlowSheet.RequestCalculation()
                                                 End If
                                             End Sub)

            container.CreateAndAddLabelRow("Compound Mapping")

            Dim addCompoundDropdownA =
                Sub(label As String, currentValue As String, setter As Action(Of String))
                    Dim idx = compIds.IndexOf(currentValue)
                    container.CreateAndAddDropDownRow(label,
                                                      New List(Of String)(New String() {"(none)"}.Concat(compIds)),
                                                      If(idx < 0, 0, idx + 1),
                                                      Sub(dd, e)
                                                          setter(If(dd.SelectedIndex > 0, compIds(dd.SelectedIndex - 1), ""))
                                                          FlowSheet.RequestCalculation()
                                                      End Sub)
                End Sub

            addCompoundDropdownA("Cellulose", CelluloseCompound, Sub(v) CelluloseCompound = v)
            addCompoundDropdownA("Hemicellulose", HemicelluloseCompound, Sub(v) HemicelluloseCompound = v)
            addCompoundDropdownA("Lignin (insoluble)", LigninCompound, Sub(v) LigninCompound = v)
            addCompoundDropdownA("Lignin (soluble)", SolubleLigninCompound, Sub(v) SolubleLigninCompound = v)
            addCompoundDropdownA("Glucose", GlucoseCompound, Sub(v) GlucoseCompound = v)
            addCompoundDropdownA("Xylose", XyloseCompound, Sub(v) XyloseCompound = v)
            addCompoundDropdownA("Furfural", FurfuralCompound, Sub(v) FurfuralCompound = v)
            addCompoundDropdownA("HMF", HMFCompound, Sub(v) HMFCompound = v)
            addCompoundDropdownA("Acetic Acid", AceticAcidCompound, Sub(v) AceticAcidCompound = v)
            addCompoundDropdownA("Water", WaterCompound, Sub(v) WaterCompound = v)

        End Sub

        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors
            If GraphicObject Is Nothing Then Return
            Dim w = GraphicObject.Width, h = GraphicObject.Height
            Dim gx = GraphicObject.X, gy = GraphicObject.Y
            If GraphicObject.InputConnectors.Count = 1 AndAlso GraphicObject.OutputConnectors.Count = 1 Then
                GraphicObject.InputConnectors(0).Position = New Point(gx, gy + 0.5 * h)
                GraphicObject.InputConnectors(0).ConnectorName = "Biomass Slurry"
                GraphicObject.OutputConnectors(0).Position = New Point(gx + w, gy + 0.5 * h)
                GraphicObject.OutputConnectors(0).ConnectorName = "Pretreated Slurry"
            Else
                GraphicObject.InputConnectors.Clear()
                GraphicObject.OutputConnectors.Clear()
                GraphicObject.InputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx, gy + 0.5 * h), .Type = ConType.ConIn,
                    .Direction = ConDir.Right, .ConnectorName = "Biomass Slurry"})
                GraphicObject.OutputConnectors.Add(New ConnectionPoint With {
                    .Position = New Point(gx + w, gy + 0.5 * h), .Type = ConType.ConOut,
                    .Direction = ConDir.Right, .ConnectorName = "Pretreated Slurry"})
            End If
            GraphicObject.EnergyConnector.Position = New Point(gx + 0.5 * w, gy + h)
            GraphicObject.EnergyConnector.Direction = ConDir.Up
            GraphicObject.EnergyConnector.Active = False
        End Sub

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _photoImage As SKImage

        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw
            If GraphicObject Is Nothing Then Return
            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)
            If GraphicObject.DrawMode = 2 Then
                If UnitOperations.BioOpsDrawHelper.TryDrawPhotorealistic(canvas,
                    GraphicObject.X, GraphicObject.Y, GraphicObject.Width, GraphicObject.Height,
                    "pretreatment_photo", _photoImage) Then Return
            End If
            DrawIcon(canvas, CSng(GraphicObject.X), CSng(GraphicObject.Y),
                     CSng(GraphicObject.Width), CSng(GraphicObject.Height),
                     GraphicObject.DrawMode = 1)
        End Sub

        Private Shared Sub DrawIcon(canvas As SKCanvas, gx As Single, gy As Single, w As Single, h As Single, Optional mono As Boolean = False)
            ' Horizontal jacketed pretreatment reactor on saddles with steam inlets + discharge nozzle.
            Dim saddle1 As New SKRect(gx + 0.18F * w, gy + 0.78F * h, gx + 0.32F * w, gy + h)
            Dim saddle2 As New SKRect(gx + 0.68F * w, gy + 0.78F * h, gx + 0.82F * w, gy + h)
            UnitOperations.BioOpsDrawHelper.DrawSkid(canvas, saddle1, mono)
            UnitOperations.BioOpsDrawHelper.DrawSkid(canvas, saddle2, mono)
            Dim vessel As New SKRect(gx + 0.1F * w, gy + 0.3F * h, gx + 0.9F * w, gy + 0.8F * h)
            UnitOperations.BioOpsDrawHelper.DrawHorizontalTank(canvas, vessel, mono)
            ' jacket outline (double line)
            Using s As New SKPaint With {.Color = If(mono, New SKColor(80, 80, 80), New SKColor(90, 110, 135)), .Style = SKPaintStyle.Stroke, .StrokeWidth = 0.9F, .IsAntialias = True}
                canvas.DrawRect(New SKRect(vessel.Left + 3, vessel.Top + 3, vessel.Right - 3, vessel.Bottom - 3), s)
            End Using
            ' two steam inlet nozzles on top (shorter, with flange at base on vessel)
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.3F * w, gy + 0.15F * h), New SKPoint(gx + 0.3F * w, gy + 0.3F * h), 0.035F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.3F * w, gy + 0.3F * h, 0.1F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawPipe(canvas, New SKPoint(gx + 0.7F * w, gy + 0.15F * h), New SKPoint(gx + 0.7F * w, gy + 0.3F * h), 0.035F * w, mono)
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.7F * w, gy + 0.3F * h, 0.1F * w, mono)
            ' right-side coupling bay + motor (screw drive)
            Dim coupling As New SKRect(gx + 0.86F * w, gy + 0.45F * h, gx + 0.92F * w, gy + 0.63F * h)
            Using cpl As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrMetalMid(mono), .IsAntialias = True}
                canvas.DrawRect(coupling, cpl)
            End Using
            Using stroke As New SKPaint With {.Color = UnitOperations.BioOpsDrawHelper.ClrStroke(mono), .Style = SKPaintStyle.Stroke, .StrokeWidth = 1.0F, .IsAntialias = True}
                canvas.DrawRect(coupling, stroke)
            End Using
            UnitOperations.BioOpsDrawHelper.DrawFlange(canvas, gx + 0.86F * w, gy + 0.54F * h, 0.14F * w, mono)
            Dim motor As New SKRect(gx + 0.92F * w, gy + 0.42F * h, gx + w, gy + 0.66F * h)
            UnitOperations.BioOpsDrawHelper.DrawMotor(canvas, motor, mono)
        End Sub

    End Class

End Namespace
