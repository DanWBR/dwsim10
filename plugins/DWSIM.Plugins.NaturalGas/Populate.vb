'Natural Gas Properties Plugin for DWSIM (cross-platform Avalonia edition)
'Copyright 2010-2026 Daniel Wagner

Imports FileHelpers
Imports System.Linq
Imports System.Reflection
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.SharedClasses.SystemsOfUnits
Imports DWSIM.Thermodynamics
Imports DWSIM.SharedClasses
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports DWSIM.Interfaces
Imports DWSIM.UI.Shared.Avalonia

Public Class Populate

    'collection of component mass heating values
    Public dmc As New Dictionary(Of String, datamass)

    'collection of component volumetric heating values
    Public dvc As New Dictionary(Of String, datavol)

    Private Shared Function ReadResource(name As String) As String
        Using s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            If s Is Nothing Then Return ""
            Using r As New IO.StreamReader(s)
                Return r.ReadToEnd()
            End Using
        End Using
    End Function

    Public Sub Populate(fsheet As IFlowsheet, panel As AvaloniaEditorPanel)

        'read data from the embedded text tables
        Dim engine As New FileHelperEngine(Of datamass)()
        Dim compsm As datamass() = engine.ReadString(ReadResource("pc_massico.txt"))
        dmc = New Dictionary(Of String, datamass)
        For Each d In compsm
            If d.dbname <> "" Then dmc.Add(d.dbname, d)
        Next
        Dim engine2 As New FileHelperEngine(Of datavol)()
        Dim compsv As datavol() = engine2.ReadString(ReadResource("pc_volumetrico.txt"))
        dvc = New Dictionary(Of String, datavol)
        For Each d In compsv
            If d.dbname <> "" Then dvc.Add(d.dbname, d)
        Next

        If fsheet.GetSelectedFlowsheetSimulationObject(Nothing) Is Nothing Then
            panel.CreateAndAddLabelRow("Calculation Results")
            panel.CreateAndAddLabelRow2("You need to select a Material Stream before opening this plugin.")
            Exit Sub
        End If

        'check if the selected object is a material stream.
        If fsheet.GetSelectedFlowsheetSimulationObject(Nothing).GraphicObject.ObjectType = ObjectType.MaterialStream Then

            'get a reference to the material stream graphic object.
            Dim gobj As IGraphicObject = fsheet.GetSelectedFlowsheetSimulationObject(Nothing).GraphicObject

            'get a reference to the material stream base class.
            Dim dobj As Streams.MaterialStream = CType(fsheet.SimulationObjects(gobj.Name), Streams.MaterialStream)

            'declare heating value variables.
            Dim hhv25m As Double = 0
            Dim hhv20m As Double = 0
            Dim hhv15m As Double = 0
            Dim hhv0m As Double = 0
            Dim lhv25m As Double = 0
            Dim lhv20m As Double = 0
            Dim lhv15m As Double = 0
            Dim lhv0m As Double = 0
            Dim hhv1515v As Double = 0
            Dim hhv00v As Double = 0
            Dim hhv2020v As Double = 0
            Dim lhv1515v As Double = 0
            Dim lhv00v As Double = 0
            Dim lhv2020v As Double = 0
            Dim hhv1515vr As Double = 0
            Dim hhv00vr As Double = 0
            Dim hhv2020vr As Double = 0
            Dim lhv1515vr As Double = 0
            Dim lhv00vr As Double = 0
            Dim lhv2020vr As Double = 0

            'declare wobbe index variables.
            Dim iw0 As Double = 0
            Dim iw15 As Double = 0
            Dim iw20 As Double = 0
            Dim iw0r As Double = 0
            Dim iw15r As Double = 0
            Dim iw20r As Double = 0

            'methane number variables
            Dim mon, mn, mn2, xc1, xc2, xc3, xc4, xnc4, xic4, xnc5, xic5, xc6, xc7, xc8, xc9, xco2, xn2 As Double
            Dim c1, c2, c3, ic4, nc4, ic5, nc5, nc6, nc7, nc8, nc9, co2, n2 As ICompound

            'molecular weight
            Dim mw As Double = dobj.Phases(0).Properties.molecularWeight.GetValueOrDefault

            Dim vx(dobj.Phases(0).Compounds.Count - 1), vxnw(dobj.Phases(0).Compounds.Count - 1), vxw(dobj.Phases(0).Compounds.Count - 1) As Double
            Dim i As Integer = 0
            Dim iw As Integer = -1
            For Each c As Compound In dobj.Phases(0).Compounds.Values
                vx(i) = c.MoleFraction.GetValueOrDefault
                If c.ConstantProperties.CAS_Number = "7732-18-5" Then
                    iw = i
                End If
                i += 1
            Next

            'wdp    =   Water dew point (real, not reliable)
            'hdp    =   Hydrocarbon dew point
            '           Calculated using the dry composition and a normal PV-Flash.
            'iwdp   =   Ideal water dew point
            '           Calculated based on the Raoult's law:
            '           xiPisat = yiP => Pisat = yiP/xi
            '           After calculating Pisat (water partial vapor pressure), use the AUX_TSATi function
            '           to return the saturation temperature (dew point).
            Dim wdp, hdp, iwdp, wc0, wc15, wc20, wcb, wdp1, iwdp1, hdp1 As Double

            Dim dewpcalc = New DewPointFinder()

            Try
                dobj.PropertyPackage.CurrentMaterialStream = dobj
                Dim res1 = dewpcalc.CalcDewPoints(vx, dobj.Phases(0).Properties.pressure.GetValueOrDefault, dobj.PropertyPackage)
                If iw <> -1 Then
                    wdp = res1("W")
                    iwdp = res1("IW")
                End If
                hdp = res1("H")
            Catch ex As Exception
                fsheet.ShowMessage(ex.ToString, IFlowsheet.MessageType.GeneralError)
            End Try

            Try
                dobj.PropertyPackage.CurrentMaterialStream = dobj
                Dim res1 = dewpcalc.CalcDewPoints(vx, 101325, dobj.PropertyPackage)
                If iw <> -1 Then
                    wdp1 = res1("W")
                    iwdp1 = res1("IW")
                End If
                hdp1 = res1("H")
            Catch ex As Exception
                fsheet.ShowMessage(ex.ToString, IFlowsheet.MessageType.GeneralError)
            End Try

            'declare a temporary material stream so we can do calculations without messing with the simulation.
            Dim tmpms As New Streams.MaterialStream("", "")
            tmpms = CType(dobj.Clone, Streams.MaterialStream)
            tmpms.PropertyPackage = dobj.PropertyPackage
            tmpms.PropertyPackage.CurrentMaterialStream = tmpms

            'set stream pressure
            tmpms.Phases(0).Properties.pressure = 101325

            'compressibility factors and specific gravities
            Dim z0, z15, z20, d0, d15, d20, d As Double

            'ideal gas specific gravity
            d = mw / 28.9626

            tmpms.Phases(0).Properties.temperature = 273.15 + 0
            tmpms.PropertyPackage.DW_CalcPhaseProps(DWSIM.Thermodynamics.PropertyPackages.Phase.Vapor)
            z0 = tmpms.Phases(2).Properties.compressibilityFactor.GetValueOrDefault
            d0 = d / z0

            tmpms.Phases(0).Properties.temperature = 273.15 + 15.56
            tmpms.PropertyPackage.DW_CalcPhaseProps(DWSIM.Thermodynamics.PropertyPackages.Phase.Vapor)
            z15 = tmpms.Phases(2).Properties.compressibilityFactor.GetValueOrDefault
            d15 = d / z15

            tmpms.Phases(0).Properties.temperature = 273.15 + 20
            tmpms.PropertyPackage.DW_CalcPhaseProps(DWSIM.Thermodynamics.PropertyPackages.Phase.Vapor)
            z20 = tmpms.Phases(2).Properties.compressibilityFactor.GetValueOrDefault
            d20 = d / z20

            If iw <> -1 Then
                If vx(iw) <> 0.0# Then
                    'water content in mg/m3
                    If tmpms.Phases(0).Compounds.ContainsKey("Agua") Then
                        wcb = vx(iw) * tmpms.Phases(0).Compounds("Agua").ConstantProperties.Molar_Weight * 1000 * 1000
                    ElseIf tmpms.Phases(0).Compounds.ContainsKey("Water") Then
                        wcb = vx(iw) * tmpms.Phases(0).Compounds("Water").ConstantProperties.Molar_Weight * 1000 * 1000
                    End If
                    wc0 = wcb / (1 * z0 * 8314.47 * (273.15 + 0) / 101325)
                    wc15 = wcb / (1 * z15 * 8314.47 * (273.15 + 15.56) / 101325)
                    wc20 = wcb / (1 * z20 * 8314.47 * (273.15 + 20) / 101325)
                End If
            End If

            'calculation of heating values at various conditions
            For Each c As Compound In dobj.Phases(0).Compounds.Values
                If dmc.ContainsKey(c.ConstantProperties.CAS_Number) Then
                    hhv25m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).sup25 * 1000
                    hhv20m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).sup20 * 1000
                    hhv15m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).sup15 * 1000
                    hhv0m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).sup0 * 1000
                    lhv25m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).inf25 * 1000
                    lhv20m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).inf20 * 1000
                    lhv15m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).inf15 * 1000
                    lhv0m += c.MoleFraction.GetValueOrDefault * c.ConstantProperties.Molar_Weight / mw * dmc(c.ConstantProperties.CAS_Number).inf0 * 1000
                End If
                If dvc.ContainsKey(c.ConstantProperties.CAS_Number) Then
                    hhv1515v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).sup1515 * 1000
                    hhv00v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).sup00 * 1000
                    hhv2020v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).sup2020 * 1000
                    lhv1515v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).inf1515 * 1000
                    lhv00v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).inf00 * 1000
                    lhv2020v += c.MoleFraction.GetValueOrDefault * dvc(c.ConstantProperties.CAS_Number).inf2020 * 1000
                End If
            Next

            'real gas heating values
            hhv1515vr = hhv1515v / z15
            hhv00vr = hhv00v / z0
            hhv2020vr = hhv2020v / z20
            lhv1515vr = lhv1515v / z15
            lhv00vr = lhv00v / z0
            lhv2020vr = lhv2020v / z20

            'ideal gas wobbe indexes
            iw0 = hhv00v / d ^ 0.5
            iw15 = hhv1515v / d ^ 0.5
            iw20 = hhv2020v / d ^ 0.5

            'real gas wobbe indexes
            iw0r = hhv00vr / d0 ^ 0.5
            iw15r = hhv1515vr / d15 ^ 0.5
            iw20r = hhv2020vr / d20 ^ 0.5

            'methane number
            c1 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "74-82-8").FirstOrDefault
            c2 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "74-84-0").FirstOrDefault
            c3 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "74-98-6").FirstOrDefault
            nc4 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "106-97-8").FirstOrDefault
            ic4 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "75-28-5").FirstOrDefault
            nc5 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "109-66-0").FirstOrDefault
            ic5 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "78-78-4").FirstOrDefault
            nc6 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "110-54-3").FirstOrDefault
            nc7 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "142-82-5").FirstOrDefault
            nc8 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "111-65-9").FirstOrDefault
            nc9 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "111-84-2").FirstOrDefault
            co2 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "124-38-9").FirstOrDefault
            n2 = (From c As ICompound In dobj.Phases(0).Compounds.Values Select c Where c.ConstantProperties.CAS_Number = "7727-37-9").FirstOrDefault

            If Not c1 Is Nothing Then xc1 = c1.MoleFraction.GetValueOrDefault
            If Not c2 Is Nothing Then xc2 = c2.MoleFraction.GetValueOrDefault
            If Not c3 Is Nothing Then xc3 = c3.MoleFraction.GetValueOrDefault
            If Not nc4 Is Nothing Then
                xc4 = nc4.MoleFraction.GetValueOrDefault
                xnc4 = nc4.MoleFraction.GetValueOrDefault
            End If
            If Not ic4 Is Nothing Then
                xc4 += ic4.MoleFraction.GetValueOrDefault
                xic4 = ic4.MoleFraction.GetValueOrDefault
            End If
            If Not nc5 Is Nothing Then xnc5 = nc5.MoleFraction.GetValueOrDefault
            If Not ic5 Is Nothing Then xic5 = ic5.MoleFraction.GetValueOrDefault
            If Not nc6 Is Nothing Then xc6 = nc6.MoleFraction.GetValueOrDefault
            If Not nc7 Is Nothing Then xc7 = nc7.MoleFraction.GetValueOrDefault
            If Not nc8 Is Nothing Then xc8 = nc8.MoleFraction.GetValueOrDefault
            If Not nc9 Is Nothing Then xc9 = nc9.MoleFraction.GetValueOrDefault
            If Not co2 Is Nothing Then xco2 = co2.MoleFraction.GetValueOrDefault
            If Not n2 Is Nothing Then xn2 = n2.MoleFraction.GetValueOrDefault

            mon = 137.78 * xc1 + 29.948 * xc2 - 18.193 * xc3 - 167.062 * xc4 + 181.233 * xco2 + 26.994 * xn2
            mn = 1.445 * mon - 103.42

            Dim mnc As New MethaneNumberInterface
            Dim mncr = mnc.Calculate(xc1, xc2, xc3, xic4, xnc4, xic5, xnc5, xc6, xc7, xc8, xc9, xn2, xco2, 0, 0, 0, 0)

            mn2 = mncr.dblMethaneNumber

            'get a reference to the current number format.
            Dim nf As String = fsheet.FlowsheetOptions.NumberFormat

            'get a reference to the current unit system.
            Dim su As SystemsOfUnits.Units = CType(fsheet.FlowsheetOptions.SelectedUnitSystem, Units)

            panel.CreateAndAddLabelRow("Calculation Results")
            panel.CreateAndAddTwoLabelsRow2("Selected Stream", gobj.Tag)
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Molar Weight", Format(mw, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Specific Gravity", Format(d, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Compressibility Factor @ NC", Format(z0, nf))
            panel.CreateAndAddTwoLabelsRow("Compressibility Factor @ SC", Format(z15, nf))
            panel.CreateAndAddTwoLabelsRow("Compressibility Factor @ BR", Format(z20, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Specific Gravity @ NC", Format(d0, nf))
            panel.CreateAndAddTwoLabelsRow("Specific Gravity @ SC", Format(d15, nf))
            panel.CreateAndAddTwoLabelsRow("Specific Gravity @ BR", Format(d20, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Mass LHV @ 0 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, lhv0m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass LHV @ 15 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, lhv15m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass LHV @ 20 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, lhv20m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass LHV @ 25 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, lhv25m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass HHV @ 0 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, hhv0m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass HHV @ 15 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, hhv15m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass HHV @ 20 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, hhv20m), nf))
            panel.CreateAndAddTwoLabelsRow("Mass HHV @ 25 °C (" & su.enthalpy & ")", Format(Converter.ConvertFromSI(su.enthalpy, hhv25m), nf))
            panel.CreateAndAddTwoLabelsRow("Molar LHV @ 0 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, lhv0m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar LHV @ 15 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, lhv15m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar LHV @ 20 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, lhv20m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar LHV @ 25 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, lhv25m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar HHV @ 0 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, hhv0m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar HHV @ 15 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, hhv15m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar HHV @ 20 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, hhv20m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Molar HHV @ 25 °C (" & su.molar_enthalpy & ")", Format(Converter.ConvertFromSI(su.molar_enthalpy, hhv25m * mw), nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. LHV @ NC (kJ/m3)", Format(lhv00v, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. LHV @ SC (kJ/m3)", Format(lhv1515v, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. LHV @ BR (kJ/m3)", Format(lhv2020v, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. HHV @ NC (kJ/m3)", Format(hhv00v, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. HHV @ SC (kJ/m3)", Format(hhv1515v, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Vol. HHV @ BR (kJ/m3)", Format(hhv2020v, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. LHV @ NC (kJ/m3)", Format(lhv00vr, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. LHV @ SC (kJ/m3)", Format(lhv1515vr, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. LHV @ BR (kJ/m3)", Format(lhv2020vr, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. HHV @ NC (kJ/m3)", Format(hhv00vr, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. HHV @ SC (kJ/m3)", Format(hhv1515vr, nf))
            panel.CreateAndAddTwoLabelsRow("Vol. HHV @ BR (kJ/m3)", Format(hhv2020vr, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Wobbe Index @ NC (kJ/m3)", Format(iw0, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Wobbe Index @ SC (kJ/m3)", Format(iw15, nf))
            panel.CreateAndAddTwoLabelsRow("Ideal Gas Wobbe Index @ BR (kJ/m3)", Format(iw20, nf))
            panel.CreateAndAddTwoLabelsRow("Wobbe Index @ NC (kJ/m3)", Format(iw0r, nf))
            panel.CreateAndAddTwoLabelsRow("Wobbe Index @ SC (kJ/m3)", Format(iw15r, nf))
            panel.CreateAndAddTwoLabelsRow("Wobbe Index @ BR (kJ/m3)", Format(iw20r, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Motor Octane Number (MON)", Format(mon, nf))
            panel.CreateAndAddTwoLabelsRow("Methane Number (H/C Ratio Method)", Format(mn, nf))
            panel.CreateAndAddTwoLabelsRow("Methane Number (New Method)", Format(mn2, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("HC Dew Point @ P (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, hdp), nf))
            panel.CreateAndAddTwoLabelsRow("Water Dew Point @ P (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, wdp), nf))
            panel.CreateAndAddTwoLabelsRow("Water Dew Point (Ideal) @ P (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, iwdp), nf))
            panel.CreateAndAddTwoLabelsRow("HC Dew Point @ 1 atm (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, hdp1), nf))
            panel.CreateAndAddTwoLabelsRow("Water Dew Point @ 1 atm (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, wdp1), nf))
            panel.CreateAndAddTwoLabelsRow("Water Dew Point (Ideal) @ 1 atm (" & su.temperature & ")", Format(Converter.ConvertFromSI(su.temperature, iwdp1), nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddTwoLabelsRow("Water Content @ NC (mg/m3)", Format(wc0, nf))
            panel.CreateAndAddTwoLabelsRow("Water Content @ SC (mg/m3)", Format(wc15, nf))
            panel.CreateAndAddTwoLabelsRow("Water Content @ BR (mg/m3)", Format(wc20, nf))
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddEmptySpace()
            panel.CreateAndAddDescriptionRow("* SC = Standard Conditions (T = 15.56 °C, P = 1 atm)")
            panel.CreateAndAddDescriptionRow("* BR = CNTP (T = 20 °C, P = 1 atm)")
            panel.CreateAndAddDescriptionRow("* NC = Normal Conditions (T = 0 °C, P = 1 atm)")

        End If

    End Sub

End Class
