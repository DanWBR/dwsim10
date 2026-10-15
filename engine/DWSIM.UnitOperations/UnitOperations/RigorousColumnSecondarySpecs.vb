'    Rigorous column: secondary specifications on product properties.
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

Imports DWSIM.Interfaces.Enums
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics.Utilities.PetroleumProperties
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

Namespace UnitOperations.Auxiliary.SepOps

    ''' <summary>
    ''' A specification on a property of a column product (a distillation curve point, the flash point,
    ''' the density at 20 C, or any stream property), met by moving one of the column's free variables.
    ''' </summary>
    <Serializable()> Public Class ColumnSecondarySpec

        Public Enum ManipulatedVariableType
            ''' <summary>The value of the condenser specification, in its own units.</summary>
            CondenserSpecValue = 0
            ''' <summary>The value of the reboiler specification, in its own units.</summary>
            ReboilerSpecValue = 1
            ''' <summary>The molar flow of a side draw, in mol/s.</summary>
            SideDrawRate = 2
        End Enum

        Public Property Enabled As Boolean = True

        Public Property Name As String = ""

        ''' <summary>The flowsheet name of the product stream the property is read on.</summary>
        Public Property ProductStreamID As String = ""

        ''' <summary>
        ''' A distillation curve point ("D86 95%", "TBP 10%"), a product property of the distillation
        ''' curves utility ("Flash Point (Closed Cup)", "Density @ 20 C") or a material stream property ID.
        ''' </summary>
        Public Property PropertyName As String = ""

        ''' <summary>The value to reach, in SI units.</summary>
        Public Property TargetValue As Double

        ''' <summary>The largest acceptable difference from the target, in SI units. Zero takes 0.1 % of the target.</summary>
        Public Property Tolerance As Double

        Public Property ManipulatedVariable As ManipulatedVariableType = ManipulatedVariableType.CondenserSpecValue

        ''' <summary>The flowsheet name of the side draw stream, when the side draw rate is the manipulated variable.</summary>
        Public Property ManipulatedStreamID As String = ""

        ''' <summary>Lower limit of the manipulated variable, in its own units. With MinValue = MaxValue there are no limits.</summary>
        Public Property MinValue As Double

        ''' <summary>Upper limit of the manipulated variable, in its own units.</summary>
        Public Property MaxValue As Double

        ''' <summary>The value the property took in the last solution, in SI units.</summary>
        Public Property CalculatedValue As Double = Double.NaN

        ''' <summary>The value of the manipulated variable in the last solution.</summary>
        Public Property ManipulatedValue As Double = Double.NaN

        Public Property Converged As Boolean

        Public Function EffectiveTolerance() As Double
            If Tolerance > 0 Then Return Tolerance
            Return 0.001 * Math.Max(Math.Abs(TargetValue), 1.0)
        End Function

    End Class

End Namespace

Namespace UnitOperations

    Partial Public MustInherit Class Column

        Private _secondarySpecs As New List(Of ColumnSecondarySpec)
        Private _secondarySpecPass As Boolean

        ''' <summary>Specifications on product properties, met by moving the condenser or reboiler specification or a side draw rate.</summary>
        <Xml.Serialization.XmlIgnore>
        Public Property SecondarySpecs As List(Of ColumnSecondarySpec)
            Get
                If _secondarySpecs Is Nothing Then _secondarySpecs = New List(Of ColumnSecondarySpec)
                Return _secondarySpecs
            End Get
            Set(value As List(Of ColumnSecondarySpec))
                _secondarySpecs = value
            End Set
        End Property

        ''' <summary>The secondary specifications as stored in the simulation file.</summary>
        Public Property SecondarySpecsData As String
            Get
                If SecondarySpecs.Count = 0 Then Return ""
                Return Newtonsoft.Json.JsonConvert.SerializeObject(SecondarySpecs)
            End Get
            Set(value As String)
                If String.IsNullOrWhiteSpace(value) Then
                    SecondarySpecs = New List(Of ColumnSecondarySpec)
                Else
                    SecondarySpecs = Newtonsoft.Json.JsonConvert.DeserializeObject(Of List(Of ColumnSecondarySpec))(value)
                End If
            End Set
        End Property

        ''' <summary>Notes from the last secondary specification solve.</summary>
        <Xml.Serialization.XmlIgnore>
        Public Property SecondarySpecsLog As New List(Of String)

        ''' <summary>Connects a side draw to a stage by its zero-based index (0 is the top stage), as a liquid or a
        ''' vapour draw with a fixed molar flow in mol/s.</summary>
        Public Sub ConnectSideDraw(stream As Interfaces.ISimulationObject, stagenumber As Integer, vapor As Boolean, molarFlow As Double)

            Dim i As Integer = 0
            Dim success As Boolean = False
            For Each con In GraphicObject.OutputConnectors
                If Not con.IsAttached AndAlso con.ConnectorName.Contains("Side") Then
                    FlowSheet.ConnectObjects(GraphicObject, stream.GraphicObject, i, 0)
                    Dim msi As New StreamInformation With {.ID = stream.Name, .StreamID = stream.Name,
                        .AssociatedStage = Stages(stagenumber).ID,
                        .StreamBehavior = StreamInformation.Behavior.Sidedraw,
                        .StreamType = StreamInformation.Type.Material,
                        .StreamPhase = If(vapor, StreamInformation.Phase.V, StreamInformation.Phase.L)}
                    msi.FlowRate.Value = molarFlow
                    MaterialStreams.Add(msi.ID, msi)
                    success = True
                    Exit For
                End If
                i += 1
            Next
            If Not success Then Throw New Exception("No side draw port available")

        End Sub

        Private Function HasActiveSecondarySpecs() As Boolean
            Return TypeOf Me Is DistillationColumn AndAlso _secondarySpecs IsNot Nothing AndAlso _secondarySpecs.Any(Function(s) s.Enabled)
        End Function

        Private Function SecondarySpecStream(id As String) As StreamInformation
            Return MaterialStreams.Values.FirstOrDefault(Function(s) s.StreamID = id)
        End Function

        Private Sub ValidateSecondarySpecs(active As List(Of ColumnSecondarySpec))

            Dim products = {StreamInformation.Behavior.Distillate, StreamInformation.Behavior.OverheadVapor,
                            StreamInformation.Behavior.BottomsLiquid, StreamInformation.Behavior.Sidedraw}
            Dim sideDraws = MaterialStreams.Values.Where(Function(s) s.StreamBehavior = StreamInformation.Behavior.Sidedraw).Count()

            If active.Count > 2 + sideDraws Then
                Throw New Exception(String.Format("{0}: {1} secondary specifications are active, but the column has only {2} free variables (condenser, reboiler and side draws).",
                                                  GraphicObject.Tag, active.Count, 2 + sideDraws))
            End If

            Dim used As New HashSet(Of String)
            For Each s In active
                Dim label = If(s.Name <> "", s.Name, s.PropertyName)
                Dim info = SecondarySpecStream(s.ProductStreamID)
                If info Is Nothing OrElse Not products.Contains(info.StreamBehavior) Then
                    Throw New Exception(String.Format("{0}: the secondary specification '{1}' names a stream that is not a product of the column.", GraphicObject.Tag, label))
                End If
                If String.IsNullOrWhiteSpace(s.PropertyName) Then
                    Throw New Exception(String.Format("{0}: the secondary specification '{1}' has no property.", GraphicObject.Tag, label))
                End If
                Dim key = s.ManipulatedVariable.ToString()
                If s.ManipulatedVariable = ColumnSecondarySpec.ManipulatedVariableType.SideDrawRate Then
                    Dim sd = SecondarySpecStream(s.ManipulatedStreamID)
                    If sd Is Nothing OrElse sd.StreamBehavior <> StreamInformation.Behavior.Sidedraw Then
                        Throw New Exception(String.Format("{0}: the secondary specification '{1}' moves a side draw that the column does not have.", GraphicObject.Tag, label))
                    End If
                    key &= s.ManipulatedStreamID
                End If
                If Not used.Add(key) Then
                    Throw New Exception(String.Format("{0}: two secondary specifications move the same variable.", GraphicObject.Tag))
                End If
            Next

        End Sub

        Private Function GetSecondaryManipulated(s As ColumnSecondarySpec) As Double
            Select Case s.ManipulatedVariable
                Case ColumnSecondarySpec.ManipulatedVariableType.CondenserSpecValue
                    Return Specs("C").SpecValue
                Case ColumnSecondarySpec.ManipulatedVariableType.ReboilerSpecValue
                    Return Specs("R").SpecValue
                Case Else
                    Return SecondarySpecStream(s.ManipulatedStreamID).FlowRate.Value
            End Select
        End Function

        Private Sub SetSecondaryManipulated(s As ColumnSecondarySpec, value As Double)
            Select Case s.ManipulatedVariable
                Case ColumnSecondarySpec.ManipulatedVariableType.CondenserSpecValue
                    Specs("C").SpecValue = value
                Case ColumnSecondarySpec.ManipulatedVariableType.ReboilerSpecValue
                    Specs("R").SpecValue = value
                Case Else
                    SecondarySpecStream(s.ManipulatedStreamID).FlowRate.Value = value
            End Select
        End Sub

        ''' <summary>A free-standing copy of a product as the last solution left it: composition, flow, temperature and pressure.</summary>
        Private Function SecondarySpecProduct(id As String) As MaterialStream

            Dim info = SecondarySpecStream(id)
            Dim ns = Stages.Count - 1
            Dim comp As Double(), flow As Double, k As Integer
            Select Case info.StreamBehavior
                Case StreamInformation.Behavior.Distillate
                    comp = xf(0) : flow = LSSf(0) : k = 0
                Case StreamInformation.Behavior.OverheadVapor
                    comp = yf(0) : flow = Vf(0) : k = 0
                Case StreamInformation.Behavior.BottomsLiquid
                    comp = xf(ns) : flow = Lf(ns) : k = ns
                Case Else
                    k = StageIndex(info.AssociatedStage)
                    If info.StreamPhase = StreamInformation.Phase.V Then
                        comp = yf(k) : flow = VSSf(k)
                    Else
                        comp = xf(k) : flow = LSSf(k)
                    End If
            End Select

            Dim pp = DirectCast(PropertyPackage, DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)
            Dim ms As New MaterialStream("", "", FlowSheet, pp)
            FlowSheet.AddCompoundsToMaterialStream(ms)
            With ms
                .SpecType = StreamSpec.Temperature_and_Pressure
                .Phases(0).Properties.temperature = Tf(k)
                .Phases(0).Properties.pressure = P0(k)
                .Phases(0).Properties.molarflow = flow
                Dim i = 0
                For Each c In .Phases(0).Compounds.Values
                    c.MoleFraction = comp(i)
                    i += 1
                Next
            End With
            Return ms

        End Function

        ''' <summary>The value of a secondary specification's property on the current solution, in SI units.</summary>
        Public Function EvaluateSecondarySpec(s As ColumnSecondarySpec) As Double

            Dim pp = DirectCast(PropertyPackage, DWSIM.Thermodynamics.PropertyPackages.PropertyPackage)
            Dim previous = pp.CurrentMaterialStream
            Try
                Dim ms = SecondarySpecProduct(s.ProductStreamID)
                Dim ct As DistillationCurveType, pct As Double
                Dim isPoint = DistillationCurvesUtility.TryParsePointName(s.PropertyName, ct, pct)
                If isPoint OrElse DistillationCurvesUtility.TemperatureProperties.Contains(s.PropertyName) OrElse
                    DistillationCurvesUtility.OtherProperties.Contains(s.PropertyName) Then
                    Dim settings As New DistillationCurveSettings
                    If isPoint Then settings.Percents = settings.Percents.Concat({pct}).Distinct().OrderBy(Function(v) v).ToArray()
                    ' the EFV flash is the expensive part; only an EFV point needs it
                    If Not (isPoint AndAlso ct = DistillationCurveType.EFV) Then settings.EFVMethod = EFVCalculationMethod.Correlation
                    Dim res = DistillationCurves.Calculate(ms, settings)
                    Return DistillationCurvesUtility.GetValueSI(res, s.PropertyName)
                Else
                    pp.CurrentMaterialStream = ms
                    ms.Calculate(True, True)
                    Return Convert.ToDouble(ms.GetPropertyValue(s.PropertyName))
                End If
            Finally
                pp.CurrentMaterialStream = previous
            End Try

        End Function

        ''' <summary>
        ''' Solves the column for its secondary specifications: each one moves its variable until the
        ''' product property reaches the target. Newton's method with a finite-difference Jacobian, updated
        ''' by Broyden after each step, and a step halved while the residual does not fall. Every function
        ''' evaluation is a full column solve; the products are written once, from the final solution.
        ''' </summary>
        Private Sub SolveWithSecondarySpecs(args As Object)

            Dim active = SecondarySpecs.Where(Function(s) s.Enabled).ToList()
            ValidateSecondarySpecs(active)

            Dim n = active.Count
            Dim innerArgs As Object = "SecondarySpecs"   ' a non-null argument keeps the products as they are
            Dim tol = active.Select(Function(s) s.EffectiveTolerance()).ToArray()
            Dim solves = 0
            SecondarySpecsLog.Clear()
            Dim ci = Globalization.CultureInfo.InvariantCulture

            _secondarySpecPass = True
            Try

                Dim residuals = Function() As Double()
                                    Dim r(n - 1) As Double
                                    For j = 0 To n - 1
                                        active(j).CalculatedValue = EvaluateSecondarySpec(active(j))
                                        r(j) = (active(j).CalculatedValue - active(j).TargetValue) / tol(j)
                                    Next
                                    Return r
                                End Function

                Dim evaluate = Function(values As Double()) As Double()
                                   For j = 0 To n - 1
                                       SetSecondaryManipulated(active(j), values(j))
                                   Next
                                   Try
                                       Calculate(innerArgs)
                                       solves += 1
                                       Dim r = residuals()
                                       SecondarySpecsLog.Add(String.Format(ci, "  solve {0}: variables {1} -> properties {2}", solves,
                                                                           String.Join("; ", values.Select(Function(v) v.ToString("G6", ci))),
                                                                           String.Join("; ", active.Select(Function(a) a.CalculatedValue.ToString("G6", ci)))))
                                       If r.Any(Function(v) Double.IsNaN(v) OrElse Double.IsInfinity(v)) Then Return Nothing
                                       Return r
                                   Catch oex As OperationCanceledException
                                       Throw
                                   Catch ex As Exception
                                       Return Nothing
                                   End Try
                               End Function

                Dim norm = Function(r As Double()) Math.Sqrt(r.Sum(Function(v) v * v))
                Dim done = Function(r As Double()) r.All(Function(v) Math.Abs(v) <= 1.0)

                ' the solution at the values the column already has
                Calculate(innerArgs)
                solves += 1
                Dim u = active.Select(Function(s) GetSecondaryManipulated(s)).ToArray()
                Dim r0 = residuals()
                If r0.Any(Function(v) Double.IsNaN(v) OrElse Double.IsInfinity(v)) Then
                    Throw New Exception(GraphicObject.Tag & ": a secondary specification property could not be calculated on the products.")
                End If

                Dim bestU = u.ToArray(), bestR = r0.ToArray()

                If Not done(r0) Then

                    ' finite-difference Jacobian
                    Dim J(n - 1, n - 1) As Double
                    For c = 0 To n - 1
                        Dim h = If(Math.Abs(u(c)) > 0.000000000001, 0.02 * Math.Abs(u(c)), 0.001)
                        Dim up = u.ToArray()
                        up(c) = Clip(active(c), u(c) + h, u(c))
                        If up(c) = u(c) Then up(c) = Clip(active(c), u(c) - h, u(c))
                        Dim rp = evaluate(up)
                        If rp Is Nothing Then
                            up(c) = Clip(active(c), u(c) - h, u(c))
                            rp = evaluate(up)
                        End If
                        If rp Is Nothing OrElse up(c) = u(c) Then Throw New Exception(GraphicObject.Tag & ": the column did not solve when its secondary specification variables were moved.")
                        For rr = 0 To n - 1
                            J(rr, c) = (rp(rr) - r0(rr)) / (up(c) - u(c))
                        Next
                        If norm(rp) < norm(bestR) Then bestU = up.ToArray() : bestR = rp.ToArray()
                    Next

                    Dim r = r0
                    For iter = 1 To 30
                        Dim du = SolveLinear(J, r.Select(Function(v) -v).ToArray())
                        If du Is Nothing Then
                            SecondarySpecsLog.Add("The sensitivity of the specifications to their variables is singular: one variable does not move its property.")
                            Exit For
                        End If
                        ' no variable moves by more than half of its value in one step
                        For c = 0 To n - 1
                            Dim lim = 0.5 * Math.Max(Math.Abs(u(c)), 0.000001)
                            If Math.Abs(du(c)) > lim Then
                                Dim f = lim / Math.Abs(du(c))
                                For k = 0 To n - 1 : du(k) *= f : Next
                            End If
                        Next
                        Dim accepted = False
                        Dim lambda = 1.0
                        For half = 0 To 5
                            Dim unew(n - 1) As Double
                            For c = 0 To n - 1 : unew(c) = Clip(active(c), u(c) + lambda * du(c), u(c)) : Next
                            Dim rnew = evaluate(unew)
                            If rnew IsNot Nothing AndAlso norm(rnew) < norm(r) Then
                                ' Broyden update of the Jacobian
                                Dim s(n - 1) As Double, ss = 0.0
                                For c = 0 To n - 1 : s(c) = unew(c) - u(c) : ss += s(c) * s(c) : Next
                                If ss > 0 Then
                                    For rr = 0 To n - 1
                                        Dim js = 0.0
                                        For c = 0 To n - 1 : js += J(rr, c) * s(c) : Next
                                        Dim dr = (rnew(rr) - r(rr)) - js
                                        For c = 0 To n - 1 : J(rr, c) += dr * s(c) / ss : Next
                                    Next
                                End If
                                u = unew : r = rnew
                                If norm(r) < norm(bestR) Then bestU = u.ToArray() : bestR = r.ToArray()
                                accepted = True
                                Exit For
                            End If
                            lambda /= 2
                        Next
                        SecondarySpecsLog.Add(String.Format(ci, "iteration {0}: largest residual {1:G4} tolerances, {2} column solves", iter, r.Max(Function(v) Math.Abs(v)), solves))
                        If done(r) Then Exit For
                        If Not accepted Then
                            SecondarySpecsLog.Add("No step along the Newton direction reduced the residual; the column keeps the best solution found.")
                            Exit For
                        End If
                    Next

                End If

                ' the final solve writes the products, at the best values found
                For j = 0 To n - 1 : SetSecondaryManipulated(active(j), bestU(j)) : Next
                Calculate(args)
                solves += 1
                Dim rf = residuals()
                For j = 0 To n - 1
                    active(j).ManipulatedValue = bestU(j)
                    active(j).Converged = Math.Abs(rf(j)) <= 1.0
                Next
                SecondarySpecsLog.Add(String.Format(ci, "{0} column solves in all.", solves))

                Dim missed = active.Where(Function(s) Not s.Converged).Select(Function(s) If(s.Name <> "", s.Name, s.PropertyName)).ToList()
                If missed.Count > 0 Then
                    FlowSheet.ShowMessage(GraphicObject.Tag & ": secondary specifications not met: " & String.Join(", ", missed) & ". The column carries the closest solution found.",
                                          Interfaces.IFlowsheet.MessageType.Warning)
                End If

            Finally
                _secondarySpecPass = False
            End Try

        End Sub

        Private Shared Function Clip(s As ColumnSecondarySpec, value As Double, current As Double) As Double
            If s.MaxValue > s.MinValue Then value = Math.Max(s.MinValue, Math.Min(s.MaxValue, value))
            ' flows, ratios and recoveries stay positive when they start positive
            If current > 0 AndAlso value <= 0 Then value = 0.5 * current
            Return value
        End Function

        ''' <summary>Gaussian elimination with partial pivoting; Nothing when the matrix is singular.</summary>
        Private Shared Function SolveLinear(A As Double(,), b As Double()) As Double()
            Dim n = b.Length
            Dim m(n - 1, n) As Double
            For i = 0 To n - 1
                For j = 0 To n - 1 : m(i, j) = A(i, j) : Next
                m(i, n) = b(i)
            Next
            For c = 0 To n - 1
                Dim p = c
                For i = c + 1 To n - 1
                    If Math.Abs(m(i, c)) > Math.Abs(m(p, c)) Then p = i
                Next
                If Math.Abs(m(p, c)) < 1.0E-300 Then Return Nothing
                If p <> c Then
                    For j = 0 To n : Dim t = m(c, j) : m(c, j) = m(p, j) : m(p, j) = t : Next
                End If
                For i = c + 1 To n - 1
                    Dim f = m(i, c) / m(c, c)
                    For j = c To n : m(i, j) -= f * m(c, j) : Next
                Next
            Next
            Dim x(n - 1) As Double
            For i = n - 1 To 0 Step -1
                Dim s = m(i, n)
                For j = i + 1 To n - 1 : s -= m(i, j) * x(j) : Next
                x(i) = s / m(i, i)
            Next
            Return x
        End Function

    End Class

End Namespace
