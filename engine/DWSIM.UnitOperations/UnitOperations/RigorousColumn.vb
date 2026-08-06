'    Rigorous Columns (Distillation and Absorption) Unit Operations
'    Copyright 2008-2022 Daniel Wagner O. de Medeiros
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

Imports DWSIM.MathOps.MathEx
Imports System.Math
Imports Mapack
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.SolvingMethods
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps
Imports DWSIM.MathOps
Imports DWSIM.DrawingTools
Imports OxyPlot
Imports OxyPlot.Axes
Imports DotNumerics.Optimization
Imports DWSIM.MathOps.MathEx.Optimization
Imports DWSIM.MathOps.MathEx.BrentOpt
Imports DWSIM.Thermodynamics.PropertyPackages.Auxiliary.FlashAlgorithms
Imports DWSIM.UnitOperations.UnitOperations.Column
Imports DWSIM.UnitOperations.Streams
Imports DWSIM.Thermodynamics.PropertyPackages

Namespace UnitOperations

    <Serializable()> Public Class DistillationColumn

        Inherits Column

        Public Property TotalCondenserSubcoolingDeltaT As Double = 0.0

        Public Property ReboiledAbsorber As Boolean = False

        Public Property RefluxedAbsorber As Boolean = False

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String, fs As IFlowsheet)
            MyBase.New(name, description, fs)
            Me.ColumnType = ColType.DistillationColumn
            MyBase.AddStages()
            For k2 = 0 To Me.Stages.Count - 1
                Me.Stages(k2).P = 101325
            Next
        End Sub

        Public Sub ConvertToComplex()

            If FlowSheet IsNot Nothing Then

                Dim hascondenser = Not ReboiledAbsorber
                Dim hasreboiler = Not RefluxedAbsorber

                Dim tag = GraphicObject.Tag

                Dim vapstreamID, diststreamID, bottomstreamID, cdutyID, rdutyID As StreamInformation
                Dim vapstream, diststream, bottomstream As MaterialStream
                Dim cdutystream, rdutystream As EnergyStream

                vapstreamID = MaterialStreams.Values.Where(Function(m) m.StreamBehavior = StreamInformation.Behavior.OverheadVapor).FirstOrDefault()
                diststreamID = MaterialStreams.Values.Where(Function(m) m.StreamBehavior = StreamInformation.Behavior.Distillate).FirstOrDefault()
                bottomstreamID = MaterialStreams.Values.Where(Function(m) m.StreamBehavior = StreamInformation.Behavior.BottomsLiquid).FirstOrDefault()

                cdutyID = EnergyStreams.Values.Where(Function(m) m.StreamBehavior = StreamInformation.Behavior.Distillate).FirstOrDefault()
                rdutyID = EnergyStreams.Values.Where(Function(m) m.StreamBehavior = StreamInformation.Behavior.BottomsLiquid).FirstOrDefault()

                If vapstreamID IsNot Nothing Then vapstream = FlowSheet.SimulationObjects(vapstreamID.StreamID)
                If diststreamID IsNot Nothing Then diststream = FlowSheet.SimulationObjects(diststreamID.StreamID)
                If bottomstreamID IsNot Nothing Then bottomstream = FlowSheet.SimulationObjects(bottomstreamID.StreamID)

                If cdutyID IsNot Nothing Then cdutystream = FlowSheet.SimulationObjects(cdutyID.StreamID)
                If rdutyID IsNot Nothing Then rdutystream = FlowSheet.SimulationObjects(rdutyID.StreamID)

                Dim dx, dy As Double

                dx = 0
                dy = 0

                If hascondenser Then

                    Stages.Remove(Stages.First())

                    Dim cond = FlowSheet.AddObject(GraphicObjects.ObjectType.Vessel, dx + Right + 21, dy + Top - 34, tag + "_cond_vessel")
                    Dim condspl = FlowSheet.AddObject(GraphicObjects.ObjectType.Splitter, dx + Right + 157, dy + Top - 9, tag & "_cond_split")
                    Dim condpump = FlowSheet.AddObject(GraphicObjects.ObjectType.Pump, dx + Right + 163, dy + Top + 50, tag & "_cond_pump")
                    condpump.GraphicObject.FlippedH = True
                    Dim condvalve = FlowSheet.AddObject(GraphicObjects.ObjectType.Valve, dx + Right + 36, dy + Top + 44, tag & "_cond_valve")
                    condvalve.GraphicObject.FlippedH = True

                    FlowSheet.Connect(cond, condspl, 1, 0)
                    FlowSheet.Connect(condspl, condpump, 1, 0)
                    FlowSheet.Connect(condpump, condvalve, 0, 0)

                    If cdutyID IsNot Nothing Then

                        FlowSheet.Disconnect(Me, cdutystream)
                        EnergyStreams.Remove(cdutyID.ID)

                        FlowSheet.Connect(cond, cdutystream, 4, 0)

                    End If

                    If vapstreamID IsNot Nothing Then

                        FlowSheet.Disconnect(Me, vapstream)
                        FlowSheet.Connect(cond, vapstream, 0, 0)
                        MaterialStreams.Remove(vapstreamID.ID)

                    End If

                    If diststreamID IsNot Nothing Then

                        FlowSheet.Disconnect(Me, diststream)
                        FlowSheet.Connect(condspl, diststream, 0, 0)

                        Dim condrec = FlowSheet.AddObject(GraphicObjects.ObjectType.OT_Recycle, dx + Left - 75, dy + Top + 10, tag & "_cond_rec")

                        FlowSheet.Connect(condvalve, condrec, -1, -1)
                        FlowSheet.Connect(condrec, Me, -1, -1)

                        MaterialStreams.Remove(diststreamID.ID)

                        Dim newdist = condrec.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Owner

                        Dim si As New StreamInformation With {.AssociatedStage = Stages(0).Name, .ID = Guid.NewGuid().ToString(),
                            .StreamBehavior = StreamInformation.Behavior.Feed,
                            .StreamPhase = StreamInformation.Phase.L, .StreamType = StreamInformation.Type.Material,
                            .StreamPosition = StreamInformation.Position.Above, .StreamID = newdist.Name}

                        MaterialStreams.Add(si.ID, si)

                        FlowSheet.Connect(Me, cond, -1, -1)

                        Dim overhs As MaterialStream = cond.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Owner

                        si = New StreamInformation With {.AssociatedStage = Stages(0).Name, .ID = Guid.NewGuid().ToString(),
                            .StreamBehavior = StreamInformation.Behavior.OverheadVapor,
                            .StreamPhase = StreamInformation.Phase.V, .StreamType = StreamInformation.Type.Material,
                            .StreamPosition = StreamInformation.Position.Above, .StreamID = overhs.Name}

                    End If

                    ReboiledAbsorber = True

                End If

                'cond    21 -34
                'spl1    157 -9
                'p1  163	50
                'v1  36	44
                'spl2    31	5
                'reb 129 -25
                'p2  181 -58
                'v2  34 -66

                If hasreboiler Then

                    Stages.Remove(Stages.Last())

                    Dim rebspl = FlowSheet.AddObject(GraphicObjects.ObjectType.Splitter, Right + 31, Bottom + 5, tag + "_reb_split")
                    Dim reb = FlowSheet.AddObject(GraphicObjects.ObjectType.Vessel, Right + 129, Bottom - 25, tag + "_reb_vessel")
                    Dim rebpump = FlowSheet.AddObject(GraphicObjects.ObjectType.Pump, Right + 181, Bottom - 58, tag + "_reb_pump")
                    rebpump.GraphicObject.FlippedH = True
                    Dim rebvalve = FlowSheet.AddObject(GraphicObjects.ObjectType.Valve, Right + 34, Bottom - 66, tag + "_reb_valve")
                    rebvalve.GraphicObject.FlippedH = True

                    FlowSheet.Connect(rebspl, reb, 0, 0)
                    FlowSheet.Connect(reb, rebpump, 1, 0)
                    FlowSheet.Connect(rebpump, rebvalve, 0, 0)

                    If rdutyID IsNot Nothing Then

                        FlowSheet.Disconnect(Me, rdutystream)
                        EnergyStreams.Remove(rdutyID.ID)

                        FlowSheet.Connect(rdutystream, reb, 0, 6)

                    End If

                    If bottomstreamID IsNot Nothing Then

                        FlowSheet.Disconnect(Me, bottomstream)
                        FlowSheet.Connect(rebspl, bottomstream, 1, 0)
                        FlowSheet.Connect(Me, rebspl, 1, 0)

                        MaterialStreams.Remove(bottomstreamID.ID)

                        Dim rebrec = FlowSheet.AddObject(GraphicObjects.ObjectType.OT_Recycle, dx + Left - 75, dy + Bottom - 10, tag & "_reb_rec")

                        FlowSheet.Connect(rebvalve, rebrec, -1, -1)
                        FlowSheet.Connect(rebrec, Me, -1, -1)

                        Dim newrebfeed As MaterialStream = rebrec.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Owner

                        Dim si As New StreamInformation With {.AssociatedStage = Stages.Last.Name, .ID = Guid.NewGuid().ToString(),
                            .StreamBehavior = StreamInformation.Behavior.Feed,
                            .StreamPhase = StreamInformation.Phase.B, .StreamType = StreamInformation.Type.Material,
                            .StreamPosition = StreamInformation.Position.Above, .StreamID = newrebfeed.Name}

                        MaterialStreams.Add(si.ID, si)

                        Dim newreb As MaterialStream = rebspl.GraphicObject.InputConnectors(0).AttachedConnector.AttachedFrom.Owner

                        si = New StreamInformation With {.AssociatedStage = Stages.Last.Name, .ID = Guid.NewGuid().ToString(),
                            .StreamBehavior = StreamInformation.Behavior.BottomsLiquid,
                            .StreamPhase = StreamInformation.Phase.B, .StreamType = StreamInformation.Type.Material,
                            .StreamPosition = StreamInformation.Position.Below, .StreamID = newreb.Name}

                        MaterialStreams.Add(si.ID, si)

                    End If

                    RefluxedAbsorber = True

                End If

                UpdateEditForm()
                FlowSheet.UpdateInterface()

            End If

        End Sub

        Public Sub ConnectFeed(feed As ISimulationObject, stagenumber As Integer)

            Dim i As Integer = 0
            Dim success As Boolean = False
            For Each con In GraphicObject.InputConnectors
                If Not con.IsAttached Then
                    FlowSheet.ConnectObjects(feed.GraphicObject, GraphicObject, 0, i)
                    Dim msi As New StreamInformation With {.ID = feed.Name, .StreamID = feed.Name,
                        .AssociatedStage = Stages(stagenumber).Name,
                        .StreamBehavior = StreamInformation.Behavior.Feed,
                        .StreamType = StreamInformation.Type.Material}
                    MaterialStreams.Add(msi.ID, msi)
                    success = True
                    Exit For
                End If
                i += 1
            Next
            If Not success Then Throw New Exception("No feed port available")

        End Sub

        Public Sub ConnectVaporProduct(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 9, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages(0).Name,
                        .StreamBehavior = StreamInformation.Behavior.OverheadVapor,
                        .StreamType = StreamInformation.Type.Material}
            MaterialStreams.Add(msi.ID, msi)

        End Sub

        Public Sub ConnectDistillate(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 0, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages(0).Name,
                        .StreamBehavior = StreamInformation.Behavior.Distillate,
                        .StreamType = StreamInformation.Type.Material}
            MaterialStreams.Add(msi.ID, msi)

        End Sub

        Public Sub ConnectBottoms(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 1, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages.Last.Name,
                        .StreamBehavior = StreamInformation.Behavior.BottomsLiquid,
                        .StreamType = StreamInformation.Type.Material}
            MaterialStreams.Add(msi.ID, msi)

        End Sub

        Public Sub ConnectCondenserDuty(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 10, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages(0).Name,
                        .StreamBehavior = StreamInformation.Behavior.Distillate,
                        .StreamType = StreamInformation.Type.Energy}
            EnergyStreams.Add(msi.ID, msi)

        End Sub

        Public Sub ConnectReboilerDuty(stream As ISimulationObject)

            FlowSheet.ConnectObjects(stream.GraphicObject, GraphicObject, 0, 10)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages.Last.Name,
                        .StreamBehavior = StreamInformation.Behavior.BottomsLiquid,
                        .StreamType = StreamInformation.Type.Energy}
            EnergyStreams.Add(msi.ID, msi)

        End Sub

        Public Sub SetCondenserSpec(spectype As String, value As Double, units As String, Optional compound As String = "")

            ParseSpecUnits(spectype, units)
            spectype = NormalizeSpecType(spectype)
            If spectype = "Reflux_Ratio" Then spectype = "Stream_Ratio"

            Dim sp As New ColumnSpec()
            [Enum].TryParse(Of ColumnSpec.SpecType)(spectype, sp.SType)
            sp.SpecValue = value
            sp.SpecUnit = units
            sp.ComponentID = compound

            Specs("C") = sp

        End Sub

        Public Sub SetReboilerSpec(spectype As String, value As Double, units As String, Optional compound As String = "")

            ParseSpecUnits(spectype, units)
            spectype = NormalizeSpecType(spectype)
            If spectype = "Boilup_Ratio" Or spectype = "BoilUp_Ratio" Then spectype = "Stream_Ratio"

            Dim sp As New ColumnSpec()
            [Enum].TryParse(Of ColumnSpec.SpecType)(spectype, sp.SType)
            sp.SpecValue = value
            sp.SpecUnit = units
            sp.ComponentID = compound

            Specs("R") = sp

        End Sub

        ''' <summary>
        ''' Takes the unit out of a spec type that carries it in parentheses, as the property grid
        ''' writes it: "Product Flow Rate (mol/s)". An explicit unit argument wins.
        ''' </summary>
        Private Shared Sub ParseSpecUnits(ByRef spectype As String, ByRef units As String)

            Dim parenStart = spectype.IndexOf("("c)
            If parenStart >= 0 Then
                Dim parenEnd = spectype.IndexOf(")"c, parenStart)
                If parenEnd > parenStart Then
                    Dim extracted = spectype.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim()
                    If String.IsNullOrEmpty(units) Then units = extracted
                    spectype = spectype.Substring(0, parenStart).Trim()
                End If
            End If

        End Sub

        ''' <summary>
        ''' Maps the common ways of naming a spec onto the enum member. Only strings that do not
        ''' already parse are touched: every ColumnSpec.SpecType name falls through unchanged. This
        ''' matters because TryParse leaves the enum at its default, Heat_Duty, when it fails, so a
        ''' name it does not recognise used to become a duty spec carrying a flow rate.
        ''' </summary>
        Private Shared Function NormalizeSpecType(spectype As String) As String

            spectype = spectype.Replace(" ", "_")

            Select Case spectype.ToLowerInvariant()
                Case "product_flow_rate", "product_molar_flow", "molar_flow_rate", "molar_flow"
                    Return "Product_Molar_Flow_Rate"
                Case "mass_flow_rate", "mass_flow", "product_mass_flow"
                    Return "Product_Mass_Flow_Rate"
                Case "component_molar_flow", "comp_molar_flow"
                    Return "Component_Molar_Flow_Rate"
                Case "component_mass_flow", "comp_mass_flow"
                    Return "Component_Mass_Flow_Rate"
                Case "component_frac", "comp_fraction", "comp_frac"
                    Return "Component_Fraction"
                Case "component_rec", "comp_recovery", "comp_rec"
                    Return "Component_Recovery"
                Case "heat", "duty", "q"
                    Return "Heat_Duty"
                Case "ratio", "reflux", "reflux_ratio", "boilup", "boilup_ratio"
                    Return "Stream_Ratio"
                Case "temp", "t"
                    Return "Temperature"
                Case "feed_rec", "recovery"
                    Return "Feed_Recovery"
                Case Else
                    Return spectype
            End Select

        End Function

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New DistillationColumn()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of DistillationColumn)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 5 To 7
                        proplist.Add("PROP_DC_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Temperature_" + CStr(i))
                    Next
                Case PropertyType.RW, PropertyType.ALL
                    For i = 2 To 2
                        proplist.Add("PROP_DC_" + CStr(i))
                    Next
                    For i = 5 To 8
                        proplist.Add("PROP_DC_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Pressure_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Efficiency_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Temperature_" + CStr(i))
                    Next
                    proplist.Add("Condenser_Specification_Value")
                    proplist.Add("Reboiler_Specification_Value")
                    proplist.Add("Global_Stage_Efficiency")
                    proplist.Add("Condenser_Calculated_Value")
                    proplist.Add("Reboiler_Calculated_Value")
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            proplist.Add(String.Format("Stream '{0}' Stage Index", streamtag))
                        Catch ex As Exception
                        End Try
                    Next
                    For Each si In MaterialStreams.Values
                        If si.StreamBehavior = StreamInformation.Behavior.Sidedraw Then
                            Try
                                Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                                proplist.Add(String.Format("Stream '{0}' Side Draw Molar Flow", streamtag))
                            Catch ex As Exception
                            End Try
                        End If
                    Next
                    proplist.Add("Estimated Height")
                    proplist.Add("Estimated Diameter")
                Case PropertyType.WR
                    For i = 2 To 2
                        proplist.Add("PROP_DC_" + CStr(i))
                    Next
                    proplist.Add("PROP_DC_7")
                    proplist.Add("PROP_DC_8")
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Pressure_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Efficiency_" + CStr(i))
                    Next
                    proplist.Add("Condenser_Specification_Value")
                    proplist.Add("Reboiler_Specification_Value")
                    proplist.Add("Global_Stage_Efficiency")
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            proplist.Add(String.Format("Stream '{0}' Stage Index", streamtag))
                        Catch ex As Exception
                        End Try
                    Next
                    For Each si In MaterialStreams.Values
                        If si.StreamBehavior = StreamInformation.Behavior.Sidedraw Then
                            Try
                                Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                                proplist.Add(String.Format("Stream '{0}' Side Draw Molar Flow", streamtag))
                            Catch ex As Exception
                            End Try
                        End If
                    Next
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then

                Return val0

            Else

                If su Is Nothing Then su = New SystemsOfUnits.SI

                Dim cv As New SystemsOfUnits.Converter
                Dim value As Object = Nothing
                Dim propidx As Integer = -1

                If prop.StartsWith("PROP_DC_") Then
                    Integer.TryParse(prop.Split("_")(2), propidx)
                End If

                Select Case propidx

                    Case 0
                        'PROP_DC_0	Condenser Pressure
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.Stages.First.P)
                    Case 1
                        'PROP_DC_1	Reboiler Pressure
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.Stages.Last.P)
                    Case 2
                        'PROP_DC_2	Condenser Pressure Drop
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.deltaP, Me.CondenserDeltaP)
                    Case 5
                        'PROP_DC_5	Condenser Duty
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.CondenserDuty)
                    Case 6
                        'PROP_DC_6	Reboiler Duty
                        value = SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.ReboilerDuty)
                    Case 7
                        'PROP_DC_7	Number of Stages
                        value = Me.NumberOfStages
                    Case 8
                        value = ColumnPressureDrop.ConvertFromSI(su.deltaP)
                End Select

                Select Case prop
                    Case "Condenser_Specification_Value"
                        value = Me.Specs("C").SpecValue
                    Case "Reboiler_Specification_Value"
                        value = Me.Specs("R").SpecValue
                    Case "Condenser_Calculated_Value"
                        value = Me.Specs("C").CalculatedValue
                    Case "Reboiler_Calculated_Value"
                        value = Me.Specs("R").CalculatedValue
                    Case "Estimated Height"
                        value = EstimatedHeight.ConvertFromSI(su.diameter)
                    Case "Estimated Diameter"
                        value = EstimatedDiameter.ConvertFromSI(su.diameter)
                End Select

                If prop.Contains("Stage_Pressure_") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.Stages(stageindex - 1).P)
                End If

                If prop.Contains("Stage_Temperature_") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Me.Stages(stageindex - 1).T)
                End If

                If prop.Contains("Stage_Efficiency_") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).Efficiency
                End If

                If prop.Contains("Global_Stage_Efficiency") Then value = "N/D"

                If prop.Contains("Stage Index") Then
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            If prop = String.Format("Stream '{0}' Stage Index", streamtag) Then
                                If si.StreamBehavior = StreamInformation.Behavior.BottomsLiquid Then
                                    value = Stages.Count - 1
                                ElseIf si.StreamBehavior = StreamInformation.Behavior.Distillate Then
                                    value = 0
                                ElseIf si.StreamBehavior = StreamInformation.Behavior.OverheadVapor Then
                                    value = 0
                                Else
                                    value = StageIndex(si.AssociatedStage)
                                End If
                                Return value
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                End If

                If prop.Contains("Side Draw Molar Flow") Then
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            If prop = String.Format("Stream '{0}' Side Draw Molar Flow", streamtag) Then
                                value = si.FlowRate.Value.ConvertFromSI(su.molarflow)
                                Return value
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                End If

                Return value
            End If

        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String

            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then

                Return u0

            Else

                If su Is Nothing Then su = New SystemsOfUnits.SI

                Dim cv As New SystemsOfUnits.Converter
                Dim value As String = ""
                Dim propidx As Integer = -1

                If prop.Contains("Stage Index") Then Return ""

                Try
                    Integer.TryParse(prop.Split("_")(2), propidx)
                Catch ex As Exception
                End Try

                Select Case propidx

                    Case 0
                        'PROP_DC_0	Condenser Pressure
                        value = su.pressure
                    Case 1
                        'PROP_DC_1	Reboiler Pressure
                        value = su.pressure
                    Case 2
                        'PROP_DC_2	Condenser Pressure Drop
                        value = su.deltaP
                    Case 4
                        value = su.molarflow
                    Case 5
                        'PROP_DC_5	Condenser Duty
                        value = su.heatflow
                    Case 6
                        'PROP_DC_6	Reboiler Duty
                        value = su.heatflow
                    Case 7
                        'PROP_DC_7	Number of Stages
                        value = ""
                    Case 8
                        value = su.deltaP
                End Select

                Select Case prop
                    Case "Condenser_Specification_Value", "Condenser_Calculated_Value"
                        value = "" 'Me.Specs("C").SpecUnit
                    Case "Reboiler_Specification_Value", "Reboiler_Calculated_Value"
                        value = "" 'Me.Specs("R").SpecUnit
                    Case "Estimated Height"
                        value = su.diameter
                    Case "Estimated Diameter"
                        value = su.diameter
                End Select

                If prop.Contains("Stage_Pressure") Then value = su.pressure
                If prop.Contains("Stage_Temperature") Then value = su.temperature
                If prop.Contains("Stage_Efficiency") Then value = ""
                If prop.Contains("Molar Flow") Then value = su.molarflow

                Return value

            End If

        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter

            If prop.Contains("PROP_DC") Then
                Dim propidx As Integer = -1
                Integer.TryParse(prop.Split("_")(2), propidx)
                Select Case propidx
                    Case 0
                        'PROP_DC_0	Condenser Pressure
                        Stages.First.P = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                    Case 1
                        'PROP_DC_1	Reboiler Pressure
                        Stages.Last.P = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
                    Case 2
                        'PROP_DC_2	Condenser Pressure Drop
                        CondenserDeltaP = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                    Case 7
                        SetNumberOfStages(propval)
                    Case 8
                        ColumnPressureDrop = SystemsOfUnits.Converter.ConvertToSI(su.deltaP, propval)
                End Select
            End If

            Select Case prop
                Case "Condenser_Specification_Value"
                    Me.Specs("C").SpecValue = propval
                Case "Reboiler_Specification_Value"
                    Me.Specs("R").SpecValue = propval
            End Select

            If prop.Contains("Stage_Pressure_") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).P = SystemsOfUnits.Converter.ConvertToSI(su.pressure, propval)
            End If

            If prop.Contains("Stage_Temperature_") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).T = SystemsOfUnits.Converter.ConvertToSI(su.temperature, propval)
            End If

            If prop.Contains("Stage_Efficiency_") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).Efficiency = propval
            End If

            If prop = "Global_Stage_Efficiency" Then
                For Each st As Stage In Me.Stages
                    st.Efficiency = propval
                Next
            End If

            If prop.Contains("Stage Index") Then
                For Each si In MaterialStreams.Values
                    Try
                        Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                        If prop = String.Format("Stream '{0}' Stage Index", streamtag) Then
                            si.AssociatedStage = Stages(Convert.ToInt32(propval)).Name
                            Exit For
                        End If
                    Catch ex As Exception
                    End Try
                Next
            End If

            If prop.Contains("Side Draw Molar Flow") Then
                For Each si In MaterialStreams.Values
                    Try
                        Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                        If prop = String.Format("Stream '{0}' Side Draw Molar Flow", streamtag) Then
                            si.FlowRate.Value = Convert.ToDouble(propval).ConvertToSI(su.molarflow)
                            Exit For
                        End If
                    Catch ex As Exception
                    End Try
                Next
            End If

            Return 1

        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.col_dc_32.png")

        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("CDEST_Desc")
        End Function

        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("CDEST_Name")
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            str.AppendLine("Distillation Column: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Condenser type: " & Me.CondenserType.ToString)
            str.AppendLine("    Number of Stages: " & Me.Stages.Count)
            str.AppendLine()
            str.AppendLine("Results")
            str.AppendLine()
            str.AppendLine("    Condenser heat duty: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.CondenserDuty).ToString(numberformat, ci) & " " & su.heatflow)
            str.AppendLine("    Reboiler heat duty: " & SystemsOfUnits.Converter.ConvertFromSI(su.heatflow, Me.ReboilerDuty).ToString(numberformat, ci) & " " & su.heatflow)
            str.AppendLine()
            str.AppendLine("Column Profiles")
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Temperature (" & su.temperature & ")").PadRight(20))
            For i As Integer = 0 To Tf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Tf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Pressure (" & su.pressure & ")").PadRight(20))
            For i As Integer = 0 To P0.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, P0(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Vapor Flow (" & su.molarflow & ")").PadRight(20))
            For i As Integer = 0 To Vf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Vf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Liquid Flow (" & su.molarflow & ")").PadRight(20))
            For i As Integer = 0 To Lf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Lf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(ColumnPropertiesProfile)
            If CreateSolverConvergengeReport Then
                str.AppendLine()
                str.AppendLine(ColumnSolverConvergenceReport)
            End If

            Return str.ToString

        End Function

    End Class

    <Serializable()> Public Class AbsorptionColumn

        Inherits Column


        Public _opmode As OpMode = OpMode.Absorber


        Public Sub ConnectFeed(feed As ISimulationObject, stagenumber As Integer)

            Dim i As Integer = 0
            Dim success As Boolean = False
            For Each con In GraphicObject.InputConnectors
                If Not con.IsAttached Then
                    FlowSheet.ConnectObjects(feed.GraphicObject, GraphicObject, 0, i)
                    Dim msi As New StreamInformation With {.ID = feed.Name, .StreamID = feed.Name,
                        .AssociatedStage = Stages(stagenumber).Name,
                        .StreamBehavior = StreamInformation.Behavior.Feed,
                        .StreamType = StreamInformation.Type.Material}
                    MaterialStreams.Add(msi.ID, msi)
                    success = True
                    Exit For
                End If
                i += 1
            Next
            If Not success Then Throw New Exception("No feed port available")

        End Sub

        ''' <summary>
        ''' Connects the top product. This column's top product leaves stage 0 as the
        ''' overhead stream V(0), so it is marked OverheadVapor - the same behaviour
        ''' the UI stores. Distillate means a liquid drawn off stage 0 at rate LSS(0),
        ''' which for an absorber is zero, and the product's flow would then be left
        ''' out of the closing mass balance entirely.
        ''' </summary>
        Public Sub ConnectTopProduct(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 0, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages(0).Name,
                        .StreamBehavior = StreamInformation.Behavior.OverheadVapor,
                        .StreamType = StreamInformation.Type.Material}
            MaterialStreams.Add(msi.ID, msi)

        End Sub

        Public Sub ConnectBottoms(stream As ISimulationObject)

            FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, 1, 0)
            Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages.Last.Name,
                        .StreamBehavior = StreamInformation.Behavior.BottomsLiquid,
                        .StreamType = StreamInformation.Type.Material}
            MaterialStreams.Add(msi.ID, msi)

        End Sub

        Public Sub New()
            MyBase.New()
        End Sub

        Public Enum OpMode
            Absorber = 0
            Extractor = 1
        End Enum

        Public Property OperationMode() As OpMode
            Get
                Return _opmode
            End Get
            Set(ByVal value As OpMode)
                _opmode = value
            End Set
        End Property

        Public Sub New(ByVal name As String, ByVal description As String, fs As IFlowsheet)
            MyBase.New(name, description, fs)
            Me.ColumnType = ColType.AbsorptionColumn
            MyBase.AddStages()
            For k2 = 0 To Me.Stages.Count - 1
                Me.Stages(k2).P = 101325
            Next
        End Sub

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New AbsorptionColumn()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of AbsorptionColumn)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.RO
                    For i = 2 To 2
                        proplist.Add("PROP_AC_" + CStr(i))
                    Next
                    proplist.Add("Estimated Height")
                    proplist.Add("Estimated Diameter")
                Case PropertyType.RW
                    For i = 0 To 2
                        proplist.Add("PROP_AC_" + CStr(i))
                    Next
                Case PropertyType.WR
                    For i = 0 To 1
                        proplist.Add("PROP_AC_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Efficiency_" + CStr(i))
                        proplist.Add("Stage_DowncomerLength_" + CStr(i))
                        proplist.Add("Stage_DowncomerHeight_" + CStr(i))
                        proplist.Add("Stage_TotalHoleArea_" + CStr(i))
                        proplist.Add("Stage_LiquidLevel_" + CStr(i))
                        proplist.Add("Stage_Height_" + CStr(i))
                    Next
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            proplist.Add(String.Format("Stream '{0}' Stage Index", streamtag))
                        Catch ex As Exception
                        End Try
                    Next
                    For Each si In MaterialStreams.Values
                        If si.StreamBehavior = StreamInformation.Behavior.Sidedraw Then
                            Try
                                Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                                proplist.Add(String.Format("Stream '{0}' Side Draw Molar Flow", streamtag))
                            Catch ex As Exception
                            End Try
                        End If
                    Next
                    proplist.Add("Estimated Height")
                    proplist.Add("Estimated Diameter")
                    proplist.Add("Number of Stages")
                Case PropertyType.ALL
                    For i = 0 To 2
                        proplist.Add("PROP_AC_" + CStr(i))
                    Next
                    For i = 1 To Me.Stages.Count
                        proplist.Add("Stage_Efficiency_" + CStr(i))
                        proplist.Add("Stage_DowncomerLength_" + CStr(i))
                        proplist.Add("Stage_DowncomerHeight_" + CStr(i))
                        proplist.Add("Stage_TotalHoleArea_" + CStr(i))
                        proplist.Add("Stage_LiquidLevel_" + CStr(i))
                        proplist.Add("Stage_Height_" + CStr(i))
                    Next
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            proplist.Add(String.Format("Stream '{0}' Stage Index", streamtag))
                        Catch ex As Exception
                        End Try
                    Next
                    For Each si In MaterialStreams.Values
                        If si.StreamBehavior = StreamInformation.Behavior.Sidedraw Then
                            Try
                                Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                                proplist.Add(String.Format("Stream '{0}' Side Draw Molar Flow", streamtag))
                            Catch ex As Exception
                            End Try
                        End If
                    Next
                    proplist.Add("Estimated Height")
                    proplist.Add("Estimated Diameter")
                    proplist.Add("Number of Stages")
            End Select
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then

                Return val0

            Else

                If su Is Nothing Then su = New SystemsOfUnits.SI
                Dim cv As New SystemsOfUnits.Converter
                Dim value As Double = 0

                Dim propidx As Integer = -1

                Try
                    Integer.TryParse(prop.Split("_")(2), propidx)
                Catch ex As Exception

                End Try

                Select Case propidx

                    Case 0
                        'PROP_DC_0	Condenser Pressure
                        Try
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.Stages.First.P)
                        Catch ex As Exception
                            value = 0.0
                        End Try
                    Case 1
                        'PROP_DC_1	Reboiler Pressure
                        Try
                            value = SystemsOfUnits.Converter.ConvertFromSI(su.pressure, Me.Stages.Last.P)
                        Catch ex As Exception
                            value = 0.0
                        End Try
                    Case 2
                        'PROP_DC_7	Number of Stages
                        value = Me.NumberOfStages
                End Select

                Select Case prop
                    Case "Estimated Height"
                        value = EstimatedHeight.ConvertFromSI(su.diameter)
                    Case "Estimated Diameter"
                        value = EstimatedDiameter.ConvertFromSI(su.diameter)
                    Case "Number of Stages"
                        value = NumberOfStages
                End Select

                If prop.Contains("Stage_Efficiency_") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).Efficiency
                End If

                If prop.Contains("Stage_DowncomerLength") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).DowncomerLength.ConvertFromSI(su.distance)
                End If

                If prop.Contains("Stage_DowncomerHeight") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).DowncomerHeight.ConvertFromSI(su.distance)
                End If

                If prop.Contains("Stage_TotalHoleArea") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).TotalHoleArea.ConvertFromSI(su.area)
                End If

                If prop.Contains("Stage_LiquidLevel") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).LiquidLevel.ConvertFromSI(su.distance)
                End If

                If prop.Contains("Stage_Height") Then
                    Dim stageindex As Integer = prop.Split("_")(2)
                    If Me.Stages.Count >= stageindex Then value = Me.Stages(stageindex - 1).StageHeight.ConvertFromSI(su.distance)
                End If

                If prop.Contains("Global_Stage_Efficiency") Then value = "N/D"

                If prop.Contains("Stage Index") Then
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            If prop = String.Format("Stream '{0}' Stage Index", streamtag) Then
                                If si.StreamBehavior = StreamInformation.Behavior.BottomsLiquid Then
                                    value = Stages.Count - 1
                                ElseIf si.StreamBehavior = StreamInformation.Behavior.Distillate Then
                                    value = 0
                                ElseIf si.StreamBehavior = StreamInformation.Behavior.OverheadVapor Then
                                    value = 0
                                Else
                                    value = StageIndex(si.AssociatedStage)
                                End If
                                Return value
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                End If

                If prop.Contains("Side Draw Molar Flow") Then
                    For Each si In MaterialStreams.Values
                        Try
                            Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                            If prop = String.Format("Stream '{0}' Side Draw Molar Flow", streamtag) Then
                                value = si.FlowRate.Value.ConvertFromSI(su.molarflow)
                                Return value
                            End If
                        Catch ex As Exception
                        End Try
                    Next
                End If

                Return value

            End If

        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            If su Is Nothing Then su = New SystemsOfUnits.SI
            Dim cv As New SystemsOfUnits.Converter
            Dim value As String = ""

            If prop.Contains("Stage Index") Then Return ""

            Dim propidx As Integer = -1

            Try
                Integer.TryParse(prop.Split("_")(2), propidx)
            Catch ex As Exception

            End Try

            Select Case propidx

                Case 0
                    'PROP_DC_0	Condenser Pressure
                    value = su.pressure
                Case 1
                    'PROP_DC_1	Reboiler Pressure
                    value = su.pressure
                Case 2
                    'PROP_DC_7	Number of Stages
                    value = ""
            End Select

            Select Case prop
                Case "Estimated Height"
                    value = su.diameter
                Case "Estimated Diameter"
                    value = su.diameter
            End Select

            If prop.Contains("Stage_Efficiency") Then value = ""
            If prop.Contains("Stage_DowncomerLength") Then value = su.distance
            If prop.Contains("Stage_DowncomerHeight") Then value = su.distance
            If prop.Contains("Stage_TotalHoleArea") Then value = su.area
            If prop.Contains("Stage_LiquidLevel") Then value = su.distance
            If prop.Contains("Stage_Height") Then value = su.distance
            If prop.Contains("Molar Flow") Then value = su.molarflow

            Return value

        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Dim propidx As Integer = -1

            Try
                Integer.TryParse(prop.Split("_")(2), propidx)
            Catch ex As Exception

            End Try

            Select Case propidx
                Case 2
                    SetNumberOfStages(propval)
            End Select

            If prop.Contains("Stage_Efficiency_") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).Efficiency = propval
            End If

            If prop.Contains("Stage_DowncomerLength") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).DowncomerLength = Convert.ToDouble(propval).ConvertToSI(su.distance)
            End If

            If prop.Contains("Stage_DowncomerHeight") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).DowncomerHeight = Convert.ToDouble(propval).ConvertToSI(su.distance)
            End If

            If prop.Contains("Stage_TotalHoleArea") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).TotalHoleArea = Convert.ToDouble(propval).ConvertToSI(su.area)
            End If

            If prop.Contains("Stage_LiquidLevel") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).LiquidLevel = Convert.ToDouble(propval).ConvertToSI(su.distance)
            End If

            If prop.Contains("Stage_Height") Then
                Dim stageindex As Integer = prop.Split("_")(2)
                If Me.Stages.Count >= stageindex Then Me.Stages(stageindex - 1).StageHeight = Convert.ToDouble(propval).ConvertToSI(su.distance)
            End If

            If prop = "Global_Stage_Efficiency" Then
                For Each st As Stage In Me.Stages
                    st.Efficiency = propval
                Next
            ElseIf prop = "Number of Stages" Then
                SetNumberOfStages(propval)
            End If

            If prop.Contains("Stage Index") Then
                For Each si In MaterialStreams.Values
                    Try
                        Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                        If prop = String.Format("Stream '{0}' Stage Index", streamtag) Then
                            si.AssociatedStage = Stages(Convert.ToInt32(propval)).Name
                            Exit For
                        End If
                    Catch ex As Exception
                    End Try
                Next
            End If

            If prop.Contains("Side Draw Molar Flow") Then
                For Each si In MaterialStreams.Values
                    Try
                        Dim streamtag = FlowSheet.SimulationObjects(si.StreamID).GraphicObject.Tag
                        If prop = String.Format("Stream '{0}' Side Draw Molar Flow", streamtag) Then
                            si.FlowRate.Value = Convert.ToDouble(propval).ConvertToSI(su.molarflow)
                            Exit For
                        End If
                    Catch ex As Exception
                    End Try
                Next
            End If


            Return 1

        End Function

        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.col_abs_32.png")

        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("CABS_Desc")
        End Function

        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("CABS_Name")
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function GetReport(su As IUnitsOfMeasure, ci As Globalization.CultureInfo, numberformat As String) As String

            Dim str As New Text.StringBuilder

            str.AppendLine("Absorption Column: " & Me.GraphicObject.Tag)
            str.AppendLine("Property Package: " & Me.PropertyPackage.ComponentName)
            str.AppendLine()
            str.AppendLine("Calculation parameters")
            str.AppendLine()
            str.AppendLine("    Number of Stages: " & Me.Stages.Count)
            str.AppendLine()
            str.AppendLine("Column Profiles")
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Temperature (" & su.temperature & ")").PadRight(20))
            For i As Integer = 0 To Tf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.temperature, Tf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Pressure (" & su.pressure & ")").PadRight(20))
            For i As Integer = 0 To P0.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.pressure, P0(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Vapor Flow (" & su.molarflow & ")").PadRight(20))
            For i As Integer = 0 To Vf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Vf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(("Stage").PadRight(20) & ("Liquid Flow (" & su.molarflow & ")").PadRight(20))
            For i As Integer = 0 To Lf.Count - 1
                str.AppendLine(i.ToString.PadRight(20) & SystemsOfUnits.Converter.ConvertFromSI(su.molarflow, Lf(i)).ToString(numberformat, ci).PadRight(20))
            Next
            str.AppendLine()
            str.AppendLine(ColumnPropertiesProfile)
            If CreateSolverConvergengeReport Then
                str.AppendLine()
                str.AppendLine(ColumnSolverConvergenceReport)
            End If

            Return str.ToString

        End Function


    End Class

    <System.Serializable()> Public MustInherit Partial Class Column

        Inherits UnitOperations.UnitOpBaseClass

        '--- Shared dynamic-simulation implementation (used by all column types) ---

        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = True

        Public Property BottomsAccumulationStream As MaterialStream
        Public Overrides Sub CreateDynamicProperties()

            AddDynamicProperty("Max. P change (%)", "Maximum Pressure change (in percent) change between iterations", 10, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Max. L change (%)", "Maximum Liquid Flow change (in percent) change between iterations", 10, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Max. V change (%)", "Maximum Vapor Flow change (in percent) change between iterations", 10, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Time step discretization", "Number of sub-steps per integration step for column dynamics.", 1, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Souders-Brown Coefficient", "Souders-Brown coefficient C_SB (m/s) for flooding check. Typical 0.03-0.05 for sieve trays. Set to 0 to disable.", 0.0, UnitOfMeasure.none, 1.0.GetType())
            AddDynamicProperty("Flooding Alarm", "True when any stage exceeds the flooding velocity limit.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Weeping Alarm", "True when any stage vapor velocity is below minimum for tray support.", False, UnitOfMeasure.none, True.GetType())
            AddDynamicProperty("Apply Murphree Efficiency", "Apply stage Murphree efficiency to dynamic simulation. Stage efficiency values are used.", False, UnitOfMeasure.none, True.GetType())

        End Sub
        Private Sub InitializeDynamicsFromSteadyStateSolution()

            CalculateDowncomerAreas()

            Dim sol = GetLastSolution()

            For i As Integer = 0 To Stages.Count - 1

                Dim s = Stages(i)

                Stages(i).Vout.Value = sol.VapMolarFlows(i).Value
                Stages(i).Lout.Value = sol.LiqMolarFlows(i).Value

                If i < Stages.Count - 1 Then Stages(i).Vin.Value = sol.VapMolarFlows(i + 1).Value
                If i > 0 Then Stages(i).Lin.Value = sol.LiqMolarFlows(i - 1).Value

                s.AccumulationStream = New MaterialStream()

                Dim Lx = sol.LiqCompositions(i).Values.Select(Function(v) v.Value).ToArray()
                Dim Vx = sol.VapCompositions(i).Values.Select(Function(v) v.Value).ToArray()

                Dim Ln = Lx.MultiplyConstY(sol.LiqMolarFlows(i).Value)
                Dim Vn = Vx.MultiplyConstY(sol.VapMolarFlows(i).Value)
                Dim Zn = Ln.AddY(Vn)

                With s.AccumulationStream
                    .SetOverallMolarComposition(Zn.NormalizeY())
                    .SetMolarFlow(Zn.SumY())
                    .SetTemperature(sol.StageTemps(i).Value)
                    .SetPressure(Stages(i).P)
                    .SetFlashSpec("PT")
                    .AssignSelfToPP()
                    .Calculate()
                End With

                Stages(i).LiquidLevel = s.AccumulationStream.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / ((Math.PI * EstimatedDiameter ^ 2 / 4) - Stages(i).DowncomerArea)

            Next

            'Bottom sump: seed once from the bottom (reboiler) stage content.
            BottomsAccumulationStream = DirectCast(Stages.Last.AccumulationStream.CloneXML(), MaterialStream)
            BottomsAccumulationStream.SetFlowsheet(FlowSheet)
            BottomsAccumulationStream.PropertyPackage = PropertyPackage
            BottomsAccumulationStream.AssignSelfToPP()
            BottomsAccumulationStream.Calculate()

            BottomLiquidLevel = BottomsAccumulationStream.OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / (Math.PI * EstimatedDiameter ^ 2 / 4)

        End Sub
        Public Overrides Sub RunDynamicModel()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator

            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds

            Dim timestep_discretization As Double = GetDynamicProperty("Time step discretization")

            Dim maxDP As Double = GetDynamicProperty("Max. P change (%)")
            Dim maxDL As Double = GetDynamicProperty("Max. L change (%)")
            Dim maxDV As Double = GetDynamicProperty("Max. V change (%)")
            Dim C_SB As Double = GetDynamicProperty("Souders-Brown Coefficient")
            Dim applyMurphree As Boolean = GetDynamicProperty("Apply Murphree Efficiency")

            Dim floodingDetected As Boolean = False
            Dim weepingDetected As Boolean = False

            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0

            Dim _Streams As New List(Of MaterialStream)
            Dim _Feeds, _SideDraws As New List(Of StreamInformation)
            Dim _HeatStreams As New List(Of StreamInformation)

            Dim _StageIDs As New List(Of String)

            Dim _BottomsProduct As StreamInformation = Nothing

            Dim _TopProduct As StreamInformation = Nothing

            'Liquid distillate (distillation condenser). Detected and handled like the overhead
            'vapor product, but assigned from the liquid phase. Absent for absorbers.
            Dim _Distillate As StreamInformation = Nothing

            For Each s In Stages
                If s.AccumulationStream Is Nothing Then
                    Throw New Exception("Column needs to be (re)initialized")
                End If
                _Streams.Add(s.AccumulationStream)
                _StageIDs.Add(s.Name)
            Next

            If BottomsAccumulationStream Is Nothing Then
                Throw New Exception("Column needs to be (re)initialized")
            End If

            _Streams.Add(BottomsAccumulationStream)
            _StageIDs.Add("BottomContents")

            For Each s In MaterialStreams.Values
                If s.StreamType = StreamInformation.Type.Material And
                    s.StreamBehavior = StreamInformation.Behavior.Feed Then
                    _Feeds.Add(s)
                End If
                If s.StreamType = StreamInformation.Type.Material And
                    s.StreamBehavior = StreamInformation.Behavior.Sidedraw Then
                    _SideDraws.Add(s)
                End If
                If s.StreamType = StreamInformation.Type.Energy Then _HeatStreams.Add(s)
                If s.StreamType = StreamInformation.Type.Material And
                    s.StreamBehavior = StreamInformation.Behavior.OverheadVapor Then
                    _TopProduct = s
                End If
                If s.StreamType = StreamInformation.Type.Material And
                    s.StreamBehavior = StreamInformation.Behavior.Distillate Then
                    _Distillate = s
                End If
                If s.StreamType = StreamInformation.Type.Material And
                    s.StreamBehavior = StreamInformation.Behavior.BottomsLiquid Then
                    _BottomsProduct = s
                End If
            Next

            CalculateDowncomerAreas()

            Dim Fv, Fl, rhov, rhol, vv, vl, Fv0, Fl0 As Double

            'Inter-stage transfer streams built during the material balance and applied (conserving)
            'afterwards: vTrans(i) = vapor leaving stage i upward (to i-1); lTrans(i) = liquid leaving
            'stage i downward (to i+1).
            Dim vTrans(_Streams.Count - 1) As MaterialStream
            Dim lTrans(_Streams.Count - 1) As MaterialStream

            'material balance
            For i = 0 To _Streams.Count - 1
                Dim stageid = _StageIDs(i)
                Dim feed = _Feeds.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If feed IsNot Nothing Then
                    Dim feedstream = DirectCast(FlowSheet.SimulationObjects(feed.StreamID), MaterialStream)
                    If Not Double.IsNaN(feedstream.GetMassFlow()) AndAlso feedstream.GetMassFlow() > 0 Then _Streams(i) = _Streams(i).Add(feedstream, timestep)
                End If
                Dim side = _SideDraws.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If side IsNot Nothing Then
                    Dim sidestream = DirectCast(FlowSheet.SimulationObjects(side.StreamID), MaterialStream)
                    If Not Double.IsNaN(sidestream.GetMassFlow()) AndAlso sidestream.GetMassFlow() > 0 Then _Streams(i) = _Streams(i).Subtract(sidestream, timestep)
                End If
                If i = 0 Then
                    If _TopProduct IsNot Nothing Then
                        Dim topstream = DirectCast(FlowSheet.SimulationObjects(_TopProduct.StreamID), MaterialStream)
                        If Not Double.IsNaN(topstream.GetMassFlow()) AndAlso topstream.GetMassFlow() > 0 Then _Streams(i) = _Streams(i).Subtract(topstream, timestep)
                    End If
                    If _Distillate IsNot Nothing Then
                        Dim diststream = DirectCast(FlowSheet.SimulationObjects(_Distillate.StreamID), MaterialStream)
                        If Not Double.IsNaN(diststream.GetMassFlow()) AndAlso diststream.GetMassFlow() > 0 Then _Streams(i) = _Streams(i).Subtract(diststream, timestep)
                    End If
                ElseIf i = _Streams.Count - 1 Then
                    If _BottomsProduct IsNot Nothing Then
                        Dim bottomstream = DirectCast(FlowSheet.SimulationObjects(_BottomsProduct.StreamID), MaterialStream)
                        If Not Double.IsNaN(bottomstream.GetMassFlow()) AndAlso bottomstream.GetMassFlow() > 0 Then _Streams(i) = _Streams(i).Subtract(bottomstream, timestep)
                    End If
                End If
                Dim duty = _HeatStreams.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If duty IsNot Nothing AndAlso duty.IsValidDouble() Then
                    Dim estream = DirectCast(FlowSheet.SimulationObjects(duty.StreamID), EnergyStream)
                    _Streams(i).SetMassEnthalpy(_Streams(i).GetMassEnthalpy() + estream.EnergyFlow.GetValueOrDefault() * timestep / _Streams(i).GetMassFlow())
                End If
                _Streams(i).SetFlowsheet(FlowSheet)
                _Streams(i).PropertyPackage = PropertyPackage
                _Streams(i).AssignSelfToPP()
                _Streams(i).SetFlashSpec("PH")
                _Streams(i).Calculate()
                rhov = _Streams(i).Vapor.Properties.density.GetValueOrDefault()
                rhol = _Streams(i).OverallLiquid.Properties.density.GetValueOrDefault()
                vv = _Streams(i).Vapor.Properties.volumetric_flow.GetValueOrDefault() / _Streams(i).Vapor.Properties.molarflow.GetValueOrDefault()
                vl = _Streams(i).OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / _Streams(i).OverallLiquid.Properties.molarflow.GetValueOrDefault()
                'Fv, Fl = mol/s
                If i = _Streams.Count - 1 Then
                    Fv0 = Stages(stageid).Vout.Value
                    Fv = Stages(i - 1).TotalHoleArea / vv * ((_Streams(i).GetPressure() - _Streams(i - 1).GetPressure()) / (101325 * rhov * Stages(i - 1).DryTrayPressureDropCoefficient)) ^ 0.5
                    If Math.Abs((Fv - Fv0) / Fv0 * 100) > maxDV Then Fv = Fv0 * (1 + maxDV / 100.0 * Math.Sign(Fv - Fv0))
                    If Fv.IsValidDouble() Then
                        Stages(stageid).Vout.Value = Fv
                        Stages(stageid - 1).Vin.Value = Fv
                        If rhov > 0 AndAlso vv > 0 AndAlso Fv > 0 Then
                            Dim vt = DirectCast(_Streams(i).CloneXML(), MaterialStream)
                            vt.AssignFromPhase(PhaseLabel.Vapor, _Streams(i), True)
                            vt.SetMassFlow(Fv * vv * rhov)
                            vt.PropertyPackage = PropertyPackage
                            vt.SetFlowsheet(FlowSheet)
                            vTrans(i) = vt
                        End If
                    End If
                ElseIf i = 0 Then
                    Fl0 = Stages(stageid).Lout.Value
                    Fl = Stages(i).LiquidFlowEquationCoefficient_Alpha * Stages(i).DowncomerLength / vl * ((Stages(i).LiquidLevel - Stages(i).LiquidFlowEquationCoefficient_Beta * Stages(i).DowncomerHeight) / Stages(i).LiquidFlowEquationCoefficient_Beta) ^ 1.5
                    If Math.Abs((Fl - Fl0) / Fl0 * 100) > maxDL Then Fl = Fl0 * (1 + maxDL / 100.0 * Math.Sign(Fl - Fl0))
                    If Fl.IsValidDouble() Then
                        Stages(stageid).Lout.Value = Fl
                        Stages(stageid + 1).Lin.Value = Fl
                        If rhol > 0 AndAlso vl > 0 AndAlso Fl > 0 Then
                            Dim lt = DirectCast(_Streams(i).CloneXML(), MaterialStream)
                            lt.AssignFromPhase(PhaseLabel.Liquid1, _Streams(i), True)
                            lt.SetMassFlow(Fl * vl * rhol)
                            lt.PropertyPackage = PropertyPackage
                            lt.SetFlowsheet(FlowSheet)
                            lTrans(i) = lt
                        End If
                    End If
                Else
                    Fv0 = Stages(stageid).Vout.Value
                    Fv = Stages(i).TotalHoleArea / vv * ((_Streams(i + 1).GetPressure() - _Streams(i).GetPressure()) / (101325 * rhov * Stages(i).DryTrayPressureDropCoefficient)) ^ 0.5
                    If Math.Abs((Fv - Fv0) / Fv0 * 100) > maxDV Then Fv = Fv0 * (1 + maxDV / 100.0 * Math.Sign(Fv - Fv0))
                    If Fv.IsValidDouble() Then
                        Stages(stageid).Vout.Value = Fv
                        If stageid > 0 Then Stages(stageid - 1).Vin.Value = Fv
                        If rhov > 0 AndAlso vv > 0 AndAlso Fv > 0 Then
                            Dim vt = DirectCast(_Streams(i).CloneXML(), MaterialStream)
                            vt.AssignFromPhase(PhaseLabel.Vapor, _Streams(i), True)
                            vt.SetMassFlow(Fv * vv * rhov)
                            vt.PropertyPackage = PropertyPackage
                            vt.SetFlowsheet(FlowSheet)
                            vTrans(i) = vt
                        End If
                    End If
                    Fl0 = Stages(stageid).Lout.Value
                    Fl = Stages(i).LiquidFlowEquationCoefficient_Alpha * Stages(i).DowncomerLength / vl * ((Stages(i).LiquidLevel - Stages(i).LiquidFlowEquationCoefficient_Beta * Stages(i).DowncomerHeight) / Stages(i).LiquidFlowEquationCoefficient_Beta) ^ 1.5
                    If Math.Abs((Fl - Fl0) / Fl0 * 100) > maxDL Then Fl = Fl0 * (1 + maxDL / 100.0 * Math.Sign(Fl - Fl0))
                    If Fl.IsValidDouble() Then
                        Stages(stageid).Lout.Value = Fl
                        If stageid < _Streams.Count - 1 Then Stages(stageid + 1).Lin.Value = Fl
                        If rhol > 0 AndAlso vl > 0 AndAlso Fl > 0 Then
                            Dim lt = DirectCast(_Streams(i).CloneXML(), MaterialStream)
                            lt.AssignFromPhase(PhaseLabel.Liquid1, _Streams(i), True)
                            lt.SetMassFlow(Fl * vl * rhol)
                            lt.PropertyPackage = PropertyPackage
                            lt.SetFlowsheet(FlowSheet)
                            lTrans(i) = lt
                        End If
                    End If
                End If

                If C_SB > 0 AndAlso i > 0 AndAlso i < _Streams.Count - 1 Then
                    Dim activeArea = (Math.PI * EstimatedDiameter ^ 2 / 4) - Stages(i).DowncomerArea
                    If activeArea > 0 AndAlso rhov > 0 AndAlso rhol > 0 Then
                        Dim vVapor = _Streams(i).Vapor.Properties.volumetric_flow.GetValueOrDefault() / activeArea
                        Dim vFlood = C_SB * Math.Sqrt((rhol - rhov) / rhov)
                        If vVapor > vFlood Then floodingDetected = True
                        Dim vWeep = 0.1 * vFlood
                        If vVapor < vWeep AndAlso vVapor > 0 Then weepingDetected = True
                    End If
                End If

                'Murphree efficiency is applied as a composition blend on the vapor transfer
                'streams in a dedicated pass below (it needs the incoming vapor, vTrans(i+1)).
            Next

            If C_SB > 0 Then
                SetDynamicProperty("Flooding Alarm", floodingDetected)
                SetDynamicProperty("Weeping Alarm", weepingDetected)
            End If

            'Murphree vapor efficiency: the vapor actually leaving stage i is a blend of the
            'equilibrium vapor (vTrans(i), composition y_eq) and the vapor entering from below
            '(vTrans(i+1), composition y_in): y_out = E*y_eq + (1-E)*y_in. Blending E moles of one
            'with (1-E) moles of the other via Add() reproduces that composition; the transfer mass
            'is then restored so inter-stage mass conservation is preserved.
            If applyMurphree Then
                For i = 1 To _Streams.Count - 2
                    If vTrans(i) IsNot Nothing AndAlso vTrans(i + 1) IsNot Nothing Then
                        Dim eff = Stages(i).Efficiency
                        If eff < 1.0 Then
                            Dim transferMass = vTrans(i).GetMassFlow()
                            Dim Meq = vTrans(i).GetMolarFlow()
                            Dim Min = vTrans(i + 1).GetMolarFlow()
                            If transferMass > 0 AndAlso Meq > 0 AndAlso Min > 0 Then
                                Dim blended As MaterialStream
                                If eff <= 0.0 Then
                                    'Fully inefficient: leaving vapor has the incoming-vapor composition.
                                    blended = DirectCast(vTrans(i + 1).CloneXML(), MaterialStream)
                                Else
                                    'Add (1-E)/E * Meq/Min moles of incoming vapor to E-weighted equilibrium
                                    'vapor so the resulting composition is E*y_eq + (1-E)*y_in.
                                    Dim factor = (1.0 - eff) / eff * Meq / Min
                                    Dim yeqStream = DirectCast(vTrans(i).CloneXML(), MaterialStream)
                                    Dim yinStream = DirectCast(vTrans(i + 1).CloneXML(), MaterialStream)
                                    blended = yeqStream.Add(yinStream, factor)
                                End If
                                blended.SetMassFlow(transferMass) 'preserve the inter-stage transfer mass
                                blended.PropertyPackage = PropertyPackage
                                blended.SetFlowsheet(FlowSheet)
                                vTrans(i) = blended
                            End If
                        End If
                    End If
                Next
            End If

            'Apply inter-stage mass transfers (conserving): vapor flows from stage i up to i-1 and
            'liquid flows from stage i down to i+1. The same mass (carrying the donor's phase
            'composition) is subtracted from the donor and added to the receiver, clamped to the
            'donor inventory so a holdup cannot go negative in one explicit step.
            For i = 0 To _Streams.Count - 1
                If vTrans(i) IsNot Nothing AndAlso i >= 1 Then
                    Dim vDonorMass = _Streams(i).GetMassFlow()
                    Dim vMoveMass = vTrans(i).GetMassFlow() * timestep
                    If vMoveMass > vDonorMass AndAlso vMoveMass > 0 Then vTrans(i).SetMassFlow(vTrans(i).GetMassFlow() * vDonorMass / vMoveMass)
                    _Streams(i) = _Streams(i).Subtract(vTrans(i), timestep)
                    _Streams(i - 1) = _Streams(i - 1).Add(vTrans(i), timestep)
                End If
                If lTrans(i) IsNot Nothing AndAlso i <= _Streams.Count - 2 Then
                    Dim lDonorMass = _Streams(i).GetMassFlow()
                    Dim lMoveMass = lTrans(i).GetMassFlow() * timestep
                    If lMoveMass > lDonorMass AndAlso lMoveMass > 0 Then lTrans(i).SetMassFlow(lTrans(i).GetMassFlow() * lDonorMass / lMoveMass)
                    _Streams(i) = _Streams(i).Subtract(lTrans(i), timestep)
                    _Streams(i + 1) = _Streams(i + 1).Add(lTrans(i), timestep)
                End If
            Next

            'update pressures

            Dim StageVol As Double

            For i = 0 To _Streams.Count - 1

                If i = _Streams.Count - 1 Then
                    StageVol = (Math.PI * EstimatedDiameter ^ 2 / 4) * (Stages(i).StageHeight + TopSpacing)
                ElseIf i = 0 Then
                    StageVol = (Math.PI * EstimatedDiameter ^ 2 / 4) * BottomSpacing
                Else
                    StageVol = (Math.PI * EstimatedDiameter ^ 2 / 4) * Stages(i).StageHeight
                End If

                'calculate new pressures

                Dim M1, P1, H1 As Double

                'current segment pressure

                _Streams(i).AssignSelfToPP()
                _Streams(i).Calculate()

                M1 = StageVol / _Streams(i).GetMolarFlow() 'm3/mol

                _Streams(i).AssignSelfToPP()

                Dim P1i = _Streams(i).GetPressure()

                Dim result = PropertyPackage.CalculateEquilibrium2(
                        FlashCalculationType.VolumeTemperature,
                        M1, _Streams(i).GetTemperature(), _Streams(i).GetPressure())

                P1 = result.CalculatedPressure
                H1 = result.CalculatedEnthalpy

                If Math.Abs((P1 - P1i) / P1i * 100) > maxDP Then P1 = P1i * (1 + maxDP / 100.0 * Math.Sign(P1 - P1i))

                _Streams(i).SetPressure(P1)
                _Streams(i).SetMassEnthalpy(H1)
                _Streams(i).SpecType = StreamSpec.Pressure_and_Enthalpy

                _Streams(i).AssignSelfToPP()
                _Streams(i).Calculate()

                'Liquid level

                If i = _Streams.Count - 1 Then
                    'REVIEW !!!
                    BottomLiquidLevel = _Streams(i).OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / (Math.PI * EstimatedDiameter ^ 2 / 4)
                End If

                Stages(i).LiquidLevel = _Streams(i).OverallLiquid.Properties.volumetric_flow.GetValueOrDefault() / ((Math.PI * EstimatedDiameter ^ 2 / 4) - Stages(i).DowncomerArea)
                Stages(i).P = _Streams(i).GetPressure()

            Next

            'update connected streams

            For i = 0 To _Streams.Count - 1
                Dim stageid = _StageIDs(i)
                Dim feed = _Feeds.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If feed IsNot Nothing Then
                    Dim feedstream = DirectCast(FlowSheet.SimulationObjects(feed.StreamID), MaterialStream)
                    feedstream.SetPressure(_Streams(i).GetPressure())
                    feedstream.SpecType = StreamSpec.Pressure_and_Enthalpy
                    feedstream.AtEquilibrium = False
                End If
                Dim side = _SideDraws.Where(Function(f) f.AssociatedStage = stageid).FirstOrDefault()
                If side IsNot Nothing Then
                    Dim sidestream = DirectCast(FlowSheet.SimulationObjects(side.StreamID), MaterialStream)
                    If side.StreamPhase = StreamInformation.Phase.L Then
                        sidestream.AssignFromPhase(PhaseLabel.Liquid1, _Streams(i), False)
                    ElseIf side.StreamPhase = StreamInformation.Phase.V Then
                        sidestream.AssignFromPhase(PhaseLabel.Vapor, _Streams(i), False)
                    End If
                    sidestream.SetPressure(_Streams(i).GetPressure())
                    sidestream.SpecType = StreamSpec.Pressure_and_Enthalpy
                    sidestream.AtEquilibrium = False
                End If
                If i = 0 Then
                    If _TopProduct IsNot Nothing Then
                        Dim topstream = DirectCast(FlowSheet.SimulationObjects(_TopProduct.StreamID), MaterialStream)
                        topstream.AssignFromPhase(PhaseLabel.Vapor, _Streams(i), False)
                        topstream.SetPressure(_Streams(i).GetPressure())
                        topstream.SpecType = StreamSpec.Pressure_and_Enthalpy
                        topstream.AtEquilibrium = False
                    End If
                    If _Distillate IsNot Nothing Then
                        'Liquid distillate: assign from the condenser holdup liquid phase.
                        Dim diststream = DirectCast(FlowSheet.SimulationObjects(_Distillate.StreamID), MaterialStream)
                        diststream.AssignFromPhase(PhaseLabel.Liquid1, _Streams(i), False)
                        diststream.SetPressure(_Streams(i).GetPressure())
                        diststream.SpecType = StreamSpec.Pressure_and_Enthalpy
                        diststream.AtEquilibrium = False
                    End If
                ElseIf i = _Streams.Count - 1 Then
                    If _BottomsProduct IsNot Nothing Then
                        Dim bottomstream = DirectCast(FlowSheet.SimulationObjects(_BottomsProduct.StreamID), MaterialStream)
                        bottomstream.AssignFromPhase(PhaseLabel.Liquid1, _Streams(i), False)
                        bottomstream.SetPressure(bottomstream.GetPressure() + bottomstream.Liquid1.Properties.density.GetValueOrDefault() * 9.8 * BottomLiquidLevel)
                        bottomstream.SpecType = StreamSpec.Pressure_and_Enthalpy
                        bottomstream.AtEquilibrium = False
                    End If
                End If
            Next

            'Persist the updated holdups back to the stages. Add/Subtract return NEW stream objects,
            'so the per-stage AccumulationStream references must be refreshed or the dynamic state
            '(feeds, products and inter-stage transfers) would be lost on the next step.
            For i = 0 To Stages.Count - 1
                Stages(i).AccumulationStream = _Streams(i)
            Next
            BottomsAccumulationStream = _Streams(_Streams.Count - 1)

        End Sub

        Public Shared ExternalInitialEstimatesProviders As New Dictionary(Of String, IExternalColumnInitialEstimatesProvider)

        Public Shared ExternalColumnSolvers As New Dictionary(Of String, IExternalColumnSolver)

        Public Property InitialEstimatesProvider As String = "Internal (Default)"

        Public Overrides ReadOnly Property EquipmentTypes As List(Of String)
            Get
                Return New List(Of String) From {"", "Tray Column", "Packed Column"}
            End Get
        End Property

        Public Overrides Sub CreateDimensionsList()

            Dimensions = New List(Of IDimension)
            Dimensions.Add(New Dimension With {.Name = DimensionName.Diameter, .IsUserDefined = False})
            Dimensions.Add(New Dimension With {.Name = DimensionName.Height, .IsUserDefined = False})
            Dimensions.Add(New Dimension With {.Name = DimensionName.NumberOfTrays, .IsUserDefined = False})

        End Sub

        Public Overrides Sub UpdateDimensionsList()

            Dimensions(0).Value = EstimatedDiameter
            Dimensions(1).Value = EstimatedHeight
            If TypeOf Me Is AbsorptionColumn Then
                Dimensions(2).Value = Stages.Count
            Else
                If DirectCast(Me, DistillationColumn).RefluxedAbsorber Then
                    Dimensions(2).Value = Stages.Count - 1
                ElseIf DirectCast(Me, DistillationColumn).RefluxedAbsorber Then
                    Dimensions(2).Value = Stages.Count - 1
                Else
                    Dimensions(2).Value = Stages.Count - 2
                End If
            End If

        End Sub

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Columns

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Public Enum ColType
            DistillationColumn = 0
            AbsorptionColumn = 1
            ReboiledAbsorber = 2
            RefluxedAbsorber = 3
        End Enum

        Public Enum SolvingScheme
            Ideal_K_Init = 0
            Ideal_Enthalpy_Init = 1
            Ideal_K_and_Enthalpy_Init = 2
            Direct = 3
        End Enum

        Public Property CreateSolverConvergengeReport As Boolean = False

        Public Property ColumnSolverConvergenceReport As String = ""

        Public Property ColumnPropertiesProfile As String = ""

        Public Property ColumnPressureDrop As Double = Double.NaN

        Public Property TraySpacing As Double = 0.5 'm

        Public Property EstimatedDiameter As Double = Double.NaN 'm

        Public Property EstimatedHeight As Double = Double.NaN 'm

        Public Property BottomSpacing As Double = 0.5 'm

        Public Property BottomLiquidLevel As Double = 0.0 'm

        Public Property TopSpacing As Double = 0.1 'm

        Public Property SolvingMethodName As String = "Wang-Henke (Bubble Point)"

        'column type
        Private _type As ColType = Column.ColType.DistillationColumn

        'stage numbering is up to bottom. 
        'condenser (when applicable) is the 0th stage.
        'reboiler (when applicable) is the nth stage. 

        'stream collections (for the *entire* column, including side operations)

        Private _conn_ms As New System.Collections.Generic.Dictionary(Of String, StreamInformation)
        Private _conn_es As New System.Collections.Generic.Dictionary(Of String, StreamInformation)

        'iteration variables

        Private _maxiterations As Integer = 100
        Private _ilooptolerance As Double = 0.00001
        Private _elooptolerance As Double = 0.00001

        Public Property SolverScheme As SolvingScheme = SolvingScheme.Direct

        'general variables

        Private _nst As Integer = 12
        Private _rr As Double = 5.0#
        Private _conddp, _drate, _vrate, _condd, _rebd As Double
        Private _st As New List(Of Auxiliary.SepOps.Stage)
        Public Property CondenserType As condtype = condtype.Total_Condenser
        Private m_specs As New Collections.Generic.Dictionary(Of String, Auxiliary.SepOps.ColumnSpec)
        Private m_jac As Object
        Private _vrateunit As String = "mol/s"

        'initial estimates

        Private _use_ie As Boolean = False
        Private _use_ie1 As Boolean = False
        Private _use_ie2 As Boolean = False
        Private _use_ie3 As Boolean = False
        Private _ie As New InitialEstimates
        Private _autoupdie As Boolean = False

        'solver

        <Xml.Serialization.XmlIgnore> Property Solver As ColumnSolver

        Public Sub CalculateDowncomerAreas()

            For Each s In Stages
                s.DowncomerArea = EstimatedDiameter ^ 2 * Math.Acos((EstimatedDiameter / 2 - s.DowncomerLength) / (EstimatedDiameter / 2)) -
                    (EstimatedDiameter / 2 - s.DowncomerLength) * (EstimatedDiameter * s.DowncomerLength - s.DowncomerLength ^ 2) ^ 0.5
            Next

        End Sub


        ''' <summary>
        ''' Set the number of stages (n > 3)
        ''' </summary>
        ''' <param name="n"></param>
        Public Sub SetNumberOfStages(n As Integer)

            If n <= 3 Then Throw New Exception("Invalid number of stages")

            NumberOfStages = n

            Dim ne As Integer = NumberOfStages

            Dim nep As Integer = Stages.Count

            Dim dif As Integer = ne - nep

            If dif < 0 Then
                Stages.RemoveRange(nep + dif - 1, -dif)
                With InitialEstimates
                    .LiqCompositions.RemoveRange(nep + dif - 1, -dif)
                    .VapCompositions.RemoveRange(nep + dif - 1, -dif)
                    .LiqMolarFlows.RemoveRange(nep + dif - 1, -dif)
                    .VapMolarFlows.RemoveRange(nep + dif - 1, -dif)
                    .StageTemps.RemoveRange(nep + dif - 1, -dif)
                End With
            ElseIf dif > 0 Then
                Dim i As Integer
                For i = 1 To dif
                    Stages.Insert(Stages.Count - 1, New Stage(Guid.NewGuid().ToString))
                    Stages(Stages.Count - 2).Name = "Stage" & Stages.Count - 2
                    With InitialEstimates
                        Dim d As New Dictionary(Of String, Parameter)
                        For Each cp In FlowSheet.SelectedCompounds.Values
                            d.Add(cp.Name, New Parameter)
                        Next
                        .LiqCompositions.Insert(.LiqCompositions.Count - 1, d)
                        .VapCompositions.Insert(.VapCompositions.Count - 1, d)
                        .LiqMolarFlows.Insert(.LiqMolarFlows.Count - 1, New Parameter)
                        .VapMolarFlows.Insert(.VapMolarFlows.Count - 1, New Parameter)
                        .StageTemps.Insert(.StageTemps.Count - 1, New Parameter)
                    End With
                Next
            End If

        End Sub

        ''' <summary>
        ''' Sets the Stream feed stage.
        ''' </summary>
        ''' <param name="streamName">Material Stream ID ('Name') property.</param>
        ''' <param name="stageIndex">Stage Index (0 = condenser)</param>
        Public Sub SetStreamFeedStage(streamName As String, stageIndex As Integer)

            Dim si = MaterialStreams.Where(Function(s) s.Value.StreamID = streamName).FirstOrDefault()
            si.Value.AssociatedStage = Stages(stageIndex).ID

        End Sub

        ''' <summary>
        ''' Sets the Stream feed stage.
        ''' </summary>
        ''' <param name="streamName">Material Stream ID ('Name') property.</param>
        ''' <param name="stageID">Stage ID (unique ID)</param>
        Public Sub SetStreamFeedStage(streamName As String, stageID As String)

            Dim si = MaterialStreams.Where(Function(s) s.Value.StreamID = streamName).FirstOrDefault()
            si.Value.AssociatedStage = stageID

        End Sub

        ''' <summary>
        ''' Sets the Stream feed stage.
        ''' </summary>
        ''' <param name="stream"></param>
        ''' <param name="stageIndex">Stage Index (0 = condenser)</param>
        Public Sub SetStreamFeedStage(stream As MaterialStream, stageIndex As Integer)

            Dim si = MaterialStreams.Where(Function(s) s.Value.StreamID = stream.Name).FirstOrDefault()
            si.Value.AssociatedStage = Stages(stageIndex).ID

        End Sub

        ''' <summary>
        ''' Sets the Stream feed stage.
        ''' </summary>
        ''' <param name="stream"></param>
        ''' <param name="stageID">Stage ID (unique ID)</param>
        Public Sub SetStreamFeedStage(stream As MaterialStream, stageID As String)

            Dim si = MaterialStreams.Where(Function(s) s.Value.StreamID = stream.Name).FirstOrDefault()
            si.Value.AssociatedStage = stageID

        End Sub

        ''' <summary>
        ''' Gets the Stream feed stage.
        ''' </summary>
        ''' <param name="stream"></param>
        Public Function GetStreamFeedStageIndex(stream As MaterialStream) As Integer

            Dim si = MaterialStreams.Where(Function(s) s.Value.StreamID = stream.Name).FirstOrDefault()

            Dim stage = Stages.Where(Function(s) s.ID = si.Value.AssociatedStage).FirstOrDefault()

            Return Stages.IndexOf(stage)

        End Function

        Public Sub SetTopPressure(p_Pa As Double)

            Stages.First.P = p_Pa

        End Sub

        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            MyBase.LoadData(data)

            If Not Stages Is Nothing Then Stages.Clear()
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "Stages").SingleOrDefault.Elements.ToList
                Dim id As String = xel.@ID
                If id = "" Then id = Guid.NewGuid().ToString
                Dim var As New Stage(id)
                var.LoadData(xel.Elements.ToList)
                Stages.Add(var)
            Next

            If _conn_ms.Count = 0 Then
                For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "MaterialStreams").SingleOrDefault.Elements.ToList
                    Dim var As New StreamInformation
                    var.LoadData(xel.Elements.ToList)
                    _conn_ms.Add(xel.@ID, var)
                Next
            End If

            If _conn_es.Count = 0 Then
                For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "EnergyStreams").SingleOrDefault.Elements.ToList
                    Dim var As New StreamInformation
                    var.LoadData(xel.Elements.ToList)
                    _conn_es.Add(xel.@ID, var)
                Next
            End If

            If Not m_specs Is Nothing Then m_specs.Clear()
            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "Specs").SingleOrDefault.Elements.ToList
                Dim var As New ColumnSpec
                var.LoadData(xel.Elements.ToList)
                m_specs.Add(xel.@ID, var)
            Next

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            Dim elm As XElement = (From xel2 As XElement In data Select xel2 Where xel2.Name = "Results").SingleOrDefault

            If Not elm Is Nothing Then

                compids = XMLSerializer.XMLSerializer.StringToArray(elm.Element("compids").Value, ci)

                T0 = elm.Element("T0").Value.ToDoubleArray(ci)
                Tf = elm.Element("Tf").Value.ToDoubleArray(ci)
                V0 = elm.Element("V0").Value.ToDoubleArray(ci)
                Vf = elm.Element("Vf").Value.ToDoubleArray(ci)
                L0 = elm.Element("L0").Value.ToDoubleArray(ci)
                Lf = elm.Element("Lf").Value.ToDoubleArray(ci)
                VSS0 = elm.Element("VSS0").Value.ToDoubleArray(ci)
                VSSf = elm.Element("VSSf").Value.ToDoubleArray(ci)
                LSS0 = elm.Element("LSS0").Value.ToDoubleArray(ci)
                LSSf = elm.Element("LSSf").Value.ToDoubleArray(ci)
                P0 = elm.Element("P0").Value.ToDoubleArray(ci)

                x0 = New ArrayList()
                For Each xel In elm.Element("x0").Elements
                    x0.Add(xel.Value.ToDoubleArray(ci))
                Next
                xf = New ArrayList()
                For Each xel In elm.Element("xf").Elements
                    xf.Add(xel.Value.ToDoubleArray(ci))
                Next
                y0 = New ArrayList()
                For Each xel In elm.Element("y0").Elements
                    y0.Add(xel.Value.ToDoubleArray(ci))
                Next
                yf = New ArrayList()
                For Each xel In elm.Element("yf").Elements
                    yf.Add(xel.Value.ToDoubleArray(ci))
                Next
                K0 = New ArrayList()
                For Each xel In elm.Element("K0").Elements
                    K0.Add(xel.Value.ToDoubleArray(ci))
                Next
                Kf = New ArrayList()
                For Each xel In elm.Element("Kf").Elements
                    Kf.Add(xel.Value.ToDoubleArray(ci))
                Next

            End If
            Return True
        End Function

        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As List(Of System.Xml.Linq.XElement) = MyBase.SaveData
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements
                .Add(New XElement("Stages"))
                For Each st As Stage In Stages
                    .Item(.Count - 1).Add(New XElement("Stage", st.SaveData.ToArray))
                Next
                .Add(New XElement("MaterialStreams"))
                For Each kvp As KeyValuePair(Of String, StreamInformation) In _conn_ms
                    .Item(.Count - 1).Add(New XElement("MaterialStream", New XAttribute("ID", kvp.Key), kvp.Value.SaveData.ToArray))
                Next
                .Add(New XElement("EnergyStreams"))
                For Each kvp As KeyValuePair(Of String, StreamInformation) In _conn_es
                    .Item(.Count - 1).Add(New XElement("EnergyStream", New XAttribute("ID", kvp.Key), kvp.Value.SaveData.ToArray))
                Next
                .Add(New XElement("Specs"))
                For Each kvp As KeyValuePair(Of String, Auxiliary.SepOps.ColumnSpec) In m_specs
                    .Item(.Count - 1).Add(New XElement("Spec", New XAttribute("ID", kvp.Key), kvp.Value.SaveData.ToArray))
                Next

                .Add(New XElement("Results"))

                .Item(.Count - 1).Add(New XElement("compids", XMLSerializer.XMLSerializer.ArrayToString(compids, ci)))

                .Item(.Count - 1).Add(New XElement("T0", T0.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("Tf", Tf.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("V0", V0.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("Vf", Vf.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("L0", L0.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("Lf", Lf.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("VSS0", VSS0.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("VSSf", VSSf.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("LSS0", LSS0.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("LSSf", LSSf.ToArrayString(ci)))
                .Item(.Count - 1).Add(New XElement("P0", P0.ToArrayString(ci)))

                .Item(.Count - 1).Add(New XElement("x0"))
                For Each d As Double() In x0
                    .Item(.Count - 1).Element("x0").Add(New XElement("data", d.ToArrayString(ci)))
                Next
                .Item(.Count - 1).Add(New XElement("xf"))
                For Each d As Double() In xf
                    .Item(.Count - 1).Element("xf").Add(New XElement("data", d.ToArrayString(ci)))
                Next
                .Item(.Count - 1).Add(New XElement("y0"))
                For Each d As Double() In y0
                    .Item(.Count - 1).Element("y0").Add(New XElement("data", d.ToArrayString(ci)))
                Next
                .Item(.Count - 1).Add(New XElement("yf"))
                For Each d As Double() In yf
                    .Item(.Count - 1).Element("yf").Add(New XElement("data", d.ToArrayString(ci)))
                Next
                .Item(.Count - 1).Add(New XElement("K0"))
                For Each d As Double() In K0
                    .Item(.Count - 1).Element("K0").Add(New XElement("data", d.ToArrayString(ci)))
                Next
                .Item(.Count - 1).Add(New XElement("Kf"))
                For Each d As Double() In Kf
                    .Item(.Count - 1).Element("Kf").Add(New XElement("data", d.ToArrayString(ci)))
                Next

            End With

            Return elements

        End Function

        'constructor

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String, fs As IFlowsheet)

            MyBase.CreateNew()

            SetFlowsheet(fs)

            ComponentName = name
            ComponentDescription = description

            _st = New System.Collections.Generic.List(Of Stage)

            _conn_ms = New System.Collections.Generic.Dictionary(Of String, StreamInformation)
            _conn_es = New System.Collections.Generic.Dictionary(Of String, StreamInformation)

            _ie = New InitialEstimates

        End Sub

        Public Function StreamExists(ByVal st As StreamInformation.Behavior)

            For Each si As StreamInformation In Me.MaterialStreams.Values
                If si.StreamBehavior = st Then
                    Return True
                End If
            Next

            Return False

        End Function

        Sub AddStages()

            Dim i As Integer
            For i = 0 To Me.NumberOfStages - 1
                _st.Add(New Stage(Guid.NewGuid().ToString))
                Select Case Me.ColumnType
                    Case ColType.DistillationColumn
                        If i = 0 Then
                            _st(_st.Count - 1).Name = FlowSheet.GetTranslatedString("DCCondenser")
                        ElseIf i = Me.NumberOfStages - 1 Then
                            _st(_st.Count - 1).Name = FlowSheet.GetTranslatedString("DCReboiler")
                        Else
                            _st(_st.Count - 1).Name = "Stage" & _st.Count - 1
                        End If
                    Case ColType.AbsorptionColumn
                        If i = 0 Then
                            _st(_st.Count - 1).Name = "TopStage"
                        ElseIf i = NumberOfStages - 1 Then
                            _st(_st.Count - 1).Name = "BottomStage"
                        Else
                            _st(_st.Count - 1).Name = "Stage" & _st.Count - 1
                        End If
                End Select
            Next

            InitialEstimates = RebuildEstimates()

        End Sub

        Public Function GetLastSolution() As InitialEstimates

            Return LastSolution

        End Function

        Public Sub SetInitialEstimates(ie As InitialEstimates)

            InitialEstimates = New InitialEstimates()
            InitialEstimates.LoadData(ie.SaveData())

        End Sub

        Public Sub ResetInitialEstimates()

            InitialEstimates = New InitialEstimates()

        End Sub

        Public Sub SetInitialTemperatureEstimates(values As Double())

            If values.Count <> NumberOfStages Then Throw New Exception(String.Format("value vector needs to have {0} elements", NumberOfStages))

            UseTemperatureEstimates = True
            InitialEstimates.StageTemps.Clear()
            For Each v In values
                InitialEstimates.StageTemps.Add(New Parameter() With {.Value = v, .ParamType = Parameter.ParameterType.Fixed})
            Next

        End Sub

        Public Sub SetInitialLiquidMolarFlowEstimates(values As Double())

            If values.Count <> NumberOfStages Then Throw New Exception(String.Format("value vector needs to have {0} elements", NumberOfStages))

            UseLiquidFlowEstimates = True
            InitialEstimates.LiqMolarFlows.Clear()
            For Each v In values
                InitialEstimates.LiqMolarFlows.Add(New Parameter() With {.Value = v, .ParamType = Parameter.ParameterType.Fixed})
            Next

        End Sub

        Public Sub SetInitialVaporMolarFlowEstimates(values As Double())

            If values.Count <> NumberOfStages Then Throw New Exception(String.Format("value vector needs to have {0} elements", NumberOfStages))

            UseVaporFlowEstimates = True
            InitialEstimates.VapMolarFlows.Clear()
            For Each v In values
                InitialEstimates.VapMolarFlows.Add(New Parameter() With {.Value = v, .ParamType = Parameter.ParameterType.Fixed})
            Next

        End Sub

        Public Sub SetInitialMolarCompositionEstimates(liqmolarfracs As Double()(), vapmolarfracs As Double()())

            If liqmolarfracs.Count <> NumberOfStages Then Throw New Exception(String.Format("liquid molar fraction value vectors needs to have {0} elements", NumberOfStages))
            If liqmolarfracs(0).Count <> FlowSheet.SelectedCompounds.Count Then Throw New Exception(String.Format("liquid composition vectors needs to have {0} elements", FlowSheet.SelectedCompounds.Count))
            If vapmolarfracs.Count <> NumberOfStages Then Throw New Exception(String.Format("vapor molar fraction value vectors needs to have {0} elements", NumberOfStages))
            If vapmolarfracs(0).Count <> FlowSheet.SelectedCompounds.Count Then Throw New Exception(String.Format("vapor composition vectors needs to have {0} elements", FlowSheet.SelectedCompounds.Count))

            UseCompositionEstimates = True
            For i = 0 To liqmolarfracs.Count - 1
                Dim d As New Dictionary(Of String, Parameter)
                Dim j = 0
                For Each cp As BaseClasses.ConstantProperties In FlowSheet.SelectedCompounds.Values
                    d.Add(cp.Name, New Parameter With {.Value = liqmolarfracs(i)(j)})
                    j += 1
                Next
                InitialEstimates.LiqCompositions.Add(d)
                Dim d2 As New Dictionary(Of String, Parameter)
                j = 0
                For Each cp As BaseClasses.ConstantProperties In FlowSheet.SelectedCompounds.Values
                    d2.Add(cp.Name, New Parameter With {.Value = vapmolarfracs(i)(j)})
                    j += 1
                Next
                InitialEstimates.VapCompositions.Add(d2)
            Next

        End Sub

        Public Function RebuildEstimates() As InitialEstimates

            Dim iest As New InitialEstimates

            Dim i As Integer
            For i = 0 To Me.NumberOfStages - 1
                iest.LiqMolarFlows.Add(New Parameter())
                iest.VapMolarFlows.Add(New Parameter())
                iest.StageTemps.Add(New Parameter())
                Dim d As New Dictionary(Of String, Parameter)
                For Each cp As BaseClasses.ConstantProperties In Me.FlowSheet.SelectedCompounds.Values
                    d.Add(cp.Name, New Parameter)
                Next
                iest.LiqCompositions.Add(d)
                Dim d2 As New Dictionary(Of String, Parameter)
                For Each cp As BaseClasses.ConstantProperties In Me.FlowSheet.SelectedCompounds.Values
                    d2.Add(cp.Name, New Parameter)
                Next
                iest.VapCompositions.Add(d2)
            Next

            Return iest

        End Function

        Public Property ColumnType As ColType
            Get
                Return _type
            End Get
            Set(ByVal value As ColType)
                _type = value
            End Set
        End Property

        Public Enum condtype
            Total_Condenser = 0
            Partial_Condenser = 1
            Full_Reflux = 2
        End Enum

        Public ReadOnly Property MaterialStreams As System.Collections.Generic.Dictionary(Of String, StreamInformation)
            Get
                Return _conn_ms
            End Get
        End Property

        Public ReadOnly Property EnergyStreams As System.Collections.Generic.Dictionary(Of String, StreamInformation)
            Get
                Return _conn_es
            End Get
        End Property

        ''' <summary>
        ''' Brings the stream information dictionaries in line with what is actually attached to
        ''' the graphic object: adds an entry for every newly connected stream, with the behavior
        ''' its connector implies, and drops the entries whose stream is gone.
        ''' </summary>
        Public Sub SyncConnectedStreams()

            Dim istrs = GraphicObject.InputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Feed")).Select(Function(x2) x2.AttachedConnector.AttachedFrom.Name).ToList
            Dim ostrs = GraphicObject.OutputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Side")).Select(Function(x2) x2.AttachedConnector.AttachedTo.Name).ToList
            Dim dist = GraphicObject.OutputConnectors.Where(Function(x) x.IsAttached AndAlso (x.ConnectorName.Contains("Distillate") Or x.ConnectorName.Contains("Top"))).Select(Function(x2) x2.AttachedConnector.AttachedTo.Name).ToList
            Dim ov = GraphicObject.OutputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Overhead")).Select(Function(x2) x2.AttachedConnector.AttachedTo.Name).ToList
            Dim bottoms = GraphicObject.OutputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Bottoms")).Select(Function(x2) x2.AttachedConnector.AttachedTo.Name).ToList
            Dim rduty = GraphicObject.InputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Reboiler")).Select(Function(x2) x2.AttachedConnector.AttachedFrom.Name).ToList
            Dim cduty = GraphicObject.OutputConnectors.Where(Function(x) x.IsAttached AndAlso x.ConnectorName.Contains("Condenser")).Select(Function(x2) x2.AttachedConnector.AttachedTo.Name).ToList

            For Each id In istrs
                If MaterialStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    MaterialStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Material,
                         .StreamBehavior = StreamInformation.Behavior.Feed})
                End If
            Next

            For Each id In ostrs
                If MaterialStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    MaterialStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Material,
                         .StreamBehavior = StreamInformation.Behavior.Sidedraw})
                End If
            Next

            For Each id In ov
                If MaterialStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    MaterialStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Material,
                         .StreamBehavior = StreamInformation.Behavior.OverheadVapor})
                End If
            Next

            For Each id In dist
                If MaterialStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    If TypeOf Me Is DistillationColumn Then
                        MaterialStreams.Add(id, New StreamInformation With
                            {.StreamID = id, .ID = id,
                             .StreamType = StreamInformation.Type.Material,
                             .StreamBehavior = StreamInformation.Behavior.Distillate})
                    ElseIf TypeOf Me Is AbsorptionColumn Then
                        MaterialStreams.Add(id, New StreamInformation With
                            {.StreamID = id, .ID = id,
                             .StreamType = StreamInformation.Type.Material,
                             .StreamBehavior = StreamInformation.Behavior.OverheadVapor})
                    End If
                End If
            Next

            For Each id In bottoms
                If MaterialStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    MaterialStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Material,
                         .StreamBehavior = StreamInformation.Behavior.BottomsLiquid})
                End If
            Next

            Dim remove As New List(Of String)
            For Each si In MaterialStreams
                If Not istrs.Contains(si.Value.StreamID) AndAlso
                   Not ov.Contains(si.Value.StreamID) AndAlso
                   Not ostrs.Contains(si.Value.StreamID) AndAlso
                   Not dist.Contains(si.Value.StreamID) AndAlso
                   Not bottoms.Contains(si.Value.StreamID) Then
                    If si.Value.ID <> "" Then remove.Add(si.Value.ID) Else remove.Add(si.Key)
                End If
                If Not GetFlowsheet().SimulationObjects.ContainsKey(si.Value.StreamID) Then
                    If si.Value.ID <> "" Then remove.Add(si.Value.ID) Else remove.Add(si.Key)
                End If
            Next
            For Each id In remove
                If MaterialStreams.ContainsKey(id) Then MaterialStreams.Remove(id)
            Next

            For Each id In cduty
                If EnergyStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    EnergyStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Energy,
                         .StreamBehavior = StreamInformation.Behavior.Distillate})
                End If
            Next

            For Each id In rduty
                If EnergyStreams.Values.Where(Function(x) x.StreamID = id).Count = 0 Then
                    EnergyStreams.Add(id, New StreamInformation With
                        {.StreamID = id, .ID = id,
                         .StreamType = StreamInformation.Type.Energy,
                         .StreamBehavior = StreamInformation.Behavior.BottomsLiquid})
                End If
            Next

            Dim remove2 As New List(Of String)
            For Each si In EnergyStreams
                If Not rduty.Contains(si.Value.StreamID) AndAlso Not cduty.Contains(si.Value.StreamID) Then
                    If si.Value.ID <> "" Then remove2.Add(si.Value.ID) Else remove2.Add(si.Key)
                End If
                If Not GetFlowsheet().SimulationObjects.ContainsKey(si.Value.StreamID) Then
                    If si.Value.ID <> "" Then remove2.Add(si.Value.ID) Else remove2.Add(si.Key)
                End If
            Next
            For Each id In remove2
                If EnergyStreams.ContainsKey(id) Then EnergyStreams.Remove(id)
            Next

        End Sub

        Public Property MaxIterations As Integer
            Get
                Return _maxiterations
            End Get
            Set(ByVal value As Integer)
                _maxiterations = value
            End Set
        End Property

        Public Property InternalLoopTolerance As Double
            Get
                Return _ilooptolerance
            End Get
            Set(ByVal value As Double)
                _ilooptolerance = value
            End Set
        End Property

        Public Property ExternalLoopTolerance As Double
            Get
                Return _elooptolerance
            End Get
            Set(ByVal value As Double)
                _elooptolerance = value
            End Set
        End Property

        Public Property VaporFlowRateUnit As String
            Get
                If _vrateunit Is Nothing And Not FlowSheet Is Nothing Then
                    _vrateunit = FlowSheet.FlowsheetOptions.SelectedUnitSystem.molarflow
                End If
                Return _vrateunit
            End Get
            Set(ByVal value As String)
                _vrateunit = value
            End Set
        End Property

        Public ReadOnly Property Specs As Collections.Generic.Dictionary(Of String, Auxiliary.SepOps.ColumnSpec)
            Get
                If m_specs Is Nothing Then
                    m_specs = New Collections.Generic.Dictionary(Of String, Auxiliary.SepOps.ColumnSpec)
                End If
                If Not m_specs.ContainsKey("C") Then
                    m_specs.Add("C", New ColumnSpec)
                    With m_specs("C")
                        .SType = ColumnSpec.SpecType.Stream_Ratio
                        .SpecUnit = ""
                        .SpecValue = Me.RefluxRatio
                    End With
                End If
                If Not m_specs.ContainsKey("R") Then
                    m_specs.Add("R", New ColumnSpec)
                    With m_specs("R")
                        .SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate
                        .SpecUnit = "mol/s"
                        .SpecValue = Me.DistillateFlowRate
                        .StageNumber = -1
                    End With
                End If
                Return m_specs
            End Get
        End Property

        Public Property VaporFlowRate As Double
            Get
                Return _vrate
            End Get
            Set(ByVal value As Double)
                _vrate = value
            End Set
        End Property

        Public Property DistillateFlowRate As Double
            Get
                Return _drate
            End Get
            Set(ByVal value As Double)
                _drate = value
            End Set
        End Property

        Public Property CondenserDeltaP As Double
            Get
                Return _conddp
            End Get
            Set(ByVal value As Double)
                _conddp = value
            End Set
        End Property

        Public Property ReboilerDuty As Double
            Get
                Return _rebd
            End Get
            Set(ByVal value As Double)
                _rebd = value
            End Set
        End Property

        Public Property CondenserDuty As Double
            Get
                Return _condd
            End Get
            Set(ByVal value As Double)
                _condd = value
            End Set
        End Property

        Public Property RefluxRatio As Double
            Get
                Return _rr
            End Get
            Set(ByVal value As Double)
                _rr = value
            End Set
        End Property

        Public Property NumberOfStages As Integer
            Get
                Return _nst
            End Get
            Set(ByVal value As Integer)
                _nst = value
            End Set
        End Property

        Public ReadOnly Property Stages As System.Collections.Generic.List(Of Auxiliary.SepOps.Stage)
            Get
                Return _st
            End Get
        End Property

        Public Function StageIndex(ByVal name As String) As Integer
            Dim i As Integer = 0
            For Each st As Stage In Me.Stages
                If st.ID = name Or st.Name = name Then Return i
                i = i + 1
            Next
            Return i
        End Function

        Public Property AutoUpdateInitialEstimates As Boolean
            Get
                Return _autoupdie
            End Get
            Set(ByVal value As Boolean)
                _autoupdie = value
            End Set
        End Property

        Public Property UseTemperatureEstimates As Boolean
            Get
                Return _use_ie
            End Get
            Set(ByVal value As Boolean)
                _use_ie = value
            End Set
        End Property

        Public Property UseVaporFlowEstimates As Boolean
            Get
                Return _use_ie1
            End Get
            Set(ByVal value As Boolean)
                _use_ie1 = value
            End Set
        End Property

        Public Property UseLiquidFlowEstimates As Boolean
            Get
                Return _use_ie3
            End Get
            Set(ByVal value As Boolean)
                _use_ie3 = value
            End Set
        End Property

        Public Property UseCompositionEstimates As Boolean
            Get
                Return _use_ie2
            End Get
            Set(ByVal value As Boolean)
                _use_ie2 = value
            End Set
        End Property

        Public Property InitialEstimates As InitialEstimates
            Get
                Return _ie
            End Get
            Set(ByVal value As InitialEstimates)
                _ie = value
            End Set
        End Property

        Private Property LastSolution As InitialEstimates

        Public Property UseBroydenAcceleration As Boolean = True

        Public Sub CheckConnPos()

            Dim idx As Integer
            For Each strinfo As StreamInformation In Me.MaterialStreams.Values
                Try
                    Select Case strinfo.StreamBehavior
                        Case StreamInformation.Behavior.Feed
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).OutputConnectors(0).AttachedConnector.AttachedToConnectorIndex
                            If Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.InputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.InputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.Distillate
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Not Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + 0.3 * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + 0.3 * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.BottomsLiquid
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Not Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + 0.98 * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + 0.98 * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.OverheadVapor
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Not Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + 0.02 * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + 0.02 * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.Sidedraw
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            End If
                    End Select
                Catch ex As Exception
                    strinfo.StreamID = ""
                End Try
            Next

            For Each strinfo As StreamInformation In Me.EnergyStreams.Values
                Try
                    Select Case strinfo.StreamBehavior
                        Case StreamInformation.Behavior.Distillate
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + 0.08 * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + 0.08 * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.BottomsLiquid
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).OutputConnectors(0).AttachedConnector.AttachedToConnectorIndex
                            If Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + 0.825 * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + 0.825 * Me.GraphicObject.Height)
                            End If
                        Case StreamInformation.Behavior.InterExchanger
                            idx = FlowSheet.GraphicObjects(strinfo.StreamID).InputConnectors(0).AttachedConnector.AttachedFromConnectorIndex
                            If Me.GraphicObject.FlippedH Then
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            Else
                                Me.GraphicObject.OutputConnectors(idx).Position = New Point.Point(Me.GraphicObject.X + Me.GraphicObject.Width, Me.GraphicObject.Y + Me.StageIndex(strinfo.AssociatedStage) / Me.NumberOfStages * Me.GraphicObject.Height)
                            End If
                    End Select
                Catch ex As Exception
                    strinfo.StreamID = ""
                End Try
            Next

            Dim i As Integer = 0
            Dim obj1(Me.GraphicObject.InputConnectors.Count), obj2(Me.GraphicObject.InputConnectors.Count) As Double
            Dim obj3(Me.GraphicObject.OutputConnectors.Count), obj4(Me.GraphicObject.OutputConnectors.Count) As Double
            For Each ic As IConnectionPoint In Me.GraphicObject.InputConnectors
                obj1(i) = -Me.GraphicObject.X + ic.Position.X
                obj2(i) = -Me.GraphicObject.Y + ic.Position.Y
                i = i + 1
            Next
            i = 0
            For Each oc As IConnectionPoint In Me.GraphicObject.OutputConnectors
                obj3(i) = -Me.GraphicObject.X + oc.Position.X
                obj4(i) = -Me.GraphicObject.Y + oc.Position.Y
                i = i + 1
            Next
            Me.GraphicObject.AdditionalInfo = New Object() {obj1, obj2, obj3, obj4}

        End Sub

        Public T0 As Double() = New Double() {}
        Public Tf As Double() = New Double() {}
        Public V0 As Double() = New Double() {}
        Public Vf As Double() = New Double() {}
        Public L0 As Double() = New Double() {}
        Public Lf As Double() = New Double() {}
        Public LSS0 As Double() = New Double() {}
        Public LSSf As Double() = New Double() {}
        Public VSS0 As Double() = New Double() {}
        Public VSSf As Double() = New Double() {}
        Public P0 As Double() = New Double() {}
        Public x0, xf, y0, yf, K0, Kf As New ArrayList
        Public ic, ec As Integer
        Public compids As New ArrayList

        Public Overridable Sub SetColumnSolver(colsolver As ColumnSolver)

            Solver = colsolver

        End Sub

        Public Overridable Function GetSolverInputData(Optional ByVal ignoreuserestimates As Boolean = False) As ColumnSolverInputData

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("For any stage in a countercurrent cascade, assume (1) phase equilibrium is achieved at each stage, (2) no chemical reactions occur, and (3) entrainment of liquid drops in vapor and occlusion of vapor bubbles in liquid are negligible. Figure 1 represents such a stage for the vaporï¿½liquid case, where the stages are numbered down from the top. The same representation applies to liquidï¿½liquid extraction if the higher-density liquid phases are represented by liquid streams and the lower-density liquid phases are represented by vapor streams.")

            IObj?.Paragraphs.Add(InspectorItem.GetImageHTML("image1.jpg"))

            IObj?.Paragraphs.Add("Entering stage j is a single- or two-phase feed of molar flow rate Fj, with overall composition in mole fractions zi,j of component i, temperature TFj , pressure PFj , and corresponding overall molar enthalpy hFj.")

            IObj?.Paragraphs.Add("Also entering stage j is interstage liquid from stage j-1 above, if any, of molar flow rate Lj-1, with composition in mole fractions xij-1, enthalpy hLj-1, temperature Tj-1, and pressure Pj-1, which is equal to or less than the pressure of stage j. Pressure of liquid from stage j-1 is increased adiabatically by hydrostatic head change across head L.")

            IObj?.Paragraphs.Add("Similarly, from stage j+1 below, interstage vapor of molar flow rate V+1, with composition in mole fractions yij+1, enthalpy hV+1, temperature Tj+1, and pressure Pj+1 enters stage j.")

            IObj?.Paragraphs.Add("Leaving stage j is vapor of intensive properties yij, hVj, Tj, and Pj. This stream can be divided into a vapor sidestream of molar flow rate Wj and an interstage stream of molar flow rate Vj to be sent to stage j-1 or, if j=1, to leave as a product. Also leaving stage j is liquid of intensive properties xij, hLj, Tj, and Pj, in equilibrium with vapor (Vj+Wj). This liquid is divided into a sidestream of molar flow rate Uj and an interstage stream of molar flow rate Lj to be sent to stage j+1 or, if j=N, to leave as a product.")

            IObj?.Paragraphs.Add("Associated with each general stage are the following indexed equations expressed in terms of the variable set in Figure 1. However, variables other than those shown in Figure 1 can be used, e.g. component flow rates can replace mole fractions, and sidestream flow rates can be expressed as fractions of interstage flow rates. The equations are referred to as MESH equations, after Wang and Henke.")

            IObj?.Paragraphs.Add("M equationsï¿½Material balance for each component (C equations for each stage).")

            IObj?.Paragraphs.Add("<m>M_{i,j}=L_{j-1}x_{i,j-1}+V_{j+1}y_{i,j+1}+F_jz_{i,j}-(L_j+U_j)x_{i,j}-(V_j+W_j)y_{i,j}</m>")

            IObj?.Paragraphs.Add("E equationsï¿½phase-Equilibrium relation for each component (C equations for each stage),")

            IObj?.Paragraphs.Add("<m>E_{i,j}=y_{i,j}-K_{i,j}x_{i,j}=0</m>")

            IObj?.Paragraphs.Add("where <mi>K_{i,j}</mi> is the phase-equilibrium ratio or K-value.")

            IObj?.Paragraphs.Add("S equationsï¿½mole-fraction Summations (one for each stage),")

            IObj?.Paragraphs.Add("<m>(S_y)_j=\sum\limits_{i=1}^{C}{y_{i,j}}-1=0</m>")

            IObj?.Paragraphs.Add("<m>(S_x)_j=\sum\limits_{i=1}^{C}{x_{i,j}} -1=0</m>")

            IObj?.Paragraphs.Add("H equationï¿½energy balance (one for each stage).")

            IObj?.Paragraphs.Add("<m>H_j=L_{j-1}h_{L_{j-1}}+V_{j+1}h_{V_{j+1}}+F_jh_{F_j}-(L_j+U_j)h_{L_j}-(V_j+W_j)h_{V_j}-Q_j=0</m>")

            IObj?.Paragraphs.Add("A countercurrent cascade of N such stages is represented by N(2C+3) such equations in [N(3C+10)+1] variables. If N and all Fj, zij, TFj, PFj, Pj, Uj, Wj, and Qj are specified, the model is represented by N(2C+3) simultaneous algebraic equations in N(2C+3) unknown (output) variables comprising all xij, yij, Lj, Vj, and Tj, where the M, E, and H equations are nonlinear. If other variables are specified, corresponding substitutions are made to the list of output variables. Regardless, the result is a set containing nonlinear equations that must be solved by an iterative technique.")

            IObj?.Paragraphs.Add("<h2>Initial Estimates</h2>")

            IObj?.Paragraphs.Add("DWSIM will calculate new or use existing initial estimates and forward the values to the selected solver.")

            'Validate unitop status.

            Me.Validate()

            'Check connectors' positions

            Me.CheckConnPos()

            Dim Vn = FlowSheet.SelectedCompounds.Keys.ToList()

            'prepare variables

            Dim llextractor As Boolean = False
            Dim myabs As AbsorptionColumn = TryCast(Me, AbsorptionColumn)
            If myabs IsNot Nothing Then
                If CType(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Absorber Then
                    llextractor = False
                Else
                    llextractor = True
                End If
            End If

            Dim pp As PropertyPackages.PropertyPackage = Me.PropertyPackage

            Dim nc, ns, maxits, i, j As Integer
            Dim firstF As Integer = -1
            Dim lastF As Integer = -1
            nc = Me.FlowSheet.SelectedCompounds.Count
            ns = Me.Stages.Count - 1
            maxits = Me.MaxIterations

            Dim tol(4) As Double
            tol(0) = Me.InternalLoopTolerance
            tol(1) = Me.ExternalLoopTolerance

            Dim F(ns), Q(ns), V(ns), L(ns), VSS(ns), LSS(ns), HF(ns), T(ns), FT(ns), P(ns), fracv(ns), eff(ns),
                distrate, rr, vaprate As Double

            Dim x(ns)() As Double, y(ns)() As Double, z(ns)() As Double, fc(ns)() As Double
            Dim idealK(ns)(), Kval(ns)(), Pvap(ns)() As Double

            For i = 0 To ns
                Array.Resize(x(i), nc)
                Array.Resize(y(i), nc)
                Array.Resize(fc(i), nc)
                Array.Resize(z(i), nc)
                Array.Resize(idealK(i), nc)
                Array.Resize(Kval(i), nc)
                Array.Resize(Pvap(i), nc)
            Next

            If Not Double.IsNaN(ColumnPressureDrop) Then
                For i = 1 To ns
                    Stages(i).P = Stages(0).P + Convert.ToDouble(i) / Convert.ToDouble(ns) * ColumnPressureDrop
                Next
            End If

            i = 0
            For Each st As Stage In Me.Stages
                P(i) = st.P
                i += 1
            Next

            Dim sumcf(nc - 1), sumF, zm(nc - 1), alpha(nc - 1), distVx(nc - 1), rebVx(nc - 1), distVy(nc - 1), rebVy(nc - 1) As Double

            IObj?.Paragraphs.Add("Collecting data from connected streams...")

            i = 0

            Dim stream As MaterialStream = Nothing

            For Each ms As StreamInformation In Me.MaterialStreams.Values
                Select Case ms.StreamBehavior
                    Case StreamInformation.Behavior.Feed
                        stream = FlowSheet.SimulationObjects(ms.StreamID)
                        pp.CurrentMaterialStream = stream
                        F(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.molarflow.GetValueOrDefault
                        HF(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.enthalpy.GetValueOrDefault *
                                                                stream.Phases(0).Properties.molecularWeight.GetValueOrDefault
                        FT(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.temperature.GetValueOrDefault
                        sumF += F(StageIndex(ms.AssociatedStage))
                        j = 0
                        For Each comp As Thermodynamics.BaseClasses.Compound In stream.Phases(0).Compounds.Values
                            fc(StageIndex(ms.AssociatedStage))(j) = comp.MoleFraction.GetValueOrDefault
                            z(StageIndex(ms.AssociatedStage))(j) = comp.MoleFraction.GetValueOrDefault
                            sumcf(j) += comp.MoleFraction.GetValueOrDefault * F(StageIndex(ms.AssociatedStage))
                            j = j + 1
                        Next
                    Case StreamInformation.Behavior.Sidedraw
                        If ms.StreamPhase = StreamInformation.Phase.V Then
                            VSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        Else
                            LSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        End If
                    Case StreamInformation.Behavior.InterExchanger
                        Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End Select
                i += 1
            Next

            For Each ms As StreamInformation In Me.EnergyStreams.Values
                Select Case ms.StreamBehavior
                    Case StreamInformation.Behavior.InterExchanger
                        Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End Select
                i += 1
            Next

            Dim cv As New SystemsOfUnits.Converter

            vaprate = SystemsOfUnits.Converter.ConvertToSI(Me.VaporFlowRateUnit, Me.VaporFlowRate)

            Dim sum1(ns), sum0_ As Double
            sum0_ = 0
            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
                sum0_ += LSS(i) + VSS(i)
            Next

            'firstF/lastF are the topmost and bottom-most fed stages. Both are found
            'by scanning the stages: taking firstF from the order the feeds happen to
            'sit in MaterialStreams makes the initial liquid traffic depend on the
            'order they were connected in, which for an absorber fed liquid at the top
            'and gas at the bottom can start L off at the gas rate.
            For i = 0 To ns
                If F(i) <> 0 Then
                    firstF = i
                    Exit For
                End If
            Next

            For i = ns To 0 Step -1
                If F(i) <> 0 Then
                    lastF = i
                    Exit For
                End If
            Next

            For i = 0 To nc - 1
                zm(i) = sumcf(i) / sumF
            Next

            Dim mwf = pp.AUX_MMM(zm)

            If TypeOf Me Is DistillationColumn Then
                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                    rr = 3.0
                Else
                    If Me.Specs("C").SType = ColumnSpec.SpecType.Stream_Ratio Then
                        rr = Me.Specs("C").SpecValue
                    ElseIf Me.Specs("C").SType = ColumnSpec.SpecType.Component_Fraction Or
                    Me.Specs("C").SType = ColumnSpec.SpecType.Component_Recovery Then
                        rr = 10.0
                    Else
                        rr = 2.5
                    End If
                End If
            End If

            If InitialEstimates.RefluxRatio IsNot Nothing And
                UseVaporFlowEstimates And UseLiquidFlowEstimates Then
                rr = InitialEstimates.RefluxRatio
            End If

            Dim Tref = FT.Where(Function(ti) ti > 0).Average
            Dim Pref = Stages.Select(Function(s) s.P).Average

            Dim fflash As Object() = pp.FlashBase.Flash_PT(zm, Pref, Tref, pp)

            Dim Lflash = fflash(0)
            Dim Vflash = fflash(1)

            Dim Kref = fflash(9)

            Dim Vprops = pp.DW_GetConstantProperties()

            Dim hamount As Double = 0.0

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    rebVx(cindex) = cvalue * zm(cindex) * sumF
                    hamount = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Product_Mass_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Product_Molar_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Feed_Recovery
                    Dim cvalue = Specs("R").SpecValue / 100.0
                    Dim pval = sumF * cvalue
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - pval - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - pval - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - pval - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - pval - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case Else
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF / 2 - sum0_
                        Else
                            distrate = sumF / 2 - sum0_ - vaprate
                        End If
                    End If
            End Select

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate,
                      ColumnSpec.SpecType.Component_Molar_Flow_Rate,
                      ColumnSpec.SpecType.Component_Recovery,
                      ColumnSpec.SpecType.Component_Fraction
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - hamount - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - hamount - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - hamount - sum0_
                            vaprate = 0.0
                        End If
                    End If
            End Select

            If InitialEstimates.VaporProductFlowRate IsNot Nothing And UseVaporFlowEstimates And Not ignoreuserestimates Then
                vaprate = InitialEstimates.VaporProductFlowRate
            End If
            If InitialEstimates.DistillateFlowRate IsNot Nothing And UseLiquidFlowEstimates And Not ignoreuserestimates Then
                distrate = InitialEstimates.DistillateFlowRate
            End If

            If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                distrate = 0.0
            Else
                If Me.CondenserType = condtype.Full_Reflux Then
                    distrate = 0.0
                ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                Else
                    vaprate = 0.0
                End If
            End If

            Dim lamount As Double = 0.0

            Select Case Specs("C").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    lamount = cvalue * zm(cindex) * sumF
                    distVx(cindex) = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
            End Select

            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Distillate Rate: {0} mol/s", distrate))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Vapor Overflow Rate: {0} mol/s", vaprate))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Reflux Ratio: {0}", rr))

            compids = New ArrayList
            For Each compName As String In Vn
                compids.Add(compName)
            Next

            Dim T1, T2 As Double

            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    LSS(0) = distrate
                Case ColType.RefluxedAbsorber
                    LSS(0) = distrate
            End Select

            Select Case Me.ColumnType
                Case ColType.AbsorptionColumn
                    T1 = FT.First
                    T2 = FT.Last
                    If (T1 = 0.0) Then Throw New Exception("The absorber needs a feed stream connected to the first stage.")
                    If (T2 = 0.0) Then Throw New Exception("The absorber needs a feed stream connected to the last stage.")

                    'A condensable vapour fed at the bottom does not hold that stage at
                    'its own inlet temperature: it condenses, and the stage settles at
                    'the boiling point of the liquid running down. Steam fed 45 K above
                    'that otherwise starts the ramp far too hot, and the temperature
                    'update walks away from the answer rather than back to it.
                    '
                    'The anchor is the saturation temperature of the liquid feed's own
                    'solvent, not a bubble point of the mixture. A rigorous bubble point
                    'is useless here: dissolved CO2 and H2S at a few hundred ppm pull the
                    'answer down to 304 K - the critical temperature of CO2, which is
                    'what the calculation actually pins on - for a stream that boils at
                    '373 K. The stripped liquid leaving the bottom is essentially pure
                    'solvent, so the solvent's boiling point is the temperature that
                    'stage really approaches.
                    If firstF >= 0 AndAlso T2 > T1 Then
                        Try
                            Dim names = FlowSheet.SelectedCompounds.Keys.ToList()
                            Dim solvent As Integer = 0
                            For k As Integer = 1 To Math.Min(names.Count, fc(firstF).Length) - 1
                                If fc(firstF)(k) > fc(firstF)(solvent) Then solvent = k
                            Next
                            Dim Tsat = pp.AUX_TSATi(P(ns), names(solvent))
                            If Environment.GetEnvironmentVariable("DWSIM_COLUMN_TRACE") = "1" Then
                                Console.WriteLine(String.Format(
                                    "[col] absorber estimate: T1={0:F2} T2={1:F2} solvent='{2}' Tsat={3:F2}",
                                    T1, T2, names(solvent), Tsat))
                            End If
                            If Tsat > 0.0 AndAlso Not Double.IsNaN(Tsat) AndAlso
                               Not Double.IsInfinity(Tsat) AndAlso T2 > Tsat Then
                                T2 = Math.Max(Tsat, T1)
                            End If
                        Catch ex As Exception
                        End Try
                    End If
                Case ColType.ReboiledAbsorber
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1
                Case ColType.RefluxedAbsorber
                    P(0) -= CondenserDeltaP
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1
                Case ColType.DistillationColumn
                    If Not DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        P(0) -= CondenserDeltaP
                    End If
                    Try
                        IObj?.SetCurrent()
                        If distVx.Sum > 0 Then
                            Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0, distVx, Nothing, FT.Max)
                            T1 = fcalc.CalculatedTemperature
                            distVy = distVx.MultiplyY(fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()).NormalizeY()
                        Else
                            If Specs("C").SType = ColumnSpec.SpecType.Temperature Then
                                T1 = Specs("C").SpecValue.ConvertToSI(Specs("C").SpecUnit)
                            Else
                                T1 = pp.DW_CalcBubT(zm, P(0), FT.MinY_NonZero())(4) '* 1.01
                            End If
                        End If
                    Catch ex As Exception
                        T1 = FT.Where(Function(t_) t_ > 0.0).Min
                    End Try
                    Try
                        IObj?.SetCurrent()
                        If rebVx.Sum > 0 Then
                            Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(ns), 0, rebVx, Nothing, FT.Max)
                            T2 = fcalc.CalculatedTemperature
                            rebVy = rebVx.MultiplyY(fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()).NormalizeY()
                        Else
                            If Specs("R").SType = ColumnSpec.SpecType.Temperature Then
                                T2 = Specs("R").SpecValue.ConvertToSI(Specs("R").SpecUnit)
                            Else
                                T2 = pp.DW_CalcDewT(zm, P(ns), FT.Max)(4) '* 0.99
                            End If
                        End If
                    Catch ex As Exception
                        T2 = FT.Where(Function(t_) t_ > 0.0).Max
                    End Try
            End Select

            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
            Next

            pp.CurrentMaterialStream = pp.CurrentMaterialStream.Clone()
            pp.CurrentMaterialStream.SetPropertyPackageObject(pp)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).SetFlowsheet(FlowSheet)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).PreferredFlashAlgorithmTag = Me.PreferredFlashAlgorithmTag

            T(0) = T1
            T(ns) = T2

            Dim needsXYestimates As Boolean = False

            i = 0
            For Each st As Stage In Me.Stages
                eff(i) = st.Efficiency
                If Me.UseTemperatureEstimates And InitialEstimates.ValidateTemperatures() And Not ignoreuserestimates Then
                    T(i) = Me.InitialEstimates.StageTemps(i).Value
                Else
                    T(i) = (T2 - T1) * (i) / ns + T1
                End If
                If Me.UseVaporFlowEstimates And InitialEstimates.ValidateVaporFlows() And Not ignoreuserestimates Then
                    V(i) = Me.InitialEstimates.VapMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    V(0) = vaprate
                                Else
                                    If Me.CondenserType = condtype.Total_Condenser Then
                                        V(0) = 0.0000000001
                                    Else
                                        V(0) = vaprate
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Total_Condenser Then
                                    V(0) = 0.0000000001
                                Else
                                    V(0) = vaprate
                                End If
                            Case Else
                                V(0) = F(lastF)
                        End Select
                    Else
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    V(i) = (rr + 1) * V(0) - F(0)
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        V(i) = (rr + 1) * (distrate + vaprate) - F(0)
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        V(i) = (rr + 1) * V(0) - F(0)
                                    Else
                                        V(i) = (rr + 1) * distrate - F(0)
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                V(i) = (rr + 1) * distrate - F(0) + V(0)
                            Case ColType.AbsorptionColumn
                                V(i) = F(lastF)
                            Case ColType.ReboiledAbsorber
                                V(i) = F(lastF)
                        End Select
                    End If
                End If
                If Me.UseLiquidFlowEstimates And InitialEstimates.ValidateLiquidFlows() And Not ignoreuserestimates Then
                    L(i) = Me.InitialEstimates.LiqMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    L(0) = vaprate * rr
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        L(0) = (distrate + vaprate) * rr
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        L(0) = vaprate * rr
                                    Else
                                        L(0) = distrate * rr
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Partial_Condenser Then
                                    L(0) = distrate * rr
                                ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                    L(0) = vaprate * rr
                                Else
                                    L(0) = distrate * rr
                                End If
                            Case Else
                                L(0) = F(firstF)
                                If L(0) = 0 Then L(i) = 0.00001
                        End Select
                    Else
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If i < ns Then L(i) = V(i) + sum1(i) - V(0) Else L(i) = sum1(i) - V(0)
                            Case ColType.AbsorptionColumn
                                L(i) = F(firstF)
                        End Select
                        If L(i) = 0 Then L(i) = 0.00001
                    End If
                End If
                If Me.UseCompositionEstimates And InitialEstimates.ValidateCompositions() And Not ignoreuserestimates Then
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.LiqCompositions(i).Values
                        x(i)(j) = par.Value
                        j = j + 1
                    Next
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.VapCompositions(i).Values
                        y(i)(j) = par.Value
                        j = j + 1
                    Next
                    z(i) = zm
                    If pp.ShouldUseKvalueMethod3 Then
                        Kval(i) = pp.DW_CalcKvalue3(x(i).MultiplyConstY(L(i)), y(i).MultiplyConstY(V(i)), T(i), P(i))
                    ElseIf pp.ShouldUseKvalueMethod2 Then
                        Kval(i) = pp.DW_CalcKvalue(x(i).MultiplyConstY(L(i)).AddY(y(i).MultiplyConstY(V(i))), T(i), P(i))
                    Else
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    End If
                Else
                    IObj?.SetCurrent()
                    z(i) = zm
                    If rebVx.Sum > 0 And distVx.Sum > 0 Then
                        For j = 0 To nc - 1
                            x(i)(j) = distVx(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (rebVx(j) - distVx(j))
                            y(i)(j) = distVy(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (rebVy(j) - distVy(j))
                        Next
                        x(i) = x(i).NormalizeY
                        y(i) = y(i).NormalizeY
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    Else
                        If pp.ShouldUseKvalueMethod3 Then
                            Kval(i) = pp.DW_CalcKvalue(z(i), T(i), P(i))
                        ElseIf pp.ShouldUseKvalueMethod2 Then
                            Kval(i) = pp.DW_CalcKvalue(z(i), T(i), P(i))
                        Else
                            Kval(i) = pp.DW_CalcKvalue_Ideal_Wilson(T(i), P(i))
                        End If
                        If ColumnType = ColType.AbsorptionColumn Then
                            For j = 0 To nc - 1
                                x(i)(j) = (L(i) + V(i)) * z(i)(j) / (L(i) + V(i) * Kval(i)(j))
                                y(i)(j) = Kval(i)(j) * x(i)(j)
                            Next
                            x(i) = x(i).NormalizeY()
                            y(i) = y(i).NormalizeY()
                        Else
                            needsXYestimates = True
                        End If
                    End If
                    If llextractor And pp.AUX_CheckTrivial(Kval(i)) Then
                        Throw New Exception("Your column is configured as a Liquid-Liquid Extractor, but the Property Package / Flash Algorithm set associated with the column is unable to generate an initial estimate for two liquid phases. Please select a different set or change the Flash Algorithm's Stability Analysis parameters and try again.")
                    End If
                End If
                i = i + 1
            Next
            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    Q(0) = 0
                    Q(ns) = 0
                Case ColType.ReboiledAbsorber
                    Q(ns) = 0
                Case ColType.RefluxedAbsorber
                    Q(0) = 0
            End Select

            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Temperature Profile: {0}", T.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Interstage Liquid Flow Rate: {0} mol/s", L.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Interstage Vapor/Liquid2 Flow Rate: {0} mol/s", V.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Liquid Side Draw Rate: {0} mol/s", LSS.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Vapor/Liquid2 Side Draw Rate: {0} mol/s", VSS.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Heat Added/Removed Profile: {0} kW", Q.ToMathArrayString))

            Dim L1trials, L2trials As New List(Of Double())
            Dim x1trials, x2trials As New List(Of Double()())

            If Not llextractor Then

                If needsXYestimates Then

                    LSS(0) = 0
                    VSS(0) = 0
                    LSS(ns) = 0

                    Dim sumLSS = LSS.Sum
                    Dim sumVSS = VSS.Sum

                    VSS(0) = vaprate
                    LSS(0) = distrate
                    LSS(ns) = sumF - LSS(0) - sumLSS - sumVSS - V(0)

                    For i = 0 To ns
                        Dim sflash As Object() = pp.FlashBase.Flash_PT(zm, P(i), T(i), pp)
                        x(i) = sflash(2)
                        y(i) = sflash(3)
                        Kval(i) = sflash(9)
                    Next

                    'LSS(0) = 0
                    VSS(0) = 0
                    LSS(ns) = 0

                End If

            Else

                If Not UseCompositionEstimates Or Not UseLiquidFlowEstimates Or Not UseVaporFlowEstimates Then

                    'll extractor
                    Dim L1, L2, Vx1(), Vx2() As Double
                    Dim trialcomp As Double() = zm.Clone
                    For counter As Integer = 0 To 100
                        Dim flashresult = pp.FlashBase.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                If UseLiquidFlowEstimates Then
                                    L1t.Add(L(i))
                                Else
                                    L1t.Add(F.Sum * L1)
                                End If
                                If UseVaporFlowEstimates Then
                                    L2t.Add(V(i))
                                Else
                                    L2t.Add(F.Sum * L2)
                                End If
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i).Clone)
                                    xt2.Add(y(i).Clone)
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                    trialcomp = zm.Clone
                    Dim lle As New PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE()
                    For counter As Integer = 0 To 100
                        Dim flashresult = lle.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 And Vx1.SubtractY(Vx2).AbsSqrSumY > 0.001 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                If UseLiquidFlowEstimates Then
                                    L1t.Add(L(i))
                                Else
                                    L1t.Add(F.Sum * L1)
                                End If
                                If UseVaporFlowEstimates Then
                                    L2t.Add(V(i))
                                Else
                                    L2t.Add(F.Sum * L2)
                                End If
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i))
                                    xt2.Add(y(i))
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                Else

                    Dim L1t, L2t As New List(Of Double)
                    Dim xt1, xt2 As New List(Of Double())
                    For i = 0 To Stages.Count - 1
                        L1t.Add(L(i))
                        L2t.Add(V(i))
                        xt1.Add(x(i).Clone)
                        xt2.Add(y(i).Clone)
                    Next
                    L1trials.Add(L1t.ToArray())
                    L2trials.Add(L2t.ToArray())
                    x1trials.Add(xt1.ToArray())
                    x2trials.Add(xt2.ToArray())

                End If

            End If

            IObj?.Paragraphs.Add("<h2>Column Specifications</h2>")

            IObj?.Paragraphs.Add("Processing Specs...")

            'process specifications
            For Each sp As Auxiliary.SepOps.ColumnSpec In Me.Specs.Values
                If sp.SType = ColumnSpec.SpecType.Component_Fraction Or
                sp.SType = ColumnSpec.SpecType.Component_Mass_Flow_Rate Or
                sp.SType = ColumnSpec.SpecType.Component_Molar_Flow_Rate Or
                sp.SType = ColumnSpec.SpecType.Component_Recovery Then
                    sp.ComponentIndex = Vn.IndexOf(sp.ComponentID)
                End If
                If sp.StageNumber = -1 And sp.SpecValue = Me.DistillateFlowRate Then
                    sumF = 0
                    Dim sumLSS As Double = 0
                    Dim sumVSS As Double = 0
                    For i = 0 To ns
                        sumF += F(i)
                        sumLSS += LSS(i)
                        sumVSS += VSS(i)
                    Next
                    sp.SpecValue = sumF - sumLSS - sumVSS - V(0)
                    sp.StageNumber = 0
                End If
                IObj?.Paragraphs.Add(String.Format("Spec Type: {0}", [Enum].GetName(sp.SType.GetType, sp.SType)))
                IObj?.Paragraphs.Add(String.Format("Spec Value: {0}", sp.SpecValue))
                IObj?.Paragraphs.Add(String.Format("Spec Stage: {0}", sp.StageNumber))
                IObj?.Paragraphs.Add(String.Format("Spec Units: {0}", sp.SpecUnit))
                IObj?.Paragraphs.Add(String.Format("Compound (if applicable): {0}", sp.ComponentID))
            Next

            IObj?.Close()

            Dim solverinput As New ColumnSolverInputData

            With solverinput
                .ColumnObject = Me
                .StageTemperatures = T.ToList
                .StagePressures = P.ToList
                .StageHeats = Q.ToList
                .StageEfficiencies = eff.ToList
                .NumberOfCompounds = nc
                .NumberOfStages = ns
                .ColumnType = ColumnType
                .CondenserSpec = Specs("C")
                .ReboilerSpec = Specs("R")
                .CondenserType = CondenserType
                .FeedCompositions = fc.ToList
                .FeedEnthalpies = HF.ToList
                .FeedFlows = F.ToList
                .VaporCompositions = y.ToList
                .VaporFlows = V.ToList
                .VaporSideDraws = VSS.ToList
                .LiquidCompositions = x.ToList
                .LiquidFlows = L.ToList
                .LiquidSideDraws = LSS.ToList
                .Kvalues = Kval.ToList()
                .MaximumIterations = maxits
                .Tolerances = tol.ToList
                .OverallCompositions = z.ToList
                .L1trials = L1trials
                .L2trials = L2trials
                .x1trials = x1trials
                .x2trials = x2trials
                If TypeOf Me Is DistillationColumn Then
                    .SubcoolingDeltaT = DirectCast(Me, DistillationColumn).TotalCondenserSubcoolingDeltaT
                End If
            End With

            Return solverinput

        End Function

        Public Overridable Function GetSolverInputData_New(Optional ByVal ignoreuserestimates As Boolean = False) As ColumnSolverInputData

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()

            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Calculation Routine", True)

            IObj?.SetCurrent()

            IObj?.Paragraphs.Add("For any stage in a countercurrent cascade, assume (1) phase equilibrium is achieved at each stage, (2) no chemical reactions occur, and (3) entrainment of liquid drops in vapor and occlusion of vapor bubbles in liquid are negligible. Figure 1 represents such a stage for the vaporï¿½liquid case, where the stages are numbered down from the top. The same representation applies to liquidï¿½liquid extraction if the higher-density liquid phases are represented by liquid streams and the lower-density liquid phases are represented by vapor streams.")

            IObj?.Paragraphs.Add(InspectorItem.GetImageHTML("image1.jpg"))

            IObj?.Paragraphs.Add("Entering stage j is a single- or two-phase feed of molar flow rate Fj, with overall composition in mole fractions zi,j of component i, temperature TFj , pressure PFj , and corresponding overall molar enthalpy hFj.")

            IObj?.Paragraphs.Add("Also entering stage j is interstage liquid from stage j-1 above, if any, of molar flow rate Lj-1, with composition in mole fractions xij-1, enthalpy hLj-1, temperature Tj-1, and pressure Pj-1, which is equal to or less than the pressure of stage j. Pressure of liquid from stage j-1 is increased adiabatically by hydrostatic head change across head L.")

            IObj?.Paragraphs.Add("Similarly, from stage j+1 below, interstage vapor of molar flow rate V+1, with composition in mole fractions yij+1, enthalpy hV+1, temperature Tj+1, and pressure Pj+1 enters stage j.")

            IObj?.Paragraphs.Add("Leaving stage j is vapor of intensive properties yij, hVj, Tj, and Pj. This stream can be divided into a vapor sidestream of molar flow rate Wj and an interstage stream of molar flow rate Vj to be sent to stage j-1 or, if j=1, to leave as a product. Also leaving stage j is liquid of intensive properties xij, hLj, Tj, and Pj, in equilibrium with vapor (Vj+Wj). This liquid is divided into a sidestream of molar flow rate Uj and an interstage stream of molar flow rate Lj to be sent to stage j+1 or, if j=N, to leave as a product.")

            IObj?.Paragraphs.Add("Associated with each general stage are the following indexed equations expressed in terms of the variable set in Figure 1. However, variables other than those shown in Figure 1 can be used, e.g. component flow rates can replace mole fractions, and sidestream flow rates can be expressed as fractions of interstage flow rates. The equations are referred to as MESH equations, after Wang and Henke.")

            IObj?.Paragraphs.Add("M equationsï¿½Material balance for each component (C equations for each stage).")

            IObj?.Paragraphs.Add("<m>M_{i,j}=L_{j-1}x_{i,j-1}+V_{j+1}y_{i,j+1}+F_jz_{i,j}-(L_j+U_j)x_{i,j}-(V_j+W_j)y_{i,j}</m>")

            IObj?.Paragraphs.Add("E equationsï¿½phase-Equilibrium relation for each component (C equations for each stage),")

            IObj?.Paragraphs.Add("<m>E_{i,j}=y_{i,j}-K_{i,j}x_{i,j}=0</m>")

            IObj?.Paragraphs.Add("where <mi>K_{i,j}</mi> is the phase-equilibrium ratio or K-value.")

            IObj?.Paragraphs.Add("S equationsï¿½mole-fraction Summations (one for each stage),")

            IObj?.Paragraphs.Add("<m>(S_y)_j=\sum\limits_{i=1}^{C}{y_{i,j}}-1=0</m>")

            IObj?.Paragraphs.Add("<m>(S_x)_j=\sum\limits_{i=1}^{C}{x_{i,j}} -1=0</m>")

            IObj?.Paragraphs.Add("H equationï¿½energy balance (one for each stage).")

            IObj?.Paragraphs.Add("<m>H_j=L_{j-1}h_{L_{j-1}}+V_{j+1}h_{V_{j+1}}+F_jh_{F_j}-(L_j+U_j)h_{L_j}-(V_j+W_j)h_{V_j}-Q_j=0</m>")

            IObj?.Paragraphs.Add("A countercurrent cascade of N such stages is represented by N(2C+3) such equations in [N(3C+10)+1] variables. If N and all Fj, zij, TFj, PFj, Pj, Uj, Wj, and Qj are specified, the model is represented by N(2C+3) simultaneous algebraic equations in N(2C+3) unknown (output) variables comprising all xij, yij, Lj, Vj, and Tj, where the M, E, and H equations are nonlinear. If other variables are specified, corresponding substitutions are made to the list of output variables. Regardless, the result is a set containing nonlinear equations that must be solved by an iterative technique.")

            IObj?.Paragraphs.Add("<h2>Initial Estimates</h2>")

            IObj?.Paragraphs.Add("DWSIM will calculate new or use existing initial estimates and forward the values to the selected solver.")

            'Validate unitop status.

            Me.Validate()

            'Check connectors' positions

            Me.CheckConnPos()

            'handle special cases when no initial estimates are used

            Dim special As Boolean = False

            Dim Vn = FlowSheet.SelectedCompounds.Keys.ToList()

            If Vn.Contains("Ethanol") And Vn.Contains("Water") Then
                'probably an azeotrope situation.
                special = True
            End If

            'prepare variables

            Dim llextractor As Boolean = False
            Dim myabs As AbsorptionColumn = TryCast(Me, AbsorptionColumn)
            If myabs IsNot Nothing Then
                If CType(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Absorber Then
                    llextractor = False
                Else
                    llextractor = True
                End If
            End If

            Dim pp As PropertyPackages.PropertyPackage = Me.PropertyPackage

            Dim nc, ns, maxits, i, j As Integer
            Dim firstF As Integer = -1
            Dim lastF As Integer = -1
            nc = Me.FlowSheet.SelectedCompounds.Count
            ns = Me.Stages.Count - 1
            maxits = Me.MaxIterations

            Dim tol(4) As Double
            tol(0) = Me.InternalLoopTolerance
            tol(1) = Me.ExternalLoopTolerance

            Dim F(ns), Q(ns), V(ns), L(ns), VSS(ns), LSS(ns), HF(ns), T(ns), FT(ns), P(ns), fracv(ns), eff(ns),
              distrate, rr, vaprate As Double

            Dim x(ns)() As Double, y(ns)() As Double, z(ns)() As Double, fc(ns)() As Double
            Dim idealK(ns)(), Kval(ns)(), Pvap(ns)() As Double

            For i = 0 To ns
                Array.Resize(x(i), nc)
                Array.Resize(y(i), nc)
                Array.Resize(fc(i), nc)
                Array.Resize(z(i), nc)
                Array.Resize(idealK(i), nc)
                Array.Resize(Kval(i), nc)
                Array.Resize(Pvap(i), nc)
            Next

            If Not Double.IsNaN(ColumnPressureDrop) Then
                For i = 1 To ns
                    Stages(i).P = Stages(0).P + Convert.ToDouble(i) / Convert.ToDouble(ns) * ColumnPressureDrop
                Next
            End If

            i = 0
            For Each st As Stage In Me.Stages
                P(i) = st.P
                i += 1
            Next

            Dim sumcf(nc - 1), sumF, zm(nc - 1), xtop(nc - 1), ytop(nc - 1), xbot(nc - 1), ybot(nc - 1), alpha(nc - 1), distVx(nc - 1), rebVx(nc - 1), distVy(nc - 1), rebVy(nc - 1) As Double

            IObj?.Paragraphs.Add("Collecting data from connected streams...")

            i = 0

            Dim stream As MaterialStream = Nothing

            For Each ms As StreamInformation In Me.MaterialStreams.Values
                Select Case ms.StreamBehavior
                    Case StreamInformation.Behavior.Feed
                        stream = FlowSheet.SimulationObjects(ms.StreamID)
                        pp.CurrentMaterialStream = stream
                        F(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.molarflow.GetValueOrDefault
                        HF(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.enthalpy.GetValueOrDefault *
                                                              stream.Phases(0).Properties.molecularWeight.GetValueOrDefault
                        FT(StageIndex(ms.AssociatedStage)) = stream.Phases(0).Properties.temperature.GetValueOrDefault
                        sumF += F(StageIndex(ms.AssociatedStage))
                        j = 0
                        For Each comp As Thermodynamics.BaseClasses.Compound In stream.Phases(0).Compounds.Values
                            fc(StageIndex(ms.AssociatedStage))(j) = comp.MoleFraction.GetValueOrDefault
                            z(StageIndex(ms.AssociatedStage))(j) = comp.MoleFraction.GetValueOrDefault
                            sumcf(j) += comp.MoleFraction.GetValueOrDefault * F(StageIndex(ms.AssociatedStage))
                            j = j + 1
                        Next
                    Case StreamInformation.Behavior.Sidedraw
                        If ms.StreamPhase = StreamInformation.Phase.V Then
                            VSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        Else
                            LSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        End If
                    Case StreamInformation.Behavior.InterExchanger
                        Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End Select
                i += 1
            Next

            For Each ms As StreamInformation In Me.EnergyStreams.Values
                Select Case ms.StreamBehavior
                    Case StreamInformation.Behavior.InterExchanger
                        Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End Select
                i += 1
            Next

            Dim cv As New SystemsOfUnits.Converter

            vaprate = SystemsOfUnits.Converter.ConvertToSI(Me.VaporFlowRateUnit, Me.VaporFlowRate)

            Dim sum1(ns), sum0_ As Double
            sum0_ = 0
            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
                sum0_ += LSS(i) + VSS(i)
            Next

            'firstF/lastF are the topmost and bottom-most fed stages. Both are found
            'by scanning the stages: taking firstF from the order the feeds happen to
            'sit in MaterialStreams makes the initial liquid traffic depend on the
            'order they were connected in, which for an absorber fed liquid at the top
            'and gas at the bottom can start L off at the gas rate.
            For i = 0 To ns
                If F(i) <> 0 Then
                    firstF = i
                    Exit For
                End If
            Next

            For i = ns To 0 Step -1
                If F(i) <> 0 Then
                    lastF = i
                    Exit For
                End If
            Next

            For i = 0 To nc - 1
                zm(i) = sumcf(i) / sumF
            Next

            Dim mwf = pp.AUX_MMM(zm)

            If TypeOf Me Is DistillationColumn Then
                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                    rr = 3.0
                Else
                    If Me.Specs("C").SType = ColumnSpec.SpecType.Stream_Ratio Then
                        rr = Me.Specs("C").SpecValue
                    ElseIf Me.Specs("C").SType = ColumnSpec.SpecType.Component_Fraction Or
                  Me.Specs("C").SType = ColumnSpec.SpecType.Component_Recovery Then
                        rr = 10.0
                    Else
                        rr = 2.5
                    End If
                End If
            End If

            If InitialEstimates.RefluxRatio IsNot Nothing And
              UseVaporFlowEstimates And UseLiquidFlowEstimates Then
                rr = InitialEstimates.RefluxRatio
            End If

            Dim Tref = FT.Where(Function(ti) ti > 0).Average
            Dim Pref = Stages.Select(Function(s) s.P).Average

            Dim fflash As Object() = pp.FlashBase.Flash_PT(zm, Pref, Tref, pp)

            Dim fflash2 As Object() = pp.FlashBase.Flash_PT(fflash(3), Pref, Tref - rr * 5, pp)

            Dim Lflash = fflash(0)
            Dim Vflash = fflash(1)

            Dim Lflash2 = fflash2(0)
            Dim Vflash2 = fflash2(1)

            Dim result As Object = Nothing

            If Me.CondenserType = condtype.Full_Reflux Then
                result = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0.9, fflash(3), Nothing, Tref)
            Else
                If Vflash2 > 0.0 Then
                    result = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0.1, fflash2(3), Nothing, Tref)
                Else
                    result = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0.1, fflash(3), Nothing, Tref)
                End If
            End If

            T(0) = result.CalculatedTemperature

            xtop = result.GetLiquidPhase1MoleFractions()
            ytop = fflash(3)

            result = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(ns), 0.9, fflash(2), Nothing, Tref)

            T(ns) = result.CalculatedTemperature

            xbot = result.GetLiquidPhase1MoleFractions()
            ybot = result.GetVaporPhaseMoleFractions()

            Dim Kref = fflash(9)

            Dim Vprops = pp.DW_GetConstantProperties()

            Dim hamount As Double = 0.0

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    rebVx(cindex) = cvalue * zm(cindex) * sumF
                    hamount = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Product_Mass_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Product_Molar_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Feed_Recovery
                    Dim cvalue = Specs("R").SpecValue / 100.0
                    Dim pval = sumF * cvalue
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - pval - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - pval - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - pval - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - pval - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case Else
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF / 2 - sum0_
                        Else
                            distrate = sumF / 2 - sum0_ - vaprate
                        End If
                    End If
            End Select

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate,
                    ColumnSpec.SpecType.Component_Molar_Flow_Rate,
                    ColumnSpec.SpecType.Component_Recovery,
                    ColumnSpec.SpecType.Component_Fraction
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - hamount - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - hamount - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - hamount - sum0_
                            vaprate = 0.0
                        End If
                    End If
            End Select

            If InitialEstimates.VaporProductFlowRate IsNot Nothing And UseVaporFlowEstimates And Not ignoreuserestimates Then
                vaprate = InitialEstimates.VaporProductFlowRate
            End If
            If InitialEstimates.DistillateFlowRate IsNot Nothing And UseLiquidFlowEstimates And Not ignoreuserestimates Then
                distrate = InitialEstimates.DistillateFlowRate
            End If

            If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                distrate = 0.0
            Else
                If Me.CondenserType = condtype.Full_Reflux Then
                    distrate = 0.0
                ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                Else
                    vaprate = 0.0
                End If
            End If

            Dim lamount As Double = 0.0

            Select Case Specs("C").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    lamount = cvalue * zm(cindex) * sumF
                    distVx(cindex) = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
            End Select

            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Distillate Rate: {0} mol/s", distrate))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Vapor Overflow Rate: {0} mol/s", vaprate))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Reflux Ratio: {0}", rr))

            compids = New ArrayList
            For Each compName As String In Vn
                compids.Add(compName)
            Next

            Dim T1, T2 As Double

            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    LSS(0) = distrate
                Case ColType.RefluxedAbsorber
                    LSS(0) = distrate
            End Select

            Select Case Me.ColumnType
                Case ColType.AbsorptionColumn
                    T1 = FT.First
                    T2 = FT.Last
                    If (T1 = 0.0) Then Throw New Exception("The absorber needs a feed stream connected to the first stage.")
                    If (T2 = 0.0) Then Throw New Exception("The absorber needs a feed stream connected to the last stage.")

                    'A condensable vapour fed at the bottom does not hold that stage at
                    'its own inlet temperature: it condenses, and the stage settles at
                    'the boiling point of the liquid running down. Steam fed 45 K above
                    'that otherwise starts the ramp far too hot, and the temperature
                    'update walks away from the answer rather than back to it.
                    '
                    'The anchor is the saturation temperature of the liquid feed's own
                    'solvent, not a bubble point of the mixture. A rigorous bubble point
                    'is useless here: dissolved CO2 and H2S at a few hundred ppm pull the
                    'answer down to 304 K - the critical temperature of CO2, which is
                    'what the calculation actually pins on - for a stream that boils at
                    '373 K. The stripped liquid leaving the bottom is essentially pure
                    'solvent, so the solvent's boiling point is the temperature that
                    'stage really approaches.
                    If firstF >= 0 AndAlso T2 > T1 Then
                        Try
                            Dim names = FlowSheet.SelectedCompounds.Keys.ToList()
                            Dim solvent As Integer = 0
                            For k As Integer = 1 To Math.Min(names.Count, fc(firstF).Length) - 1
                                If fc(firstF)(k) > fc(firstF)(solvent) Then solvent = k
                            Next
                            Dim Tsat = pp.AUX_TSATi(P(ns), names(solvent))
                            If Environment.GetEnvironmentVariable("DWSIM_COLUMN_TRACE") = "1" Then
                                Console.WriteLine(String.Format(
                                    "[col] absorber estimate: T1={0:F2} T2={1:F2} solvent='{2}' Tsat={3:F2}",
                                    T1, T2, names(solvent), Tsat))
                            End If
                            If Tsat > 0.0 AndAlso Not Double.IsNaN(Tsat) AndAlso
                               Not Double.IsInfinity(Tsat) AndAlso T2 > Tsat Then
                                T2 = Math.Max(Tsat, T1)
                            End If
                        Catch ex As Exception
                        End Try
                    End If
                Case ColType.ReboiledAbsorber
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1
                Case ColType.RefluxedAbsorber
                    P(0) -= CondenserDeltaP
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1
                Case ColType.DistillationColumn
                    If Not DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        P(0) -= CondenserDeltaP
                    End If
                    If special Then
                        T1 = Tref
                    Else
                        Try
                            IObj?.SetCurrent()
                            If distVx.Sum > 0 Then
                                Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0, distVx, Nothing, FT.Max)
                                T1 = fcalc.CalculatedTemperature
                                distVy = distVx.MultiplyY(fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()).NormalizeY()
                            Else
                                If Specs("C").SType = ColumnSpec.SpecType.Temperature Then
                                    T1 = Specs("C").SpecValue.ConvertToSI(Specs("C").SpecUnit)
                                Else
                                    T1 = T(0)
                                End If
                            End If
                        Catch ex As Exception
                            T1 = FT.Where(Function(t_) t_ > 0.0).Min
                        End Try
                    End If
                    If special Then
                        T2 = Tref
                    Else
                        Try
                            IObj?.SetCurrent()
                            If rebVx.Sum > 0 Then
                                Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(ns), 0, rebVx, Nothing, FT.Max)
                                T2 = fcalc.CalculatedTemperature
                                rebVy = rebVx.MultiplyY(fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()).NormalizeY()
                            Else
                                If Specs("R").SType = ColumnSpec.SpecType.Temperature Then
                                    T2 = Specs("R").SpecValue.ConvertToSI(Specs("R").SpecUnit)
                                Else
                                    T2 = T(ns)
                                End If
                            End If
                        Catch ex As Exception
                            T2 = FT.Where(Function(t_) t_ > 0.0).Max
                        End Try
                    End If
            End Select

            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
            Next

            pp.CurrentMaterialStream = pp.CurrentMaterialStream.Clone()
            pp.CurrentMaterialStream.SetPropertyPackageObject(pp)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).SetFlowsheet(FlowSheet)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).PreferredFlashAlgorithmTag = Me.PreferredFlashAlgorithmTag

            T(0) = T1
            T(ns) = T2

            Dim needsXYestimates As Boolean = False

            i = 0
            For Each st As Stage In Me.Stages
                eff(i) = st.Efficiency
                If Me.UseTemperatureEstimates And InitialEstimates.ValidateTemperatures() And Not ignoreuserestimates Then
                    T(i) = Me.InitialEstimates.StageTemps(i).Value
                Else
                    T(i) = (T2 - T1) * (i) / ns + T1
                End If
                If Me.UseVaporFlowEstimates And InitialEstimates.ValidateVaporFlows() And Not ignoreuserestimates Then
                    V(i) = Me.InitialEstimates.VapMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    V(0) = vaprate
                                Else
                                    If Me.CondenserType = condtype.Total_Condenser Then
                                        V(0) = 0.0000000001
                                    Else
                                        V(0) = vaprate
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Total_Condenser Then
                                    V(0) = 0.0000000001
                                Else
                                    V(0) = vaprate
                                End If
                            Case Else
                                V(0) = F(lastF)
                        End Select
                    Else
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    V(i) = (rr + 1) * V(0) - F(0)
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        V(i) = (rr + 1) * (distrate + vaprate) - F(0)
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        V(i) = (rr + 1) * V(0) - F(0)
                                    Else
                                        V(i) = (rr + 1) * distrate - F(0)
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                V(i) = (rr + 1) * distrate - F(0) + V(0)
                            Case ColType.AbsorptionColumn
                                V(i) = F(lastF)
                            Case ColType.ReboiledAbsorber
                                V(i) = F(lastF)
                        End Select
                    End If
                End If
                If Me.UseLiquidFlowEstimates And InitialEstimates.ValidateLiquidFlows() And Not ignoreuserestimates Then
                    L(i) = Me.InitialEstimates.LiqMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    L(0) = vaprate * rr
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        L(0) = (distrate + vaprate) * rr
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        L(0) = vaprate * rr
                                    Else
                                        L(0) = distrate * rr
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Partial_Condenser Then
                                    L(0) = distrate * rr
                                ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                    L(0) = vaprate * rr
                                Else
                                    L(0) = distrate * rr
                                End If
                            Case Else
                                L(0) = F(firstF)
                                If L(0) = 0 Then L(i) = 0.00001
                        End Select
                    Else
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If i < ns Then L(i) = V(i) + sum1(i) - V(0) Else L(i) = sum1(i) - V(0)
                            Case ColType.AbsorptionColumn
                                L(i) = F(firstF)
                        End Select
                        If L(i) = 0 Then L(i) = 0.00001
                    End If
                End If
                If Me.UseCompositionEstimates And InitialEstimates.ValidateCompositions() And Not ignoreuserestimates Then
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.LiqCompositions(i).Values
                        x(i)(j) = par.Value
                        j = j + 1
                    Next
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.VapCompositions(i).Values
                        y(i)(j) = par.Value
                        j = j + 1
                    Next
                    z(i) = zm
                    If pp.ShouldUseKvalueMethod3 Then
                        Kval(i) = pp.DW_CalcKvalue3(x(i).MultiplyConstY(L(i)), y(i).MultiplyConstY(V(i)), T(i), P(i))
                    ElseIf pp.ShouldUseKvalueMethod2 Then
                        Kval(i) = pp.DW_CalcKvalue(x(i).MultiplyConstY(L(i)).AddY(y(i).MultiplyConstY(V(i))), T(i), P(i))
                    Else
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    End If
                Else
                    IObj?.SetCurrent()
                    z(i) = zm
                    If rebVx.Sum > 0 And distVx.Sum > 0 Then
                        For j = 0 To nc - 1
                            x(i)(j) = distVx(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (rebVx(j) - distVx(j))
                            y(i)(j) = distVy(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (rebVy(j) - distVy(j))
                        Next
                        x(i) = x(i).NormalizeY
                        y(i) = y(i).NormalizeY
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    Else
                        For j = 0 To nc - 1
                            x(i)(j) = xtop(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (xbot(j) - xtop(j))
                            y(i)(j) = ytop(j) + Convert.ToDouble(i) / Convert.ToDouble(ns) * (ybot(j) - ytop(j))
                        Next
                        x(i) = x(i).NormalizeY
                        y(i) = y(i).NormalizeY
                        If pp.ShouldUseKvalueMethod3 Then
                            Kval(i) = pp.DW_CalcKvalue(z(i), T(i), P(i))
                        ElseIf pp.ShouldUseKvalueMethod2 Then
                            Kval(i) = pp.DW_CalcKvalue(z(i), T(i), P(i))
                        Else
                            Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                        End If
                        If ColumnType = ColType.AbsorptionColumn Then
                            For j = 0 To nc - 1
                                x(i)(j) = (L(i) + V(i)) * z(i)(j) / (L(i) + V(i) * Kval(i)(j))
                                y(i)(j) = Kval(i)(j) * x(i)(j)
                            Next
                            x(i) = x(i).NormalizeY()
                            y(i) = y(i).NormalizeY()
                        Else
                            needsXYestimates = True
                        End If
                    End If
                    If llextractor And pp.AUX_CheckTrivial(Kval(i)) Then
                        Throw New Exception("Your column is configured as a Liquid-Liquid Extractor, but the Property Package / Flash Algorithm set associated with the column is unable to generate an initial estimate for two liquid phases. Please select a different set or change the Flash Algorithm's Stability Analysis parameters and try again.")
                    End If
                End If
                i = i + 1
            Next
            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    Q(0) = 0
                    Q(ns) = 0
                Case ColType.ReboiledAbsorber
                    Q(ns) = 0
                Case ColType.RefluxedAbsorber
                    Q(0) = 0
            End Select

            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Temperature Profile: {0}", T.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Interstage Liquid Flow Rate: {0} mol/s", L.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Interstage Vapor/Liquid2 Flow Rate: {0} mol/s", V.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Liquid Side Draw Rate: {0} mol/s", LSS.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Vapor/Liquid2 Side Draw Rate: {0} mol/s", VSS.ToMathArrayString))
            IObj?.Paragraphs.Add(String.Format("Estimated/Specified Heat Added/Removed Profile: {0} kW", Q.ToMathArrayString))

            Dim L1trials, L2trials As New List(Of Double())
            Dim x1trials, x2trials As New List(Of Double()())

            If Not llextractor Then

                If needsXYestimates Then

                    LSS(0) = 0
                    VSS(0) = 0
                    LSS(ns) = 0

                    Dim sumLSS = LSS.Sum
                    Dim sumVSS = VSS.Sum

                    VSS(0) = vaprate
                    LSS(0) = distrate
                    LSS(ns) = sumF - LSS(0) - sumLSS - sumVSS - V(0)

                    'For i = 0 To ns
                    '    Dim sflash As Object() = pp.FlashBase.Flash_PT(zm, P(i), T(i), pp)
                    '    x(i) = sflash(2)
                    '    y(i) = sflash(3)
                    '    Kval(i) = sflash(9)
                    'Next

                    'LSS(0) = 0
                    VSS(0) = 0
                    LSS(ns) = 0

                End If

            Else

                If Not UseCompositionEstimates Or Not UseLiquidFlowEstimates Or Not UseVaporFlowEstimates Then

                    'll extractor
                    Dim L1, L2, Vx1(), Vx2() As Double
                    Dim trialcomp As Double() = zm.Clone
                    For counter As Integer = 0 To 100
                        Dim flashresult = pp.FlashBase.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                If UseLiquidFlowEstimates Then
                                    L1t.Add(L(i))
                                Else
                                    L1t.Add(F.Sum * L1)
                                End If
                                If UseVaporFlowEstimates Then
                                    L2t.Add(V(i))
                                Else
                                    L2t.Add(F.Sum * L2)
                                End If
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i).Clone)
                                    xt2.Add(y(i).Clone)
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                    trialcomp = zm.Clone
                    Dim lle As New PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE()
                    For counter As Integer = 0 To 100
                        Dim flashresult = lle.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 And Vx1.SubtractY(Vx2).AbsSqrSumY > 0.001 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                If UseLiquidFlowEstimates Then
                                    L1t.Add(L(i))
                                Else
                                    L1t.Add(F.Sum * L1)
                                End If
                                If UseVaporFlowEstimates Then
                                    L2t.Add(V(i))
                                Else
                                    L2t.Add(F.Sum * L2)
                                End If
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i))
                                    xt2.Add(y(i))
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                Else

                    Dim L1t, L2t As New List(Of Double)
                    Dim xt1, xt2 As New List(Of Double())
                    For i = 0 To Stages.Count - 1
                        L1t.Add(L(i))
                        L2t.Add(V(i))
                        xt1.Add(x(i).Clone)
                        xt2.Add(y(i).Clone)
                    Next
                    L1trials.Add(L1t.ToArray())
                    L2trials.Add(L2t.ToArray())
                    x1trials.Add(xt1.ToArray())
                    x2trials.Add(xt2.ToArray())

                End If

            End If

            IObj?.Paragraphs.Add("<h2>Column Specifications</h2>")

            IObj?.Paragraphs.Add("Processing Specs...")

            'process specifications
            For Each sp As Auxiliary.SepOps.ColumnSpec In Me.Specs.Values
                If sp.SType = ColumnSpec.SpecType.Component_Fraction Or
              sp.SType = ColumnSpec.SpecType.Component_Mass_Flow_Rate Or
              sp.SType = ColumnSpec.SpecType.Component_Molar_Flow_Rate Or
              sp.SType = ColumnSpec.SpecType.Component_Recovery Then
                    sp.ComponentIndex = Vn.IndexOf(sp.ComponentID)
                End If
                If sp.StageNumber = -1 And sp.SpecValue = Me.DistillateFlowRate Then
                    sumF = 0
                    Dim sumLSS As Double = 0
                    Dim sumVSS As Double = 0
                    For i = 0 To ns
                        sumF += F(i)
                        sumLSS += LSS(i)
                        sumVSS += VSS(i)
                    Next
                    sp.SpecValue = sumF - sumLSS - sumVSS - V(0)
                    sp.StageNumber = 0
                End If
                IObj?.Paragraphs.Add(String.Format("Spec Type: {0}", [Enum].GetName(sp.SType.GetType, sp.SType)))
                IObj?.Paragraphs.Add(String.Format("Spec Value: {0}", sp.SpecValue))
                IObj?.Paragraphs.Add(String.Format("Spec Stage: {0}", sp.StageNumber))
                IObj?.Paragraphs.Add(String.Format("Spec Units: {0}", sp.SpecUnit))
                IObj?.Paragraphs.Add(String.Format("Compound (if applicable): {0}", sp.ComponentID))
            Next

            IObj?.Close()

            If Me.ColumnType = ColType.DistillationColumn Then

                Dim tridiag = WangHenkeMethod2.RunTridiagonal(Me, F, V, Q, L, HF, VSS, LSS, Kval, x, y, z, fc,
                                                              T, P, CondenserType, ns, nc, ColumnType, PropertyPackage, Specs)

                'Return New Object() {Tj, Vj, Lj, VSSj, LSSj, yc, xc, K, Q}

                V = tridiag(1)
                L = tridiag(2)
                y = tridiag(5)
                x = tridiag(6)

            End If


            Dim solverinput As New ColumnSolverInputData

            With solverinput
                .ColumnObject = Me
                .StageTemperatures = T.ToList
                .StagePressures = P.ToList
                .StageHeats = Q.ToList
                .StageEfficiencies = eff.ToList
                .NumberOfCompounds = nc
                .NumberOfStages = ns
                .ColumnType = ColumnType
                .CondenserSpec = Specs("C")
                .ReboilerSpec = Specs("R")
                .CondenserType = CondenserType
                .FeedCompositions = fc.ToList
                .FeedEnthalpies = HF.ToList
                .FeedFlows = F.ToList
                .VaporCompositions = y.ToList
                .VaporFlows = V.ToList
                .VaporSideDraws = VSS.ToList
                .LiquidCompositions = x.ToList
                .LiquidFlows = L.ToList
                .LiquidSideDraws = LSS.ToList
                .Kvalues = Kval.ToList()
                .MaximumIterations = maxits
                .Tolerances = tol.ToList
                .OverallCompositions = z.ToList
                .L1trials = L1trials
                .L2trials = L2trials
                .x1trials = x1trials
                .x2trials = x2trials
                If TypeOf Me Is DistillationColumn Then
                    .SubcoolingDeltaT = DirectCast(Me, DistillationColumn).TotalCondenserSubcoolingDeltaT
                End If
            End With

            Return solverinput

        End Function

        ''' <summary>
        ''' Generates initial estimates for column solvers with improved robustness relative to
        ''' GetSolverInputData_New. Key improvements:
        '''  1. Reflux ratio estimated from relative volatility (Underwood-simplified) instead of hard-coded values.
        '''  2. Condenser/reboiler temperatures from bubble/dew-point of overall feed composition, not from
        '''     a secondary flash at an arbitrary (and potentially invalid) temperature.
        '''  3. ytop taken from the same equilibrium result used to compute T1 and xtop (no inconsistency).
        '''  4. Compositions for non-component specs computed via per-stage PT flash using the accumulated
        '''     feed composition at each stage (no empty x/y arrays, consistent K-values).
        '''  5. All internal vapor and liquid flows clamped above a physical minimum (0.1 % of sumF).
        '''  6. L(i>0) properly estimated for ReboiledAbsorber and RefluxedAbsorber column types.
        '''  7. Absorber T1/T2 derived from the first/last non-zero feed temperature rather than FT(0)/FT(ns).
        '''  8. Special-case Ethanol/Water flag removed; thermodynamic equilibrium is used unconditionally.
        ''' </summary>
        Public Overridable Function GetSolverInputData_Robust(Optional ByVal ignoreuserestimates As Boolean = False) As ColumnSolverInputData

            Dim IObj As Inspector.InspectorItem = Inspector.Host.GetNewInspectorItem()
            Inspector.Host.CheckAndAdd(IObj, "", "Calculate", If(GraphicObject IsNot Nothing, GraphicObject.Tag, "Temporary Object") & " (" & GetDisplayName() & ")", GetDisplayName() & " Initial Estimates (Robust)", True)
            IObj?.SetCurrent()

            Me.Validate()
            Me.CheckConnPos()

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 1. Basic setup
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim llextractor As Boolean = False
            Dim myabs As AbsorptionColumn = TryCast(Me, AbsorptionColumn)
            If myabs IsNot Nothing Then
                llextractor = (CType(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Extractor)
            End If

            Dim Vn = FlowSheet.SelectedCompounds.Keys.ToList()
            Dim pp As PropertyPackages.PropertyPackage = Me.PropertyPackage

            Dim nc As Integer = Me.FlowSheet.SelectedCompounds.Count
            Dim ns As Integer = Me.Stages.Count - 1
            Dim maxits As Integer = Me.MaxIterations

            Dim tol(4) As Double
            tol(0) = Me.InternalLoopTolerance
            tol(1) = Me.ExternalLoopTolerance

            Dim F(ns), Q(ns), V(ns), L(ns), VSS(ns), LSS(ns), HF(ns), T(ns), FT(ns), P(ns), eff(ns),
                distrate, rr, vaprate As Double

            Dim x(ns)() As Double, y(ns)() As Double, z(ns)() As Double, fc(ns)() As Double
            Dim Kval(ns)() As Double

            Dim i As Integer, j As Integer
            For i = 0 To ns
                Array.Resize(x(i), nc)
                Array.Resize(y(i), nc)
                Array.Resize(fc(i), nc)
                Array.Resize(z(i), nc)
                Array.Resize(Kval(i), nc)
            Next

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 2. Pressure profile
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            If Not Double.IsNaN(ColumnPressureDrop) Then
                For i = 1 To ns
                    Stages(i).P = Stages(0).P + Convert.ToDouble(i) / Convert.ToDouble(ns) * ColumnPressureDrop
                Next
            End If
            i = 0
            For Each st As Stage In Me.Stages
                P(i) = st.P
                i += 1
            Next

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 3. Collect feed data from connected streams
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim sumcf(nc - 1), sumF, zm(nc - 1) As Double
            Dim firstF As Integer = -1
            Dim lastF As Integer = -1

            For Each ms As StreamInformation In Me.MaterialStreams.Values
                Select Case ms.StreamBehavior
                    Case StreamInformation.Behavior.Feed
                        Dim stream As MaterialStream = FlowSheet.SimulationObjects(ms.StreamID)
                        pp.CurrentMaterialStream = stream
                        Dim si = StageIndex(ms.AssociatedStage)
                        F(si) = stream.Phases(0).Properties.molarflow.GetValueOrDefault
                        HF(si) = stream.Phases(0).Properties.enthalpy.GetValueOrDefault *
                                 stream.Phases(0).Properties.molecularWeight.GetValueOrDefault
                        FT(si) = stream.Phases(0).Properties.temperature.GetValueOrDefault
                        sumF += F(si)
                        j = 0
                        For Each comp As Thermodynamics.BaseClasses.Compound In stream.Phases(0).Compounds.Values
                            fc(si)(j) = comp.MoleFraction.GetValueOrDefault
                            z(si)(j) = comp.MoleFraction.GetValueOrDefault
                            sumcf(j) += comp.MoleFraction.GetValueOrDefault * F(si)
                            j += 1
                        Next
                    Case StreamInformation.Behavior.Sidedraw
                        If ms.StreamPhase = StreamInformation.Phase.V Then
                            VSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        Else
                            LSS(StageIndex(ms.AssociatedStage)) = ms.FlowRate.Value
                        End If
                    Case StreamInformation.Behavior.InterExchanger
                        Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End Select
            Next
            For Each ms As StreamInformation In Me.EnergyStreams.Values
                If ms.StreamBehavior = StreamInformation.Behavior.InterExchanger Then
                    Q(StageIndex(ms.AssociatedStage)) = -DirectCast(FlowSheet.SimulationObjects(ms.StreamID), Streams.EnergyStream).EnergyFlow.GetValueOrDefault
                End If
            Next

            vaprate = SystemsOfUnits.Converter.ConvertToSI(Me.VaporFlowRateUnit, Me.VaporFlowRate)

            'firstF/lastF are the topmost and bottom-most fed stages. Both are found
            'by scanning the stages: taking firstF from the order the feeds happen to
            'sit in MaterialStreams makes the initial liquid traffic depend on the
            'order they were connected in, which for an absorber fed liquid at the top
            'and gas at the bottom can start L off at the gas rate.
            For i = 0 To ns
                If F(i) <> 0 Then
                    firstF = i
                    Exit For
                End If
            Next

            For i = ns To 0 Step -1
                If F(i) <> 0 Then
                    lastF = i
                    Exit For
                End If
            Next

            For i = 0 To nc - 1
                zm(i) = sumcf(i) / Math.Max(sumF, 1.0E-20)
            Next

            Dim mwf = pp.AUX_MMM(zm)
            Dim Vprops = pp.DW_GetConstantProperties()

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 4. Cumulative feed / sidestream balance
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim sum1(ns), sum0_ As Double
            sum0_ = 0
            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
                sum0_ += LSS(i) + VSS(i)
            Next

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 5. Reference flash at feed conditions for K-values and alpha
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim Tref As Double = FT.Where(Function(ti) ti > 0).Average
            Dim Pref As Double = Stages.Select(Function(s) s.P).Average

            Dim feedFlash As Object() = pp.FlashBase.Flash_PT(zm, Pref, Tref, pp)
            Dim Kref = feedFlash(9)

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 6. IMPROVED: reflux ratio from relative volatility (Underwood-simplified)
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            rr = 2.5
            If TypeOf Me Is DistillationColumn Then
                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                    rr = 3.0
                Else
                    If Me.Specs("C").SType = ColumnSpec.SpecType.Stream_Ratio Then
                        rr = Me.Specs("C").SpecValue
                    Else
                        ' Sort components by K-value; alpha = K_lightest / K_2nd-lightest
                        ' rr â‰ˆ alpha / (alpha - 1), then scale by Gilliland factor 1.3
                        If nc >= 2 Then
                            Dim sortedK = Kref.Select(Function(k, idx) New With {.K = k, .Idx = idx}).
                                              OrderByDescending(Function(e) e.K).ToArray()
                            Dim K1 = Math.Max(sortedK(0).K, 0.0000000001)
                            Dim K2 = Math.Max(sortedK(1).K, 0.0000000001)
                            Dim alpha_lk As Double = K1 / K2
                            Dim rr_min As Double
                            If alpha_lk > 1.05 Then
                                rr_min = alpha_lk / (alpha_lk - 1.0)
                            Else
                                rr_min = 10.0  ' near-azeotrope: use high reflux
                            End If
                            rr = Math.Min(1.3 * rr_min, 20.0)
                            rr = Math.Max(rr, 1.2)
                        End If
                    End If
                End If
            End If

            If InitialEstimates.RefluxRatio IsNot Nothing And
               UseVaporFlowEstimates And UseLiquidFlowEstimates Then
                rr = InitialEstimates.RefluxRatio
            End If

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 7. Product flow rate estimates from column specifications
            '    (identical logic to GetSolverInputData)
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim distVx(nc - 1), rebVx(nc - 1), distVy(nc - 1), rebVy(nc - 1) As Double
            Dim hamount As Double = 0.0

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    rebVx(cindex) = cvalue * zm(cindex) * sumF
                    hamount = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cunits = Specs("R").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("R").ComponentID
                    Dim cvalue = Specs("R").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    hamount = camount
                    rebVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) < Kref(cindex) Then
                            hamount += sumF * zm(i)
                            rebVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            rebVx(i) = 0.0
                        End If
                    Next
                    rebVx = rebVx.NormalizeY()
                Case ColumnSpec.SpecType.Product_Mass_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) / mwf * 1000 - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Product_Molar_Flow_Rate
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - SystemsOfUnits.Converter.ConvertToSI(Me.Specs("R").SpecUnit, Me.Specs("R").SpecValue) - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case ColumnSpec.SpecType.Feed_Recovery
                    Dim cvalue = Specs("R").SpecValue / 100.0
                    Dim pval = sumF * cvalue
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = sumF - pval - sum0_
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - pval - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - pval - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - pval - sum0_
                            vaprate = 0.0
                        End If
                    End If
                Case Else
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF / 2 - sum0_
                        Else
                            distrate = sumF / 2 - sum0_ - vaprate
                        End If
                    End If
            End Select

            Select Case Specs("R").SType
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate,
                     ColumnSpec.SpecType.Component_Molar_Flow_Rate,
                     ColumnSpec.SpecType.Component_Recovery,
                     ColumnSpec.SpecType.Component_Fraction
                    If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        vaprate = (sumF - sum0_) / 2
                        distrate = 0.0
                    Else
                        If Me.CondenserType = condtype.Full_Reflux Then
                            vaprate = sumF - hamount - sum0_
                            distrate = 0.0
                        ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                            If Me.Specs("C").SType = ColumnSpec.SpecType.Product_Molar_Flow_Rate Then
                                distrate = SystemsOfUnits.Converter.ConvertToSI(Me.Specs("C").SpecUnit, Me.Specs("C").SpecValue)
                            Else
                                distrate = sumF - hamount - sum0_ - vaprate
                            End If
                        Else
                            distrate = sumF - hamount - sum0_
                            vaprate = 0.0
                        End If
                    End If
            End Select

            If InitialEstimates.VaporProductFlowRate IsNot Nothing And UseVaporFlowEstimates And Not ignoreuserestimates Then
                vaprate = InitialEstimates.VaporProductFlowRate
            End If
            If InitialEstimates.DistillateFlowRate IsNot Nothing And UseLiquidFlowEstimates And Not ignoreuserestimates Then
                distrate = InitialEstimates.DistillateFlowRate
            End If

            If TypeOf Me Is DistillationColumn AndAlso DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                distrate = 0.0
            Else
                If Me.CondenserType = condtype.Full_Reflux Then
                    distrate = 0.0
                ElseIf Me.CondenserType = condtype.Partial_Condenser Then
                Else
                    vaprate = 0.0
                End If
            End If

            ' distVx from condenser spec
            Dim lamount As Double = 0.0
            Select Case Specs("C").SType
                Case ColumnSpec.SpecType.Component_Fraction
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    lamount = cvalue * zm(cindex) * sumF
                    distVx(cindex) = cvalue * zm(cindex) * sumF
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Mass_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits) / Vprops(cindex).Molar_Weight * 1000
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Molar_Flow_Rate
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cunits = Specs("C").SpecUnit
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = cvalue.ConvertToSI(cunits)
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
                Case ColumnSpec.SpecType.Component_Recovery
                    Dim cname = Specs("C").ComponentID
                    Dim cvalue = Specs("C").SpecValue
                    Dim cindex = Vn.IndexOf(cname)
                    Dim camount = sumF * zm(cindex) * cvalue / 100
                    lamount = camount
                    distVx(cindex) = camount
                    For i = 0 To nc - 1
                        If Kref(i) > Kref(cindex) Then
                            lamount += sumF * zm(i)
                            distVx(i) = sumF * zm(i)
                        ElseIf i <> cindex Then
                            distVx(i) = 0.0
                        End If
                    Next
                    distVx = distVx.NormalizeY()
            End Select

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 8. IMPROVED: condenser (T1) and reboiler (T2) temperature estimates
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim T1, T2 As Double
            Dim xtop(nc - 1), ytop(nc - 1), xbot(nc - 1), ybot(nc - 1) As Double

            Select Case Me.ColumnType

                Case ColType.AbsorptionColumn
                    ' IMPROVED: use first/last non-zero feed temperature (not FT(0)/FT(ns))
                    T1 = FT.FirstOrDefault(Function(tx) tx > 0)
                    T2 = FT.LastOrDefault(Function(tx) tx > 0)
                    If T1 = 0.0 Then Throw New Exception("The absorber needs at least one feed stream connected.")
                    If T2 = 0.0 Then T2 = T1

                Case ColType.ReboiledAbsorber
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1

                Case ColType.RefluxedAbsorber
                    P(0) -= CondenserDeltaP
                    T1 = MathEx.Common.WgtAvg(F, FT)
                    T2 = T1

                Case ColType.DistillationColumn
                    If Not DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                        P(0) -= CondenserDeltaP
                    End If

                    ' T1: condenser temperature
                    Try
                        IObj?.SetCurrent()
                        If distVx.Sum > 0 Then
                            ' Component spec available: bubble-point of estimated distillate
                            Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0, distVx, Nothing, Tref)
                            T1 = fcalc.CalculatedTemperature
                            ' IMPROVED: ytop from same result (not from a separate fflash)
                            Dim safeK = fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()
                            distVy = distVx.MultiplyY(safeK).NormalizeY()
                            xtop = distVx.Clone()
                            ytop = distVy.Clone()
                        Else
                            If Specs("C").SType = ColumnSpec.SpecType.Temperature Then
                                T1 = Specs("C").SpecValue.ConvertToSI(Specs("C").SpecUnit)
                            Else
                                ' Bubble-point of overall feed composition at condenser pressure
                                T1 = pp.DW_CalcBubT(zm, P(0), FT.MinY_NonZero())(4)
                            End If
                            ' xtop/ytop from flash at T1
                            Dim topFlash = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(0), 0.1, zm, Nothing, T1)
                            xtop = topFlash.GetLiquidPhase1MoleFractions()
                            ytop = topFlash.GetVaporPhaseMoleFractions()
                            If ytop Is Nothing OrElse ytop.Sum = 0 Then ytop = xtop.Clone()
                        End If
                    Catch
                        T1 = FT.Where(Function(t_) t_ > 0.0).Min
                        xtop = zm.Clone()
                        ytop = zm.Clone()
                    End Try

                    ' T2: reboiler temperature
                    Try
                        IObj?.SetCurrent()
                        If rebVx.Sum > 0 Then
                            ' Component spec available: bubble-point of estimated bottoms
                            Dim fcalc = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(ns), 0, rebVx, Nothing, Tref)
                            T2 = fcalc.CalculatedTemperature
                            Dim safeK = fcalc.Kvalues.Select(Function(k) Convert.ToDouble(IIf(Double.IsNaN(k), 0.0, k))).ToArray()
                            rebVy = rebVx.MultiplyY(safeK).NormalizeY()
                            xbot = rebVx.Clone()
                            ybot = rebVy.Clone()
                        Else
                            If Specs("R").SType = ColumnSpec.SpecType.Temperature Then
                                T2 = Specs("R").SpecValue.ConvertToSI(Specs("R").SpecUnit)
                            Else
                                ' Dew-point of overall feed composition at reboiler pressure
                                T2 = pp.DW_CalcDewT(zm, P(ns), FT.Max)(4)
                            End If
                            Dim botFlash = pp.CalculateEquilibrium(FlashCalculationType.PressureVaporFraction, P(ns), 0.9, zm, Nothing, T2)
                            xbot = botFlash.GetLiquidPhase1MoleFractions()
                            ybot = botFlash.GetVaporPhaseMoleFractions()
                            If xbot Is Nothing OrElse xbot.Sum = 0 Then xbot = zm.Clone()
                        End If
                    Catch
                        T2 = FT.Where(Function(t_) t_ > 0.0).Max
                        xbot = zm.Clone()
                        ybot = zm.Clone()
                    End Try

            End Select

            ' Assign distillate side-draw
            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    LSS(0) = distrate
                Case ColType.RefluxedAbsorber
                    LSS(0) = distrate
            End Select

            ' Recompute sum1 after LSS(0) is set
            For i = 0 To ns
                sum1(i) = 0
                For j = 0 To i
                    sum1(i) += F(j) - LSS(j) - VSS(j)
                Next
            Next

            ' Clone pp stream for calculations
            pp.CurrentMaterialStream = pp.CurrentMaterialStream.Clone()
            pp.CurrentMaterialStream.SetPropertyPackageObject(pp)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).SetFlowsheet(FlowSheet)
            DirectCast(pp.CurrentMaterialStream, MaterialStream).PreferredFlashAlgorithmTag = Me.PreferredFlashAlgorithmTag

            T(0) = T1
            T(ns) = T2

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 9. Stage profiles: T, V, L, x, y, K
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            compids = New ArrayList
            For Each compName As String In Vn
                compids.Add(compName)
            Next

            ' Minimum physical flow to prevent zeros/negatives
            Dim minFlow As Double = Math.Max(sumF * 0.001, 0.00000001)

            Dim needsXYestimates As Boolean = False

            i = 0
            For Each st As Stage In Me.Stages
                eff(i) = st.Efficiency

                ' Temperature profile: linear interpolation between T1 and T2
                If Me.UseTemperatureEstimates And InitialEstimates.ValidateTemperatures() And Not ignoreuserestimates Then
                    T(i) = Me.InitialEstimates.StageTemps(i).Value
                Else
                    T(i) = T1 + (T2 - T1) * Convert.ToDouble(i) / Convert.ToDouble(ns)
                End If

                ' Vapor flow profile
                If Me.UseVaporFlowEstimates And InitialEstimates.ValidateVaporFlows() And Not ignoreuserestimates Then
                    V(i) = Me.InitialEstimates.VapMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    V(0) = Math.Max(vaprate, minFlow)
                                Else
                                    If Me.CondenserType = condtype.Total_Condenser Then
                                        V(0) = minFlow
                                    Else
                                        V(0) = Math.Max(vaprate, minFlow)
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Total_Condenser Then
                                    V(0) = minFlow
                                Else
                                    V(0) = Math.Max(vaprate, minFlow)
                                End If
                            Case Else
                                V(0) = Math.Max(If(lastF >= 0, F(lastF), sumF * 0.5), minFlow)
                        End Select
                    Else
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                Dim Vcalc As Double
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    Vcalc = (rr + 1) * V(0)
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        Vcalc = (rr + 1) * (distrate + vaprate)
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        Vcalc = (rr + 1) * V(0)
                                    Else
                                        Vcalc = (rr + 1) * distrate
                                    End If
                                End If
                                V(i) = Math.Max(Vcalc, minFlow)
                            Case ColType.RefluxedAbsorber
                                V(i) = Math.Max((rr + 1) * distrate + V(0), minFlow)
                            Case ColType.AbsorptionColumn, ColType.ReboiledAbsorber
                                V(i) = Math.Max(If(lastF >= 0, F(lastF), sumF * 0.5), minFlow)
                        End Select
                    End If
                End If

                ' Liquid flow profile
                If Me.UseLiquidFlowEstimates And InitialEstimates.ValidateLiquidFlows() And Not ignoreuserestimates Then
                    L(i) = Me.InitialEstimates.LiqMolarFlows(i).Value
                Else
                    If i = 0 Then
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If DirectCast(Me, DistillationColumn).ReboiledAbsorber Then
                                    L(0) = Math.Max(vaprate * rr, minFlow)
                                Else
                                    If Me.CondenserType = condtype.Partial_Condenser Then
                                        L(0) = Math.Max((distrate + vaprate) * rr, minFlow)
                                    ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                        L(0) = Math.Max(vaprate * rr, minFlow)
                                    Else
                                        L(0) = Math.Max(distrate * rr, minFlow)
                                    End If
                                End If
                            Case ColType.RefluxedAbsorber
                                If Me.CondenserType = condtype.Partial_Condenser Then
                                    L(0) = Math.Max(distrate * rr, minFlow)
                                ElseIf Me.CondenserType = condtype.Full_Reflux Then
                                    L(0) = Math.Max(vaprate * rr, minFlow)
                                Else
                                    L(0) = Math.Max(distrate * rr, minFlow)
                                End If
                            Case Else  ' AbsorptionColumn, ReboiledAbsorber
                                L(0) = Math.Max(If(firstF >= 0, F(firstF), sumF * 0.5), minFlow)
                        End Select
                    Else
                        ' IMPROVED: proper cases for all column types + clamping
                        Dim Lcalc As Double
                        Select Case Me.ColumnType
                            Case ColType.DistillationColumn
                                If i < ns Then
                                    Lcalc = V(i) + sum1(i) - V(0)
                                Else
                                    Lcalc = sum1(i) - V(0)
                                End If
                            Case ColType.RefluxedAbsorber
                                ' Same material-balance formula as DistillationColumn
                                If i < ns Then
                                    Lcalc = V(i) + sum1(i) - V(0)
                                Else
                                    Lcalc = sum1(i) - V(0)
                                End If
                            Case ColType.ReboiledAbsorber
                                ' No overhead product; V(0) = top vapor leaving the column
                                If i < ns Then
                                    Lcalc = V(i) + sum1(i) - V(0)
                                Else
                                    Lcalc = sum1(i) - V(0)
                                End If
                            Case ColType.AbsorptionColumn
                                Lcalc = If(firstF >= 0, F(firstF), sumF * 0.5)
                            Case Else
                                Lcalc = minFlow
                        End Select
                        L(i) = Math.Max(Lcalc, minFlow)
                    End If
                End If

                ' Composition and K-value estimates
                If Me.UseCompositionEstimates And InitialEstimates.ValidateCompositions() And Not ignoreuserestimates Then
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.LiqCompositions(i).Values
                        x(i)(j) = par.Value
                        j += 1
                    Next
                    j = 0
                    For Each par As Parameter In Me.InitialEstimates.VapCompositions(i).Values
                        y(i)(j) = par.Value
                        j += 1
                    Next
                    z(i) = zm
                    If pp.ShouldUseKvalueMethod3 Then
                        Kval(i) = pp.DW_CalcKvalue3(x(i).MultiplyConstY(L(i)), y(i).MultiplyConstY(V(i)), T(i), P(i))
                    ElseIf pp.ShouldUseKvalueMethod2 Then
                        Kval(i) = pp.DW_CalcKvalue(x(i).MultiplyConstY(L(i)).AddY(y(i).MultiplyConstY(V(i))), T(i), P(i))
                    Else
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    End If
                Else
                    IObj?.SetCurrent()
                    z(i) = zm
                    If rebVx.Sum > 0 And distVx.Sum > 0 Then
                        ' Component specs available: interpolate between product compositions.
                        ' Use section-aware interpolation: above vs. below the main feed stage.
                        Dim frac As Double = Convert.ToDouble(i) / Convert.ToDouble(ns)
                        For j = 0 To nc - 1
                            x(i)(j) = distVx(j) + frac * (rebVx(j) - distVx(j))
                            y(i)(j) = distVy(j) + frac * (rebVy(j) - distVy(j))
                        Next
                        x(i) = x(i).NormalizeY()
                        y(i) = y(i).NormalizeY()
                        Kval(i) = pp.DW_CalcKvalue(x(i), y(i), T(i), P(i))
                    Else
                        ' No component specs: estimate via ideal K-values.
                        If pp.ShouldUseKvalueMethod3 Then
                            Kval(i) = pp.DW_CalcKvalue(zm, T(i), P(i))
                        ElseIf pp.ShouldUseKvalueMethod2 Then
                            Kval(i) = pp.DW_CalcKvalue(zm, T(i), P(i))
                        Else
                            Kval(i) = pp.DW_CalcKvalue_Ideal_Wilson(T(i), P(i))
                        End If
                        If ColumnType = ColType.AbsorptionColumn Then
                            For j = 0 To nc - 1
                                x(i)(j) = (L(i) + V(i)) * z(i)(j) / (L(i) + V(i) * Math.Max(Kval(i)(j), 0.0000000001))
                                y(i)(j) = Kval(i)(j) * x(i)(j)
                            Next
                            x(i) = x(i).NormalizeY()
                            y(i) = y(i).NormalizeY()
                        Else
                            needsXYestimates = True
                        End If
                    End If
                    If llextractor And pp.AUX_CheckTrivial(Kval(i)) Then
                        Throw New Exception("Your column is configured as a Liquid-Liquid Extractor, but the Property Package / Flash Algorithm set associated with the column is unable to generate an initial estimate for two liquid phases. Please select a different set or change the Flash Algorithm's Stability Analysis parameters and try again.")
                    End If
                End If

                i += 1
            Next

            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    Q(0) = 0
                    Q(ns) = 0
                Case ColType.ReboiledAbsorber
                    Q(ns) = 0
                Case ColType.RefluxedAbsorber
                    Q(0) = 0
            End Select

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 10. IMPROVED: needsXYestimates â†’ per-stage PT flash with accumulated
            '     feed composition (richer in lights near top, heavies near bottom)
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim L1trials, L2trials As New List(Of Double())
            Dim x1trials, x2trials As New List(Of Double()())

            If Not llextractor Then

                If needsXYestimates Then

                    Dim sumLSS = LSS.Sum
                    Dim sumVSS = VSS.Sum
                    VSS(0) = 0
                    LSS(ns) = 0

                    ' Stage-by-stage flash using the accumulated feed composition from
                    ' the top down; this naturally enriches lighter components near the
                    ' top and heavier ones near the bottom without hard-coding anything.
                    Dim zAccum(nc - 1) As Double
                    Dim fAccum As Double = 0
                    For i = 0 To ns
                        If F(i) > 0 Then
                            For j = 0 To nc - 1
                                zAccum(j) = (zAccum(j) * fAccum + fc(i)(j) * F(i)) / (fAccum + F(i))
                            Next
                            fAccum += F(i)
                        End If
                        Dim zLocal = If(fAccum > 0, zAccum.Clone(), zm.Clone())
                        Try
                            Dim sflash As Object() = pp.FlashBase.Flash_PT(zLocal, P(i), T(i), pp)
                            x(i) = sflash(2)
                            y(i) = sflash(3)
                            Kval(i) = sflash(9)
                        Catch
                            x(i) = zLocal.Clone()
                            y(i) = zLocal.Clone()
                        End Try
                    Next

                End If

            Else

                ' LLE extractor (unchanged from original)
                If Not UseCompositionEstimates Or Not UseLiquidFlowEstimates Or Not UseVaporFlowEstimates Then

                    Dim L1, L2 As Double
                    Dim Vx1(), Vx2() As Double
                    Dim trialcomp As Double() = zm.Clone
                    For counter As Integer = 0 To 100
                        Dim flashresult = pp.FlashBase.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                L1t.Add(If(UseLiquidFlowEstimates, L(i), F.Sum * L1))
                                L2t.Add(If(UseVaporFlowEstimates, V(i), F.Sum * L2))
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i).Clone)
                                    xt2.Add(y(i).Clone)
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                    trialcomp = zm.Clone
                    Dim lle As New PropertyPackages.Auxiliary.FlashAlgorithms.SimpleLLE()
                    For counter As Integer = 0 To 100
                        Dim flashresult = lle.Flash_PT(trialcomp, P.Average, T.Average, pp)
                        L1 = flashresult(0)
                        L2 = flashresult(5)
                        Vx1 = flashresult(2)
                        Vx2 = flashresult(6)
                        If L2 > 0.0 And Vx1.SubtractY(Vx2).AbsSqrSumY > 0.001 Then
                            Dim L1t, L2t As New List(Of Double)
                            Dim xt1, xt2 As New List(Of Double())
                            For i = 0 To Stages.Count - 1
                                L1t.Add(If(UseLiquidFlowEstimates, L(i), F.Sum * L1))
                                L2t.Add(If(UseVaporFlowEstimates, V(i), F.Sum * L2))
                                If UseCompositionEstimates Then
                                    xt1.Add(x(i))
                                    xt2.Add(y(i))
                                Else
                                    xt1.Add(Vx1)
                                    xt2.Add(Vx2)
                                End If
                            Next
                            L1trials.Add(L1t.ToArray())
                            L2trials.Add(L2t.ToArray())
                            x1trials.Add(xt1.ToArray())
                            x2trials.Add(xt2.ToArray())
                        End If
                        Dim rnd As New Random(counter)
                        trialcomp = Enumerable.Repeat(0, nc).Select(Function(d) rnd.NextDouble()).ToArray
                        trialcomp = trialcomp.NormalizeY
                    Next

                Else

                    Dim L1t, L2t As New List(Of Double)
                    Dim xt1, xt2 As New List(Of Double())
                    For i = 0 To Stages.Count - 1
                        L1t.Add(L(i))
                        L2t.Add(V(i))
                        xt1.Add(x(i).Clone)
                        xt2.Add(y(i).Clone)
                    Next
                    L1trials.Add(L1t.ToArray())
                    L2trials.Add(L2t.ToArray())
                    x1trials.Add(xt1.ToArray())
                    x2trials.Add(xt2.ToArray())

                End If

            End If

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 11. Process spec component indices and legacy stage-number fixup
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            For Each sp As Auxiliary.SepOps.ColumnSpec In Me.Specs.Values
                If sp.SType = ColumnSpec.SpecType.Component_Fraction Or
                   sp.SType = ColumnSpec.SpecType.Component_Mass_Flow_Rate Or
                   sp.SType = ColumnSpec.SpecType.Component_Molar_Flow_Rate Or
                   sp.SType = ColumnSpec.SpecType.Component_Recovery Then
                    sp.ComponentIndex = Vn.IndexOf(sp.ComponentID)
                End If
                If sp.StageNumber = -1 And sp.SpecValue = Me.DistillateFlowRate Then
                    Dim sumF2 As Double = 0, sumLSS2 As Double = 0, sumVSS2 As Double = 0
                    For i = 0 To ns
                        sumF2 += F(i)
                        sumLSS2 += LSS(i)
                        sumVSS2 += VSS(i)
                    Next
                    sp.SpecValue = sumF2 - sumLSS2 - sumVSS2 - V(0)
                    sp.StageNumber = 0
                End If
            Next

            IObj?.Close()

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 12. Tridiagonal refinement for distillation columns (from _New)
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            If Me.ColumnType = ColType.DistillationColumn Then
                Try
                    Dim tridiag = WangHenkeMethod2.RunTridiagonal(Me, F, V, Q, L, HF, VSS, LSS, Kval, x, y, z, fc,
                                                                  T, P, CondenserType, ns, nc, ColumnType, PropertyPackage, Specs)
                    V = tridiag(1)
                    L = tridiag(2)
                    y = tridiag(5)
                    x = tridiag(6)

                    ' Re-clamp after tridiagonal in case it produced non-physical values
                    For i = 0 To ns
                        V(i) = Math.Max(V(i), minFlow)
                        L(i) = Math.Max(L(i), minFlow)
                    Next
                Catch
                    ' Tridiagonal failed; proceed with the analytically computed profile
                End Try
            End If

            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            ' 13. Build and return solver input
            'â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            Dim solverinput As New ColumnSolverInputData
            With solverinput
                .ColumnObject = Me
                .StageTemperatures = T.ToList
                .StagePressures = P.ToList
                .StageHeats = Q.ToList
                .StageEfficiencies = eff.ToList
                .NumberOfCompounds = nc
                .NumberOfStages = ns
                .ColumnType = ColumnType
                .CondenserSpec = Specs("C")
                .ReboilerSpec = Specs("R")
                .CondenserType = CondenserType
                .FeedCompositions = fc.ToList
                .FeedEnthalpies = HF.ToList
                .FeedFlows = F.ToList
                .VaporCompositions = y.ToList
                .VaporFlows = V.ToList
                .VaporSideDraws = VSS.ToList
                .LiquidCompositions = x.ToList
                .LiquidFlows = L.ToList
                .LiquidSideDraws = LSS.ToList
                .Kvalues = Kval.ToList()
                .MaximumIterations = maxits
                .Tolerances = tol.ToList
                .OverallCompositions = z.ToList
                .L1trials = L1trials
                .L2trials = L2trials
                .x1trials = x1trials
                .x2trials = x2trials
                If TypeOf Me Is DistillationColumn Then
                    .SubcoolingDeltaT = DirectCast(Me, DistillationColumn).TotalCondenserSubcoolingDeltaT
                End If
            End With

            Return solverinput

        End Function

        Public Sub TestConvergence()

            Calculate("TestConvergence")

        End Sub

        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            ColumnPropertiesProfile = ""

            Dim inputdata As ColumnSolverInputData

            If InitialEstimatesProvider <> "" AndAlso Column.ExternalInitialEstimatesProviders.ContainsKey(InitialEstimatesProvider) Then
                inputdata = Column.ExternalInitialEstimatesProviders(InitialEstimatesProvider).GetInitialEstimates(Me)
            ElseIf InitialEstimatesProvider = "Internal 2 (Experimental)" Then
                inputdata = GetSolverInputData_New()
            ElseIf InitialEstimatesProvider = "Internal 3 (Robust)" Then
                inputdata = GetSolverInputData_Robust()
            Else
                inputdata = GetSolverInputData()
            End If

            Dim i, j As Integer
            Dim ns = inputdata.NumberOfStages

            'store initial values

            x0.Clear()
            y0.Clear()
            K0.Clear()
            For i = 0 To ns
                x0.Add(inputdata.LiquidCompositions(i))
                y0.Add(inputdata.VaporCompositions(i))
                K0.Add(inputdata.Kvalues(i))
            Next
            T0 = inputdata.StageTemperatures.ToArray()
            P0 = inputdata.StagePressures.ToArray()
            V0 = inputdata.VaporFlows.ToArray()
            L0 = inputdata.LiquidFlows.ToArray()
            VSS0 = inputdata.VaporSideDraws.ToArray()
            LSS0 = inputdata.LiquidSideDraws.ToArray()

            Dim nc = inputdata.NumberOfCompounds
            Dim maxits = inputdata.MaximumIterations
            Dim tol = inputdata.Tolerances.ToArray()
            Dim F = inputdata.FeedFlows.ToArray()
            Dim V = inputdata.VaporFlows.ToArray()
            Dim L = inputdata.LiquidFlows.ToArray()
            Dim VSS = inputdata.VaporSideDraws.ToArray()
            Dim LSS = inputdata.LiquidSideDraws.ToArray()
            Dim Kval = inputdata.Kvalues.ToArray()
            Dim Q = inputdata.StageHeats.ToArray()
            Dim x = inputdata.LiquidCompositions.ToArray()
            Dim y = inputdata.VaporCompositions.ToArray()
            Dim z = inputdata.OverallCompositions.ToArray()
            Dim fc = inputdata.FeedCompositions.ToArray()
            Dim HF = inputdata.FeedEnthalpies.ToArray()
            Dim T = inputdata.StageTemperatures.ToArray()
            Dim P = inputdata.StagePressures.ToArray()
            Dim eff = inputdata.StageEfficiencies.ToArray()

            Dim pp = DirectCast(PropertyPackage, PropertyPackages.PropertyPackage)

            Dim L1trials = inputdata.L1trials
            Dim L2trials = inputdata.L2trials
            Dim x1trials = inputdata.x1trials
            Dim x2trials = inputdata.x2trials

            Dim llextractor As Boolean = False
            Dim myabs As AbsorptionColumn = TryCast(Me, AbsorptionColumn)
            If myabs IsNot Nothing Then
                If CType(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Absorber Then
                    llextractor = False
                Else
                    llextractor = True
                End If
            End If

            Dim so As ColumnSolverOutputData = Nothing

            If TypeOf Me Is DistillationColumn Then
                Dim solvererror = True
                If SolvingMethodName.Contains("Modified") Then
                    Try
                        SetColumnSolver(New SolvingMethods.WangHenkeMethod2())
                        so = Solver.SolveColumn(inputdata)
                        solvererror = False
                    Catch ex As Exception
                    End Try
                    If solvererror Then
                        FlowSheet.ShowMessage(GraphicObject.Tag + ": Column Solver did not converge. Will reset some parameters and try again shortly...", IFlowsheet.MessageType.Warning)
                        'try to solve with auto-generated initial estimates.
                        SetColumnSolver(New SolvingMethods.WangHenkeMethod2())
                        so = Solver.SolveColumn(GetSolverInputData(True))
                    End If
                ElseIf SolvingMethodName.Contains("Bubble") Then
                    Try
                        SetColumnSolver(New SolvingMethods.WangHenkeMethod())
                        so = Solver.SolveColumn(inputdata)
                        solvererror = False
                    Catch ex As Exception
                    End Try
                    If solvererror Then
                        FlowSheet.ShowMessage(GraphicObject.Tag + ": Column Solver did not converge. Will reset some parameters and try again shortly...", IFlowsheet.MessageType.Warning)
                        'try to solve with auto-generated initial estimates.
                        SetColumnSolver(New SolvingMethods.WangHenkeMethod())
                        so = Solver.SolveColumn(GetSolverInputData(True))
                    End If
                ElseIf SolvingMethodName.Contains("Napthali") Then
                    Try
                        inputdata.CalculationMode = 0
                        SetColumnSolver(New SolvingMethods.NaphtaliSandholmMethod())
                        so = Solver.SolveColumn(inputdata)
                        solvererror = False
                    Catch oex As OperationCanceledException
                        Throw oex
                    Catch ex As Exception
                    End Try
                    If solvererror Then
                        FlowSheet.ShowMessage(GraphicObject.Tag + ": the column did not converge. DWSIM will try again with a different solver configuration...", IFlowsheet.MessageType.Warning)
                        'try to solve with auto-generated initial estimates.
                        inputdata.CalculationMode = 0
                        SetColumnSolver(New SolvingMethods.NaphtaliSandholmMethod())
                        so = Solver.SolveColumn(GetSolverInputData(True))
                    End If
                Else
                    If Column.ExternalColumnSolvers.ContainsKey(SolvingMethodName) Then
                        so = Column.ExternalColumnSolvers(SolvingMethodName).SolveColumn(Me, inputdata)
                    Else
                        Throw New Exception($"Unable to find column solver with name '{SolvingMethodName}'.")
                    End If
                End If
            ElseIf TypeOf Me Is AbsorptionColumn Then
                If SolvingMethodName.Contains("Rates") Then
                    SetColumnSolver(New SolvingMethods.BurninghamOttoMethod())
                Else
                    SetColumnSolver(New SolvingMethods.NaphtaliSandholmMethod())
                End If
                If llextractor Then
                    If L1trials.Count = 0 Then
                        Throw New Exception("Unable to find a initial LLE estimate to solve the column.")
                    End If
                    'run all trial compositions until it solves
                    Dim ntrials = L1trials.Count
                    Dim ex0 As Exception = Nothing
                    For i = 0 To ntrials - 1
                        Try
                            For j = 0 To Stages.Count - 1
                                For k = 0 To nc - 1
                                    If x1trials(i)(j)(k) = 0.0 Then
                                        Kval(j)(k) = 1.0E+20
                                    Else
                                        Kval(j)(k) = (x2trials(i)(j)(k) / x1trials(i)(j)(k))
                                    End If
                                Next
                            Next
                            inputdata.VaporFlows = L2trials(i).ToList()
                            inputdata.LiquidFlows = L1trials(i).ToList()
                            inputdata.Kvalues = Kval.ToList()
                            inputdata.LiquidCompositions = x1trials(i).ToList()
                            inputdata.VaporCompositions = x2trials(i).ToList()
                            so = Solver.SolveColumn(inputdata)
                            ex0 = Nothing
                            Exit For
                        Catch ex As Exception
                            'do nothing, try next set
                            ex0 = ex
                        End Try
                    Next
                    If ex0 IsNot Nothing Then Throw ex0
                Else
                    Dim solvererror = True
                    If SolvingMethodName.Contains("Rates") Then
                        Try
                            Auxiliary.SepOps.SolvingMethods.BurninghamOttoMethod.RelaxTemperatureUpdates = False
                            Auxiliary.SepOps.SolvingMethods.BurninghamOttoMethod.RelaxCompositionUpdates = False
                            so = Solver.SolveColumn(inputdata)
                            solvererror = False
                        Catch ex As Exception
                        End Try
                        If solvererror Then
                            FlowSheet.ShowMessage(GraphicObject.Tag + ": Column Solver did not converge. Will reset some parameters and try again shortly...", IFlowsheet.MessageType.Warning)
                            Auxiliary.SepOps.SolvingMethods.BurninghamOttoMethod.RelaxTemperatureUpdates = True
                            Auxiliary.SepOps.SolvingMethods.BurninghamOttoMethod.RelaxCompositionUpdates = True
                            so = Solver.SolveColumn(inputdata)
                        End If
                    Else
                        Try
                            inputdata.CalculationMode = 0
                            so = Solver.SolveColumn(inputdata)
                            solvererror = False
                        Catch oex As OperationCanceledException
                            Throw oex
                        Catch ex As Exception
                        End Try
                        If solvererror Then
                            FlowSheet.ShowMessage(GraphicObject.Tag + ": the column did not converge. DWSIM will try again with a different solver configuration...", IFlowsheet.MessageType.Warning)
                            'try to solve with auto-generated initial estimates.
                            inputdata.CalculationMode = 0
                            so = Solver.SolveColumn(GetSolverInputData(True))
                        End If
                    End If
                End If
            End If

            ic = so.IterationsTaken

            Me.CondenserDuty = so.StageHeats(0)
            Me.ReboilerDuty = so.StageHeats(ns)

            'store final values
            xf.Clear()
            yf.Clear()
            Kf.Clear()
            For i = 0 To ns
                xf.Add(so.LiquidCompositions(i))
                yf.Add(so.VaporCompositions(i))
                x(i) = so.LiquidCompositions(i)
                y(i) = so.VaporCompositions(i)
                Kf.Add(so.Kvalues(i))
                Kval(i) = so.Kvalues(i)
            Next
            Tf = so.StageTemperatures.ToArray()
            Vf = so.VaporFlows.ToArray()
            Lf = so.LiquidFlows.ToArray()
            VSSf = so.VaporSideDraws.ToArray()
            LSSf = so.LiquidSideDraws.ToArray()
            Q = so.StageHeats.ToArray()

            'generate properties profile

            GeneratePropertiesProfileReport()

            'estimate diameter and height

            Dim lt = TraySpacing
            Dim H = (NumberOfStages + 2) * lt

            Dim maxV = Vf.Max()
            Dim maxy As Double() = yf(Vf.ToList().IndexOf(Vf.Max))
            Dim maxL = Lf.Max()
            Dim maxx As Double() = xf(Lf.ToList().IndexOf(Lf.Max))

            Dim ms = New MaterialStream("", "", FlowSheet, pp)
            FlowSheet().AddCompoundsToMaterialStream(ms)
            pp.CurrentMaterialStream = ms

            Dim maxVW = maxV / 1000.0 * pp.AUX_MMM(maxy)
            Dim maxLW = maxL / 1000.0 * pp.AUX_MMM(maxx)

            Dim Tx = Tf.Average()
            Dim Px = P0.Average()
            Dim rhov = pp.AUX_MMM(maxy) / (8.314 * Tx / Px * 1000)
            Dim rhol = pp.AUX_LIQDENS(Tx, maxx, Px)
            Dim uv = (-0.17 * lt ^ 2 + 0.27 * lt - 0.047) * ((rhol - rhov) / rhov) ^ 0.5
            Dim Dc = (4 * maxVW / (Math.PI * rhov * uv)) ^ 0.5

            EstimatedHeight = H
            EstimatedDiameter = Dc

            'if enabled, auto update initial estimates

            If Me.AutoUpdateInitialEstimates Then
                'check if initial estimates are valid
                If Vf.IsValid And Lf.IsValid And LSSf.IsValid And Tf.IsValid Then
                    InitialEstimates = RebuildEstimates()
                    InitialEstimates.VaporProductFlowRate = Vf(0)
                    InitialEstimates.DistillateFlowRate = LSSf(0)
                    InitialEstimates.BottomsFlowRate = Lf(0)
                    For i = 0 To Me.Stages.Count - 1
                        Me.InitialEstimates.StageTemps(i).Value = Tf(i)
                        Me.InitialEstimates.VapMolarFlows(i).Value = Vf(i)
                        Me.InitialEstimates.LiqMolarFlows(i).Value = Lf(i)
                        j = 0
                        For Each par As Parameter In Me.InitialEstimates.LiqCompositions(i).Values
                            par.Value = xf(i)(j)
                            j = j + 1
                        Next
                        j = 0
                        For Each par As Parameter In Me.InitialEstimates.VapCompositions(i).Values
                            par.Value = yf(i)(j)
                            j = j + 1
                        Next
                    Next
                    LastSolution = RebuildEstimates()
                    LastSolution.LoadData(InitialEstimates.SaveData())
                End If
            Else
                If Vf.IsValid And Lf.IsValid And LSSf.IsValid And Tf.IsValid Then
                    LastSolution = RebuildEstimates()
                    LastSolution.VaporProductFlowRate = Vf(0)
                    LastSolution.DistillateFlowRate = LSSf(0)
                    LastSolution.BottomsFlowRate = Lf(0)
                    For i = 0 To Me.Stages.Count - 1
                        Me.LastSolution.StageTemps(i).Value = Tf(i)
                        Me.LastSolution.VapMolarFlows(i).Value = Vf(i)
                        Me.LastSolution.LiqMolarFlows(i).Value = Lf(i)
                        j = 0
                        For Each par As Parameter In Me.LastSolution.LiqCompositions(i).Values
                            par.Value = xf(i)(j)
                            j = j + 1
                        Next
                        j = 0
                        For Each par As Parameter In Me.LastSolution.VapCompositions(i).Values
                            par.Value = yf(i)(j)
                            j = j + 1
                        Next
                    Next
                End If
            End If

            'update stage temperatures

            For i = 0 To Me.Stages.Count - 1
                Me.Stages(i).T = Tf(i)
            Next

            'update reflux ratio

            RefluxRatio = Lf(0) / (LSSf(0) + Vf(0))

            If args Is Nothing Then

                'copy results to output streams

                Dim compound_balances As New Dictionary(Of String, Double)
                Dim compound_feeds As New Dictionary(Of String, Double)
                Dim comps = FlowSheet.SelectedCompounds.Keys.ToList()
                For Each c In comps
                    compound_balances.Add(c, 0.0)
                    compound_feeds.Add(c, 0.0)
                Next

                'product flows

                Dim msm As MaterialStream = Nothing
                Dim sinf As StreamInformation

                For Each sinf In Me.MaterialStreams.Values
                    Select Case sinf.StreamBehavior
                        Case StreamInformation.Behavior.Feed
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    compound_balances(subst.Name) += subst.MolarFlow.GetValueOrDefault()
                                    compound_feeds(subst.Name) += subst.MolarFlow.GetValueOrDefault()
                                Next
                            End With
                        Case StreamInformation.Behavior.Distillate
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                pp.CurrentMaterialStream = msm
                                .Clear()
                                .SpecType = StreamSpec.Pressure_and_Enthalpy
                                .DefinedFlow = FlowSpec.Mass
                                .Phases(0).Properties.massflow = LSSf(0) * pp.AUX_MMM(xf(0)) / 1000
                                .Phases(0).Properties.molarflow = LSSf(0)
                                .Phases(0).Properties.temperature = Tf(0)
                                .Phases(0).Properties.pressure = P(0)
                                .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(xf(0), Tf(0), P(0), PropertyPackages.State.Liquid)
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = xf(0)(i)
                                    compound_balances(subst.Name) -= xf(0)(i) * LSSf(0)
                                    i += 1
                                Next
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MassFraction = pp.AUX_CONVERT_MOL_TO_MASS(xf(0))(i)
                                    i += 1
                                Next
                                .Phases(3).Properties.molarfraction = 1.0
                                .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Liquid1)
                                .AtEquilibrium = True
                            End With
                        Case StreamInformation.Behavior.OverheadVapor
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                pp.CurrentMaterialStream = msm
                                .Clear()
                                .SpecType = StreamSpec.Pressure_and_Enthalpy
                                .DefinedFlow = FlowSpec.Mass
                                .Phases(0).Properties.massflow = Vf(0) * pp.AUX_MMM(yf(0)) / 1000
                                .Phases(0).Properties.temperature = Tf(0)
                                .Phases(0).Properties.pressure = P(0)
                                If llextractor Then
                                    .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(yf(0), Tf(0), P(0), PropertyPackages.State.Liquid)
                                Else
                                    .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(yf(0), Tf(0), P(0), PropertyPackages.State.Vapor)
                                End If
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = yf(0)(i)
                                    compound_balances(subst.Name) -= yf(0)(i) * Vf(0)
                                    i += 1
                                Next
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MassFraction = pp.AUX_CONVERT_MOL_TO_MASS(yf(0))(i)
                                    i += 1
                                Next
                                If llextractor Then
                                    .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Liquid1)
                                    .Phases(3).Properties.molarfraction = 1.0
                                    .Phases(1).Properties.molarfraction = 1.0
                                Else
                                    .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Vapor)
                                    .Phases(2).Properties.molarfraction = 1.0
                                End If
                                .AtEquilibrium = True
                            End With
                        Case StreamInformation.Behavior.BottomsLiquid
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                pp.CurrentMaterialStream = msm
                                .Clear()
                                .SpecType = StreamSpec.Pressure_and_Enthalpy
                                .DefinedFlow = FlowSpec.Mass
                                .Phases(0).Properties.massflow = Lf(ns) * pp.AUX_MMM(xf(ns)) / 1000
                                .Phases(0).Properties.temperature = Tf(ns)
                                .Phases(0).Properties.pressure = P(ns)
                                .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(xf(ns), Tf(ns), P(ns), PropertyPackages.State.Liquid)
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = xf(ns)(i)
                                    compound_balances(subst.Name) -= xf(ns)(i) * Lf(ns)
                                    i += 1
                                Next
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MassFraction = pp.AUX_CONVERT_MOL_TO_MASS(xf(ns))(i)
                                    i += 1
                                Next
                                .Phases(3).Properties.molarfraction = 1.0
                                .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Liquid1)
                                .AtEquilibrium = True
                            End With
                        Case StreamInformation.Behavior.Sidedraw
                            Dim sidx As Integer = StageIndex(sinf.AssociatedStage)
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            If sinf.StreamPhase = StreamInformation.Phase.L Or sinf.StreamPhase = StreamInformation.Phase.B Then
                                With msm
                                    pp.CurrentMaterialStream = msm
                                    .Clear()
                                    .SpecType = StreamSpec.Pressure_and_Enthalpy
                                    .DefinedFlow = FlowSpec.Mass
                                    .Phases(0).Properties.massflow = LSSf(sidx) * pp.AUX_MMM(xf(sidx)) / 1000
                                    .Phases(0).Properties.temperature = Tf(sidx)
                                    .Phases(0).Properties.pressure = P(sidx)
                                    .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(xf(sidx), Tf(sidx), P(sidx), PropertyPackages.State.Liquid)
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MoleFraction = xf(sidx)(i)
                                        compound_balances(subst.Name) -= xf(sidx)(i) * LSSf(sidx)
                                        i += 1
                                    Next
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MassFraction = pp.AUX_CONVERT_MOL_TO_MASS(xf(sidx))(i)
                                        i += 1
                                    Next
                                    .Phases(3).Properties.molarfraction = 1.0
                                    .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Liquid1)
                                    .AtEquilibrium = True
                                End With
                            ElseIf sinf.StreamPhase = StreamInformation.Phase.V Then
                                With msm
                                    pp.CurrentMaterialStream = msm
                                    .Clear()
                                    .SpecType = StreamSpec.Pressure_and_Enthalpy
                                    .DefinedFlow = FlowSpec.Mass
                                    .Phases(0).Properties.massflow = VSSf(sidx) * pp.AUX_MMM(yf(sidx)) / 1000
                                    .Phases(0).Properties.temperature = Tf(sidx)
                                    .Phases(0).Properties.pressure = P(sidx)
                                    .Phases(0).Properties.enthalpy = pp.DW_CalcEnthalpy(yf(sidx), Tf(sidx), P(sidx), PropertyPackages.State.Vapor)
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MoleFraction = yf(sidx)(i)
                                        compound_balances(subst.Name) -= yf(sidx)(i) * VSSf(sidx)
                                        i += 1
                                    Next
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MassFraction = pp.AUX_CONVERT_MOL_TO_MASS(yf(sidx))(i)
                                        i += 1
                                    Next
                                    .CopyCompositions(PhaseLabel.Mixture, PhaseLabel.Vapor)
                                    .Phases(2).Properties.molarfraction = 1.0
                                    .AtEquilibrium = True
                                End With
                            End If
                    End Select
                Next

                For Each c In comps
                    'relative errors
                    compound_balances(c) = compound_balances(c) / (compound_feeds(c) + 1.0E-20)
                Next

                Dim mintol = tol.MinY_NonZero() * 10

                If compound_balances.Values.Where(Function(b) Math.Abs(b) > mintol).Count > 0 Then
                    Dim mbal = compound_balances.Where(Function(b) Math.Abs(b.Value) > mintol).FirstOrDefault
                    Throw New Exception(String.Format("Failed to fulfill mass balance for {0}: Relative Error = {1} [Tolerance = {2}]", mbal.Key, mbal.Value, mintol))
                End If

                'condenser/reboiler duties

                Dim esm As Streams.EnergyStream

                For Each sinf In Me.EnergyStreams.Values
                    If sinf.StreamBehavior = StreamInformation.Behavior.Distillate Then
                        'condenser
                        If sinf.StreamID <> "" Then
                            esm = FlowSheet.SimulationObjects(sinf.StreamID)
                            esm.EnergyFlow = Q(0)
                            esm.GraphicObject.Calculated = True
                        End If
                    ElseIf sinf.StreamBehavior = StreamInformation.Behavior.BottomsLiquid Then
                        'reboiler
                        If sinf.StreamID <> "" Then
                            esm = FlowSheet.SimulationObjects(sinf.StreamID)
                            If esm.GraphicObject.InputConnectors(0).IsAttached Then
                                esm.EnergyFlow = Q(Me.NumberOfStages - 1)
                            Else
                                esm.EnergyFlow = -Q(Me.NumberOfStages - 1)
                            End If
                            esm.GraphicObject.Calculated = True
                        End If
                    End If
                Next

            End If

        End Sub

        Private Sub GeneratePropertiesProfileReport()

            Dim units = FlowSheet.FlowsheetOptions.SelectedUnitSystem

            Dim reporter = New Text.StringBuilder()

            reporter.AppendLine("========================================================")
            reporter.AppendLine(String.Format("Column Properties Profile"))
            reporter.AppendLine("========================================================")
            reporter.AppendLine()

            If TypeOf Me Is DistillationColumn Then
                reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}{13,16}",
                                              "Stage", "P", "T",
                                              "mV", "wV", "rhoV", "etaV", "kV",
                                              "mL", "wL", "rhoL", "etaL", "kL", "sigma"))
                reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}{13,16}",
                                              "", units.pressure, units.temperature,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity, units.surfaceTension))
            Else
                If DirectCast(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Extractor Then
                    reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}",
                                              "Stage", "P", "T",
                                              "mL1", "wL1", "rhoL1", "etaL1", "kL1",
                                              "mL2", "wL2", "rhoL2", "etaL2", "kL2"))
                    reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}",
                                              "", units.pressure, units.temperature,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity))
                Else
                    reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}{13,16}",
                                              "Stage", "P", "T",
                                              "mV", "wV", "rhoV", "etaV", "kV",
                                              "mL", "wL", "rhoL", "etaL", "kL", "sigma"))
                    reporter.AppendLine(String.Format("{0,-8}{1,16}{2,16}{3,16}{4,16}{5,16}{6,16}" +
                                              "{7,16}{8,16}{9,16}{10,16}{11,16}{12,16}{13,16}",
                                              "", units.pressure, units.temperature,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity,
                                              units.molarflow, units.massflow, units.density, units.viscosity, units.thermalConductivity, units.surfaceTension))
                End If
            End If

            reporter.AppendLine()

            Dim pp = DirectCast(PropertyPackage, Thermodynamics.PropertyPackages.PropertyPackage)

            For i = 0 To Me.Stages.Count - 1

                Dim ms As New MaterialStream("", "", FlowSheet, pp)
                FlowSheet.AddCompoundsToMaterialStream(ms)
                pp.CurrentMaterialStream = ms

                Dim compx As Double() = xf(i)
                Dim compy As Double() = yf(i)

                Dim mV, wV, rhoV, etaV, kV, mL, wL, rhoL, etaL, kL, sigma, Ti, Pi As Double

                Ti = Tf(i)
                Pi = Stages(i).P

                If TypeOf Me Is DistillationColumn Then

                    ms.SetOverallComposition(compy)
                    ms.SetPhaseComposition(compy, PropertyPackages.Phase.Vapor)

                    mV = Vf(i).ConvertFromSI(units.molarflow)
                    wV = (Vf(i) / 1000.0 * pp.AUX_MMM(compy)).ConvertFromSI(units.massflow)
                    rhoV = pp.AUX_VAPDENS(Ti, Pi).ConvertFromSI(units.density)
                    etaV = pp.AUX_VAPVISCm(Ti, rhoV.ConvertToSI(units.density), pp.AUX_MMM(compy)).ConvertFromSI(units.viscosity)
                    If Double.IsNaN(etaV) Then etaV = 0.0
                    kV = pp.AUX_CONDTG(Ti, Pi).ConvertFromSI(units.thermalConductivity)

                Else

                    If DirectCast(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Extractor Then

                        ms.SetOverallComposition(compy)
                        ms.SetPhaseComposition(compy, PropertyPackages.Phase.Liquid1)
                        ms.SetPhaseComposition(compy, PropertyPackages.Phase.Liquid)

                        mV = Vf(i).ConvertFromSI(units.molarflow)
                        wV = (Vf(i) / 1000.0 * pp.AUX_MMM(compy)).ConvertFromSI(units.massflow)
                        rhoV = pp.AUX_LIQDENS(Ti, Pi).ConvertFromSI(units.density)
                        etaV = pp.AUX_LIQVISCm(Ti, pp.AUX_MMM(compy)).ConvertFromSI(units.viscosity)
                        If Double.IsNaN(etaV) Then etaV = 0.0
                        kV = pp.AUX_CONDTL(Ti).ConvertFromSI(units.thermalConductivity)

                    Else

                        ms.SetOverallComposition(compy)
                        ms.SetPhaseComposition(compy, PropertyPackages.Phase.Vapor)

                        mV = Vf(i).ConvertFromSI(units.molarflow)
                        wV = (Vf(i) / 1000.0 * pp.AUX_MMM(compy)).ConvertFromSI(units.massflow)
                        rhoV = pp.AUX_VAPDENS(Ti, Pi).ConvertFromSI(units.density)
                        etaV = pp.AUX_VAPVISCm(Ti, rhoV.ConvertToSI(units.density), pp.AUX_MMM(compy)).ConvertFromSI(units.viscosity)
                        If Double.IsNaN(etaV) Then etaV = 0.0
                        kV = pp.AUX_CONDTG(Ti, Pi).ConvertFromSI(units.thermalConductivity)

                    End If

                End If

                ms.SetOverallComposition(compx)
                ms.SetPhaseComposition(compx, PropertyPackages.Phase.Liquid1)
                ms.SetPhaseComposition(compx, PropertyPackages.Phase.Liquid)

                mL = Lf(i).ConvertFromSI(units.molarflow)
                wL = (Lf(i) / 1000.0 * pp.AUX_MMM(compx)).ConvertFromSI(units.massflow)
                rhoL = pp.AUX_LIQDENS(Ti, Pi).ConvertFromSI(units.density)
                etaL = pp.AUX_LIQVISCm(Ti, pp.AUX_MMM(compx)).ConvertFromSI(units.viscosity)
                kL = pp.AUX_CONDTL(Ti).ConvertFromSI(units.thermalConductivity)

                sigma = pp.AUX_SURFTM(Ti).ConvertFromSI(units.surfaceTension)

                If TypeOf Me Is DistillationColumn Then

                    reporter.AppendLine(String.Format("{0,-8}{1,16:G6}{2,16:G6}{3,16:G6}{4,16:G6}{5,16:G6}{6,16:G6}" +
                                                   "{7,16:G6}{8,16:G6}{9,16:G6}{10,16:G6}{11,16:G6}{12,16:G6}{13,16:G6}",
                                                   i + 1, Pi.ConvertFromSI(units.pressure), Ti.ConvertFromSI(units.temperature),
                                                   mV, wV, rhoV, etaV, kV, mL, wL, rhoL, etaL, kL, sigma))

                Else

                    If DirectCast(Me, AbsorptionColumn).OperationMode = AbsorptionColumn.OpMode.Extractor Then

                        reporter.AppendLine(String.Format("{0,-8}{1,16:G6}{2,16:G6}{3,16:G6}{4,16:G6}{5,16:G6}{6,16:G6}" +
                                                   "{7,16:G6}{8,16:G6}{9,16:G6}{10,16:G6}{11,16:G6}{12,16:G6}",
                                                   i + 1, Pi.ConvertFromSI(units.pressure), Ti.ConvertFromSI(units.temperature),
                                                   mL, wL, rhoL, etaL, kL, mV, wV, rhoV, etaV, kV))

                    Else

                        reporter.AppendLine(String.Format("{0,-8}{1,16:G6}{2,16:G6}{3,16:G6}{4,16:G6}{5,16:G6}{6,16:G6}" +
                                                   "{7,16:G6}{8,16:G6}{9,16:G6}{10,16:G6}{11,16:G6}{12,16:G6}{13,16:G6}",
                                                   i + 1, Pi.ConvertFromSI(units.pressure), Ti.ConvertFromSI(units.temperature),
                                                   mV, wV, rhoV, etaV, kV, mL, wL, rhoL, etaL, kL, sigma))

                    End If

                End If

                ms = Nothing
                pp.CurrentMaterialStream = Nothing

            Next

            reporter.AppendLine()

            ColumnPropertiesProfile = reporter.ToString()

        End Sub

        Public Overrides Sub DeCalculate()

            Dim i As Integer

            'update output streams

            'product flows

            Dim sinf As StreamInformation
            Dim msm As MaterialStream = Nothing

            For Each sinf In Me.MaterialStreams.Values
                If FlowSheet.SimulationObjects.ContainsKey(sinf.StreamID) Then
                    Select Case sinf.StreamBehavior
                        Case StreamInformation.Behavior.Distillate
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                .Phases(0).Properties.massflow = 0
                                .Phases(0).Properties.temperature = 0
                                .Phases(0).Properties.pressure = 0
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = 0
                                    i += 1
                                Next
                            End With
                        Case StreamInformation.Behavior.OverheadVapor
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                .Phases(0).Properties.massflow = 0
                                .Phases(0).Properties.temperature = 0
                                .Phases(0).Properties.pressure = 0
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = 0
                                    i += 1
                                Next
                            End With
                        Case StreamInformation.Behavior.BottomsLiquid
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            With msm
                                .Phases(0).Properties.massflow = 0
                                .Phases(0).Properties.temperature = 0
                                .Phases(0).Properties.pressure = 0
                                i = 0
                                For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                    subst.MoleFraction = 0
                                    i += 1
                                Next
                            End With
                        Case StreamInformation.Behavior.Sidedraw
                            Dim sidx As Integer = StageIndex(sinf.AssociatedStage)
                            msm = FlowSheet.SimulationObjects(sinf.StreamID)
                            If sinf.StreamPhase = StreamInformation.Phase.L Then
                                With msm
                                    .Phases(0).Properties.massflow = 0
                                    .Phases(0).Properties.temperature = 0
                                    .Phases(0).Properties.pressure = 0
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MoleFraction = 0
                                        i += 1
                                    Next
                                End With
                            ElseIf sinf.StreamPhase = StreamInformation.Phase.V Then
                                With msm
                                    .Phases(0).Properties.massflow = 0
                                    .Phases(0).Properties.temperature = 0
                                    .Phases(0).Properties.pressure = 0
                                    i = 0
                                    For Each subst As BaseClasses.Compound In .Phases(0).Compounds.Values
                                        subst.MoleFraction = 0
                                        i += 1
                                    Next
                                End With
                            End If
                    End Select
                End If
            Next

            'condenser/reboiler duties

            Dim esm As New Streams.EnergyStream("", "")

            For Each sinf In Me.EnergyStreams.Values
                If FlowSheet.SimulationObjects.ContainsKey(sinf.StreamID) Then
                    If sinf.StreamBehavior = StreamInformation.Behavior.Distillate Then
                        'condenser
                        esm = FlowSheet.SimulationObjects(sinf.StreamID)
                        esm.EnergyFlow = 0
                        esm.GraphicObject.Calculated = False
                    ElseIf sinf.StreamBehavior = StreamInformation.Behavior.BottomsLiquid Then
                        'reboiler
                        esm = FlowSheet.SimulationObjects(sinf.StreamID)
                        esm.EnergyFlow = 0
                        esm.GraphicObject.Calculated = False
                    End If
                End If
            Next

        End Sub

        Public Overrides Sub Validate()

            Dim sinf As StreamInformation
            Dim feedok As Boolean = False
            Dim rmok As Boolean = False
            Dim cmok As Boolean = False
            Dim cmvok As Boolean = False

            'check existence/status of all specified material streams

            For Each sinf In Me.MaterialStreams.Values
                If Not FlowSheet.SimulationObjects.ContainsKey(sinf.StreamID) Then
                    Throw New Exception(FlowSheet.GetTranslatedString("DCStreamMissingException"))
                Else
                    Select Case sinf.StreamBehavior
                        Case StreamInformation.Behavior.Feed
                            If sinf.AssociatedStage = "" Then
                                Dim fs = FlowSheet.SimulationObjects(sinf.StreamID).GraphicObject.Tag
                                Throw New Exception(String.Format("Please set the Column Stage for Feed Stream '{0}'.", fs))
                            End If
                            feedok = True
                        Case StreamInformation.Behavior.Distillate
                            cmok = True
                        Case StreamInformation.Behavior.OverheadVapor
                            cmvok = True
                        Case StreamInformation.Behavior.BottomsLiquid
                            rmok = True
                    End Select
                End If
            Next

            'check if all connections were done correctly

            Select Case Me.ColumnType
                Case ColType.DistillationColumn
                    Dim dcol = DirectCast(Me, DistillationColumn)
                    If dcol.ReboiledAbsorber Then
                        cmok = True
                    End If
                    Select Case Me.CondenserType
                        Case condtype.Total_Condenser
                            If Not feedok Or Not cmok Or Not rmok Then
                                Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                            ElseIf Not cmvok And Me.CondenserType = condtype.Partial_Condenser Then
                                Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                            End If
                        Case condtype.Partial_Condenser
                            If Not feedok Or Not cmok Or Not cmvok Or Not rmok Then
                                Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                            ElseIf Not cmvok And Me.CondenserType = condtype.Partial_Condenser Then
                                Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                            End If
                        Case condtype.Full_Reflux
                            If Not feedok Or Not cmvok Or Not rmok Then
                                Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                            End If
                    End Select
                Case ColType.AbsorptionColumn
                    If Not feedok Or Not rmok Or Not (cmvok Or cmok) Then
                        Throw New Exception(FlowSheet.GetTranslatedString("DCConnectionMissingException"))
                    End If
            End Select

            'all ok, proceed to calculations...

        End Sub

        Public Overrides Function GetChartModelNames() As List(Of String)

            Return New List(Of String)({"Temperature Profile", "Pressure Profile", "Vapor Flow Profile", "Liquid Flow Profile"})

        End Function

        Public Overrides Function GetChartModel(name As String) As Object
            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem

            Dim model = New PlotModel() With {.Subtitle = name, .Title = GraphicObject.Tag}

            model.TitleFontSize = 11
            model.SubtitleFontSize = 10

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Bottom,
                .FontSize = 10
            })

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Left,
                .FontSize = 10,
                .Title = "Stage",
                .StartPosition = 1,
                .EndPosition = 0,
                .MajorStep = 1.0,
                .MinorStep = 0.5
            })

            model.LegendFontSize = 11
            model.LegendPlacement = LegendPlacement.Outside
            model.LegendOrientation = LegendOrientation.Horizontal
            model.LegendPosition = LegendPosition.BottomCenter
            model.TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView

            Dim py = PopulateColumnData(0)

            Select Case name
                Case "Temperature Profile"
                    model.AddLineSeries(PopulateColumnData(2), py)
                    model.Axes(0).Title = "Temperature (" + su.temperature + ")"
                Case "Pressure Profile"
                    model.AddLineSeries(PopulateColumnData(1), py)
                    model.Axes(0).Title = "Pressure (" + su.pressure + ")"
                Case "Vapor Flow Profile"
                    model.AddLineSeries(PopulateColumnData(3), py)
                    model.Axes(0).Title = "Molar Flow (" + su.molarflow + ")"
                Case "Liquid Flow Profile"
                    model.AddLineSeries(PopulateColumnData(4), py)
                    model.Axes(0).Title = "Molar Flow (" + su.molarflow + ")"
            End Select

            Return model

        End Function

        Private Function PopulateColumnData(position As Integer) As List(Of Double)
            Dim su = FlowSheet.FlowsheetOptions.SelectedUnitSystem
            Dim vec As New List(Of Double)()
            Select Case position
                Case 0
                    'stage
                    Dim comp_ant As Double = 1.0F
                    For Each st In Stages
                        vec.Add(comp_ant)
                        comp_ant += 1.0F
                    Next
                    Exit Select
                Case 1
                    'pressure
                    vec = SystemsOfUnits.Converter.ConvertArrayFromSI(su.pressure, P0).ToList()
                    Exit Select
                Case 2
                    'temperature
                    vec = SystemsOfUnits.Converter.ConvertArrayFromSI(su.temperature, Tf).ToList()
                    Exit Select
                Case 3
                    'vapor flow
                    vec = SystemsOfUnits.Converter.ConvertArrayFromSI(su.molarflow, Vf).ToList()
                    Exit Select
                Case 4
                    'liquid flow
                    vec = SystemsOfUnits.Converter.ConvertArrayFromSI(su.molarflow, Lf).ToList()
                    Exit Select
            End Select
            Return vec
        End Function

    End Class

End Namespace


''' <summary>
''' Contains auxiliary data classes for rigorous separation operations such as distillation and absorption column stage parameters and specifications.
''' </summary>
Namespace UnitOperations.Auxiliary.SepOps

    <System.Serializable()> Public Class Parameter

        Implements Interfaces.ICustomXMLSerialization

        Enum ParameterType
            Fixed
            Variable
        End Enum

        Private m_value As Double
        Private m_type As ParameterType = ParameterType.Fixed
        Private _minval, _maxval As Double

        Public Property MaxVal() As Double
            Get
                Return _maxval
            End Get
            Set(ByVal value As Double)
                _maxval = value
            End Set
        End Property

        Public Property MinVal() As Double
            Get
                Return _minval
            End Get
            Set(ByVal value As Double)
                _minval = value
            End Set
        End Property

        Public Property Value() As Double
            Get
                Return m_value
            End Get
            Set(ByVal value As Double)
                m_value = value
            End Set
        End Property

        Public Property ParamType() As ParameterType
            Get
                Return m_type
            End Get
            Set(ByVal value As ParameterType)
                m_type = value
            End Set
        End Property

        Public Overrides Function ToString() As String
            Return Me.Value.ToString
        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

    End Class

    <System.Serializable()> Public Class Stage

        Implements Interfaces.ICustomXMLSerialization

        Public Property Name As String = ""

        Public Property ID As String = ""

        Public ReadOnly Property Kvalues As Dictionary(Of String, Parameter)

        Public ReadOnly Property v As Dictionary(Of String, Parameter)

        Public ReadOnly Property l As Dictionary(Of String, Parameter)

        Public Property P As Double

        Public Property T As Double

        Public Property Efficiency As Double = 1.0

        Public Property Q As New Parameter

        Public Property Vss As New Parameter

        Public Property Lss As New Parameter

        Public Property Vout As New Parameter

        Public Property Vin As New Parameter

        Public Property Lout As New Parameter

        Public Property Lin As New Parameter

        Public Property F As New Parameter

        Public Property DryTrayPressureDropCoefficient As Double = 0.085

        Public Property LiquidFlowEquationCoefficient_Alpha As Double = 1.84

        Public Property LiquidFlowEquationCoefficient_Beta As Double = 0.6

        Public Property TotalHoleArea As Double = 0.8

        Public Property DowncomerLength As Double = 0.1

        Public Property DowncomerHeight As Double = 0.3

        Public Property DowncomerArea As Double = 0.0

        Public Property LiquidLevel As Double = 0.0

        Public Property StageHeight As Double = 0.0

        Public Property AccumulationStream As MaterialStream

        Sub New(_id As String)

            ID = _id

            Kvalues = New Dictionary(Of String, Parameter)
            l = New Dictionary(Of String, Parameter)
            v = New Dictionary(Of String, Parameter)

        End Sub

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            'Legacy fix: stages created before the Efficiency default was set to 1.0 were saved with
            'Efficiency = 0, which degenerates the equilibrium (E) equations in the column solvers.
            'A zero Murphree stage efficiency is physically meaningless (the stage would do nothing),
            'so treat a non-positive stored value as the default of 1.0.
            If Efficiency <= 0.0 Then Efficiency = 1.0
            Dim fields As Reflection.PropertyInfo() = Me.GetType.GetProperties()
            For Each fi As Reflection.PropertyInfo In fields
                Dim propname As String = fi.Name
                If TypeOf Me.GetType.GetProperty(fi.Name).PropertyType Is IDictionary(Of String, Parameter) Then
                    Dim xel As XElement = (From xmlprop In data Select xmlprop Where xmlprop.Name = propname).SingleOrDefault
                    If Not xel Is Nothing Then
                        Dim val As List(Of XElement) = xel.Elements.ToList()
                        For Each xel2 As XElement In val
                            Dim p As New Parameter()
                            p.LoadData(xel2.Elements.ToList)
                            DirectCast(Me.GetType.GetProperty(fi.Name).PropertyType, IDictionary(Of String, Parameter)).Add(xel.@Key, p)
                        Next
                    End If
                End If
            Next
            Return True
        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements As List(Of System.Xml.Linq.XElement) = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture
            With elements
                Dim fields As Reflection.PropertyInfo() = Me.GetType.GetProperties()
                For Each fi As Reflection.PropertyInfo In fields
                    If TypeOf Me.GetType.GetProperty(fi.Name).PropertyType Is IDictionary(Of String, Parameter) Then
                        Dim collection As IDictionary(Of String, Parameter) = DirectCast(Me.GetType.GetProperty(fi.Name).GetValue(Me, Nothing), IDictionary(Of String, Parameter))
                        .Add(New XElement(fi.Name))
                        For Each kvp As KeyValuePair(Of String, Parameter) In collection
                            .Item(.Count - 1).Add(New XElement("Item", New XAttribute("Key", kvp.Key), kvp.Value.SaveData.ToArray))
                        Next
                    End If
                Next
            End With

            Return elements

        End Function

    End Class

    <System.Serializable()> Public Class InitialEstimates

        Implements Interfaces.ICustomXMLSerialization

        Public Property VaporProductFlowRate As Double?
        Public Property DistillateFlowRate As Double?
        Public Property BottomsFlowRate As Double?
        Public Property RefluxRatio As Double?

        Private _liqcompositions As New List(Of Dictionary(Of String, Parameter))
        Private _vapcompositions As New List(Of Dictionary(Of String, Parameter))
        Private _stagetemps As New List(Of Parameter)
        Private _liqmolflows As New List(Of Parameter)
        Private _vapmolflows As New List(Of Parameter)

        Public Function ValidateTemperatures() As Boolean

            If _stagetemps.Count = 0 Then Return False

            If _stagetemps.Select(Function(x) x.Value).ToArray().Sum = 0.0 Then Return False

            If Not _stagetemps.Select(Function(x) x.Value).ToArray().IsValid Then Return False

            Return True

        End Function

        Public Function ValidateVaporFlows() As Boolean

            If _vapmolflows.Count = 0 Then Return False

            If _vapmolflows.Select(Function(x) x.Value).ToArray().Sum = 0.0 Then Return False

            If Not _vapmolflows.Select(Function(x) x.Value).ToArray().IsValid Then Return False

            Return True

        End Function

        Public Function ValidateLiquidFlows() As Boolean

            If _liqmolflows.Count = 0 Then Return False

            If _liqmolflows.Select(Function(x) x.Value).ToArray().Sum = 0.0 Then Return False

            If Not _liqmolflows.Select(Function(x) x.Value).ToArray().IsValid Then Return False

            Return True

        End Function

        Public Function ValidateCompositions() As Boolean

            If _liqcompositions.Select(Function(x) x.Values.Select(Function(x2) x2.Value).Sum).Sum = 0.0 Then
                Return False
            End If
            If _vapcompositions.Select(Function(x) x.Values.Select(Function(x2) x2.Value).Sum).Sum = 0.0 Then
                Return False
            End If
            If Not _liqcompositions.Select(Function(x) x.Values.Select(Function(x2) x2.Value).Sum).ToArray().IsValid Then
                Return False
            End If
            If Not _vapcompositions.Select(Function(x) x.Values.Select(Function(x2) x2.Value).Sum).ToArray().IsValid Then
                Return False
            End If

            If _liqcompositions.Count = 0 Then Return False
            If _vapcompositions.Count = 0 Then Return False

            Return True

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "LiquidCompositions").SingleOrDefault.Elements.ToList
                Dim var As New Dictionary(Of String, Parameter)
                For Each xel2 As XElement In xel.Elements
                    Dim p As New Parameter
                    p.LoadData(xel2.Elements.ToList)
                    var.Add(xel2.@ID, p)
                Next
                _liqcompositions.Add(var)
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "VaporCompositions").SingleOrDefault.Elements.ToList
                Dim var As New Dictionary(Of String, Parameter)
                For Each xel2 As XElement In xel.Elements
                    Dim p As New Parameter
                    p.LoadData(xel2.Elements.ToList)
                    var.Add(xel2.@ID, p)
                Next
                _vapcompositions.Add(var)
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "StageTemps").SingleOrDefault.Elements.ToList
                Dim var As New Parameter
                var.LoadData(xel.Elements.ToList)
                _stagetemps.Add(var)
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "LiqMoleFlows").SingleOrDefault.Elements.ToList
                Dim var As New Parameter
                var.LoadData(xel.Elements.ToList)
                _liqmolflows.Add(var)
            Next

            For Each xel As XElement In (From xel2 As XElement In data Select xel2 Where xel2.Name = "VapMoleFlows").SingleOrDefault.Elements.ToList
                Dim var As New Parameter
                var.LoadData(xel.Elements.ToList)
                _vapmolflows.Add(var)
            Next
            Return True
        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Dim elements = XMLSerializer.XMLSerializer.Serialize(Me)
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            With elements

                .Add(New XElement("LiquidCompositions"))
                For Each dict As Dictionary(Of String, Parameter) In _liqcompositions
                    .Item(.Count - 1).Add(New XElement("LiquidComposition"))
                    For Each kvp As KeyValuePair(Of String, Parameter) In dict
                        .Item(.Count - 1).Elements.Last.Add(New XElement("Compound", New XAttribute("ID", kvp.Key), kvp.Value.SaveData.ToArray))
                    Next
                Next
                .Add(New XElement("VaporCompositions"))
                For Each dict As Dictionary(Of String, Parameter) In _vapcompositions
                    .Item(.Count - 1).Add(New XElement("VaporComposition"))
                    For Each kvp As KeyValuePair(Of String, Parameter) In dict
                        .Item(.Count - 1).Elements.Last.Add(New XElement("Compound", New XAttribute("ID", kvp.Key), kvp.Value.SaveData.ToArray))
                    Next
                Next
                .Add(New XElement("StageTemps"))
                For Each p As Parameter In _stagetemps
                    .Item(.Count - 1).Add(New XElement("StageTemp", p.SaveData.ToArray))
                Next
                .Add(New XElement("LiqMoleFlows"))
                For Each p As Parameter In _liqmolflows
                    .Item(.Count - 1).Add(New XElement("LiqMoleFlow", p.SaveData.ToArray))
                Next
                .Add(New XElement("VapMoleFlows"))
                For Each p As Parameter In _vapmolflows
                    .Item(.Count - 1).Add(New XElement("VapMoleFlow", p.SaveData.ToArray))
                Next

            End With

            Return elements

        End Function

        Public ReadOnly Property LiqCompositions() As List(Of Dictionary(Of String, Parameter))
            Get
                Return _liqcompositions
            End Get
        End Property

        Public ReadOnly Property VapCompositions() As List(Of Dictionary(Of String, Parameter))
            Get
                Return _vapcompositions
            End Get
        End Property

        Public ReadOnly Property StageTemps() As List(Of Parameter)
            Get
                Return _stagetemps
            End Get
        End Property

        Public ReadOnly Property LiqMolarFlows() As List(Of Parameter)
            Get
                Return _liqmolflows
            End Get
        End Property

        Public ReadOnly Property VapMolarFlows() As List(Of Parameter)
            Get
                Return _vapmolflows
            End Get
        End Property

        Sub New()
            _liqcompositions = New List(Of Dictionary(Of String, Parameter))
            _vapcompositions = New List(Of Dictionary(Of String, Parameter))
            _stagetemps = New List(Of Parameter)
            _liqmolflows = New List(Of Parameter)
            _vapmolflows = New List(Of Parameter)
        End Sub

    End Class

    Public Class ColumnSolverInputData

        Public Property ColumnObject As Column

        Public Property CalculationMode As Integer = 0

        Public Property NumberOfCompounds As Integer
        Public Property NumberOfStages As Integer

        Public Property MaximumIterations As Integer
        Public Property EarlyStopIteration As Integer = -1
        Public Property Tolerances() As List(Of Double)

        Public Property StageTemperatures As List(Of Double)
        Public Property StagePressures As List(Of Double)
        Public Property StageHeats As List(Of Double)
        Public Property StageEfficiencies As List(Of Double)

        Public Property FeedFlows As List(Of Double)
        Public Property FeedCompositions As List(Of Double())
        Public Property FeedEnthalpies As List(Of Double)
        Public Property VaporFlows As List(Of Double)
        Public Property VaporCompositions As List(Of Double())
        Public Property LiquidFlows As List(Of Double)
        Public Property LiquidCompositions As List(Of Double())
        Public Property VaporSideDraws As List(Of Double)
        Public Property LiquidSideDraws As List(Of Double)

        Public Property Kvalues As List(Of Double())
        Public Property OverallCompositions As List(Of Double())

        Public Property CondenserType As condtype
        Public Property ColumnType As ColType

        Public Property CondenserSpec As ColumnSpec
        Public Property ReboilerSpec As ColumnSpec

        Public Property L1trials As List(Of Double())
        Public Property L2trials As List(Of Double())
        Public Property x1trials As List(Of Double()())
        Public Property x2trials As List(Of Double()())

        Public Property SubcoolingDeltaT As Double = 0.0

    End Class

    Public Class ColumnSolverOutputData

        Public Property IterationsTaken As Integer
        Public Property FinalError As Double
        Public Property StageTemperatures As List(Of Double)
        Public Property StageHeats As List(Of Double)
        Public Property VaporFlows As List(Of Double)
        Public Property VaporCompositions As List(Of Double())
        Public Property LiquidFlows As List(Of Double)
        Public Property LiquidCompositions As List(Of Double())
        Public Property VaporSideDraws As List(Of Double)
        Public Property LiquidSideDraws As List(Of Double)
        Public Property Kvalues As List(Of Double())

    End Class

    <System.Serializable()> Public Class StreamInformation

        Implements Interfaces.ICustomXMLSerialization

        Public Enum Type
            Material = 0
            Energy = 1
        End Enum

        Public Enum Behavior
            Distillate = 0
            BottomsLiquid = 1
            Feed = 2
            Sidedraw = 3
            OverheadVapor = 4
            SideOpLiquidProduct = 5
            SideOpVaporProduct = 6
            Steam = 7
            InterExchanger = 8
        End Enum

        Public Enum Phase
            L = 0
            V = 1
            B = 2
            None = 3
        End Enum

        Public Enum Position
            Above = 0
            Below = 1
        End Enum

        Public Property StreamID As String = ""

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            Dim xel = (From xe In data Select xe Where xe.Name = "Name").SingleOrDefault

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            If Not xel Is Nothing Then Me.StreamID = xel.Value
            If Me.StreamID = "" Then Me.StreamID = Me.ID

            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

        Public Property FlowRate As Parameter

        Public Property ID As String = ""

        Public Property SideOpID As String = ""

        Public Property StreamPhase As Phase = Phase.L

        Public Property StreamBehavior As Behavior = Behavior.Feed

        Public Property StreamType As Type = Type.Material

        Public Property StreamPosition As Position = Position.Above

        Public Property AssociatedStage As String = ""

        Sub New()
            FlowRate = New Parameter
        End Sub

        Sub New(ByVal _id As String, ByVal _streamID As String, ByVal _associatedstage As String, ByVal _t As Type, ByVal _bhv As Behavior, ByVal _ph As Phase)
            Me.New()
            ID = _id
            AssociatedStage = _associatedstage
            StreamType = _t
            StreamBehavior = _bhv
            StreamPhase = _ph
        End Sub

    End Class

    <System.Serializable()> Public Class ColumnSpec

        Implements Interfaces.ICustomXMLSerialization

        Public Enum SpecType
            Heat_Duty = 0
            Product_Molar_Flow_Rate = 1
            Component_Molar_Flow_Rate = 2
            Product_Mass_Flow_Rate = 3
            Component_Mass_Flow_Rate = 4
            Component_Fraction = 5
            Component_Recovery = 6
            Stream_Ratio = 7
            Temperature = 8
            Feed_Recovery = 9
        End Enum

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            XMLSerializer.XMLSerializer.Deserialize(Me, data)
            If SpecUnit = "W" Then SpecUnit = "Mass"
            If SpecUnit = "We" Then SpecUnit = "Mass"
            Return True

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

        Sub New()

        End Sub

        Public Property SpecUnit As String = ""

        Public Property SpecValue As Double

        Public Property ComponentID As String = ""

        Public Property ComponentIndex As Integer

        Public Property StageNumber As Integer

        Public Property SType As SpecType = SpecType.Component_Molar_Flow_Rate

        Public Property CalculatedValue As Double

        Public Property InitialEstimate As Double?

    End Class

    Public MustInherit Class ColumnSolver

        Public MustOverride ReadOnly Property Name As String

        Public MustOverride ReadOnly Property Description As String

        Public MustOverride Function SolveColumn(input As ColumnSolverInputData) As ColumnSolverOutputData

        Public Overridable Function SolveColumn(col As Column, input As ColumnSolverInputData) As ColumnSolverOutputData

            Throw New NotImplementedException()

        End Function

    End Class

End Namespace
