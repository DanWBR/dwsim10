'    ChemSep column profiles of a CAPE-OPEN unit operation
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

Imports System.Globalization
Imports System.Text
Imports System.Text.RegularExpressions
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps

Namespace UnitOperations

    ''' <summary>
    ''' Stage profiles of a ChemSep column run as a CAPE-OPEN unit operation, read from the ChemSep input file the
    ''' unit keeps in its persisted data. Stage 1 is the top stage, as in ChemSep. Values are in SI units: K, Pa,
    ''' mol/s and W. Compositions and K-values are indexed [stage][compound], with the compounds in the order of
    ''' <see cref="Compounds"/>.
    ''' </summary>
    Public Class ChemSepColumnProfiles

        ''' <summary>Names of the compounds: the flowsheet names when they could be matched, otherwise the ChemSep names.</summary>
        Public Property Compounds As String() = New String() {}

        ''' <summary>Stage labels: "Condenser", "Reboiler" or "Stage n", with ChemSep's numbering from the top.</summary>
        Public Property StageNames As String() = New String() {}

        ''' <summary>Stage numbers as ChemSep counts them, 1 at the top.</summary>
        Public Property StageNumbers As Integer() = New Integer() {}

        ''' <summary>Stage temperatures, K.</summary>
        Public Property Temperature As Double() = New Double() {}

        ''' <summary>Stage pressures, Pa.</summary>
        Public Property Pressure As Double() = New Double() {}

        ''' <summary>Vapor leaving each stage, mol/s.</summary>
        Public Property VaporFlow As Double() = New Double() {}

        ''' <summary>Liquid leaving each stage, mol/s (side draws not included).</summary>
        Public Property LiquidFlow As Double() = New Double() {}

        ''' <summary>Heat duty of each stage, W.</summary>
        Public Property Duty As Double() = New Double() {}

        ''' <summary>Liquid side draw of each stage, mol/s.</summary>
        Public Property LiquidSideDraw As Double() = New Double() {}

        ''' <summary>Vapor side draw of each stage, mol/s.</summary>
        Public Property VaporSideDraw As Double() = New Double() {}

        ''' <summary>Liquid mole fractions, [stage][compound].</summary>
        Public Property X As Double()() = New Double()() {}

        ''' <summary>Vapor mole fractions, [stage][compound].</summary>
        Public Property Y As Double()() = New Double()() {}

        ''' <summary>K-values, [stage][compound].</summary>
        Public Property K As Double()() = New Double()() {}

        ''' <summary>Whether ChemSep reported the column as converged.</summary>
        Public Property Converged As Boolean

        ''' <summary>Iterations of the last ChemSep solution.</summary>
        Public Property Iterations As Integer

        ''' <summary>Number of stages.</summary>
        Public ReadOnly Property NumberOfStages As Integer
            Get
                Return Temperature.Length
            End Get
        End Property

        ''' <summary>
        ''' The profiles as comma-separated text, one row per stage: stage, temperature (K), pressure (Pa), vapor and
        ''' liquid flows (mol/s), duty (W), then the liquid and the vapor mole fraction of each compound.
        ''' </summary>
        Public Function ToCsv() As String
            Dim ci = CultureInfo.InvariantCulture
            Dim sb As New StringBuilder()
            Dim head As New List(Of String) From {"Stage", "T (K)", "P (Pa)", "V (mol/s)", "L (mol/s)", "Q (W)"}
            head.AddRange(Compounds.Select(Function(c) "x " & c))
            head.AddRange(Compounds.Select(Function(c) "y " & c))
            sb.AppendLine(String.Join(",", head.Select(Function(h) """" & h & """")))
            For i = 0 To NumberOfStages - 1
                Dim row As New List(Of String) From {StageNumbers(i).ToString(ci), Temperature(i).ToString("R", ci), Pressure(i).ToString("R", ci),
                    VaporFlow(i).ToString("R", ci), LiquidFlow(i).ToString("R", ci), Duty(i).ToString("R", ci)}
                row.AddRange(X(i).Select(Function(v) v.ToString("R", ci)))
                row.AddRange(Y(i).Select(Function(v) v.ToString("R", ci)))
                sb.AppendLine(String.Join(",", row))
            Next
            Return sb.ToString()
        End Function

    End Class

    Partial Public Class CapeOpenUO

        ''' <summary>The persisted data read from the file, kept for the ChemSep profiles when the COM object isn't available.</summary>
        <NonSerialized> Private _loadedPersistedData As Byte()

        Private Const ChemSepMagic As String = "CSUO"

        ''' <summary>
        ''' Whether this CAPE-OPEN unit operation is a ChemSep column, from the selected component or its persisted data.
        ''' </summary>
        Public ReadOnly Property IsChemSepColumn As Boolean
            Get
                If _seluo IsNot Nothing AndAlso _seluo.Name IsNot Nothing AndAlso _seluo.Name.ToLower().Contains("chemsep") Then Return True
                Return HasChemSepMagic(If(_persisteddata, _loadedPersistedData))
            End Get
        End Property

        Private Shared Function HasChemSepMagic(data As Byte()) As Boolean
            Return data IsNot Nothing AndAlso data.Length > 8 AndAlso Encoding.ASCII.GetString(data, 0, 4) = ChemSepMagic
        End Function

        ''' <summary>
        ''' The ChemSep input file (.sep) of this unit, with the results of its last solution, or Nothing when the unit
        ''' isn't a ChemSep column. The text comes from the COM object when it is available (so it reflects the last
        ''' calculation), otherwise from the data loaded with the flowsheet.
        ''' </summary>
        Public Function GetChemSepFile() As String
            Dim data As Byte() = Nothing
            If _couo IsNot Nothing Then
                Try
                    PersistSave(Nothing)
                    data = _persisteddata
                Catch ex As Exception
                    data = Nothing
                End Try
            End If
            If Not HasChemSepMagic(data) Then data = If(HasChemSepMagic(_persisteddata), _persisteddata, _loadedPersistedData)
            If Not HasChemSepMagic(data) Then Return Nothing
            ' "CSUO", the length of the text as a little-endian Int32, then the .sep text in Latin-1
            Dim n = BitConverter.ToInt32(data, 4)
            If n <= 0 OrElse n > data.Length - 8 Then n = data.Length - 8
            Return Encoding.GetEncoding(28591).GetString(data, 8, n)
        End Function

        ''' <summary>
        ''' The stage profiles of the ChemSep column, read from the results ChemSep keeps in its input file.
        ''' Throws when the unit isn't a ChemSep column or hasn't been solved yet.
        ''' </summary>
        Public Function GetChemSepProfiles() As ChemSepColumnProfiles
            Dim text = GetChemSepFile()
            If text Is Nothing Then Throw New Exception(GraphicObject?.Tag & ": this CAPE-OPEN unit operation isn't a ChemSep column.")
            Dim names As List(Of String) = Nothing, cas As List(Of String) = Nothing
            If FlowSheet IsNot Nothing Then
                names = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.Name).ToList()
                cas = FlowSheet.SelectedCompounds.Values.Select(Function(c) c.CAS_Number).ToList()
            End If
            Return ParseChemSepProfiles(text, names, cas)
        End Function

        ''' <summary>
        ''' The ChemSep profiles held in a display-only distillation column, for the column profile windows. The column
        ''' isn't added to the flowsheet.
        ''' </summary>
        Public Function GetChemSepProfilesColumn() As DistillationColumn
            Dim p = GetChemSepProfiles()
            Dim col As New DistillationColumn()
            col.SetFlowsheet(FlowSheet)
            col.GraphicObject = GraphicObject
            col.Name = Name
            col.NumberOfStages = p.NumberOfStages
            col.Stages.Clear()
            For i = 0 To p.NumberOfStages - 1
                col.Stages.Add(New Stage(Guid.NewGuid().ToString()) With {.Name = p.StageNames(i), .T = p.Temperature(i), .P = p.Pressure(i)})
            Next
            col.T0 = p.Temperature.ToArray() : col.Tf = p.Temperature.ToArray()
            col.P0 = p.Pressure.ToArray()
            col.V0 = p.VaporFlow.ToArray() : col.Vf = p.VaporFlow.ToArray()
            col.L0 = p.LiquidFlow.ToArray() : col.Lf = p.LiquidFlow.ToArray()
            col.LSS0 = p.LiquidSideDraw.ToArray() : col.LSSf = p.LiquidSideDraw.ToArray()
            col.VSS0 = p.VaporSideDraw.ToArray() : col.VSSf = p.VaporSideDraw.ToArray()
            col.x0 = New ArrayList(p.X) : col.xf = New ArrayList(p.X)
            col.y0 = New ArrayList(p.Y) : col.yf = New ArrayList(p.Y)
            col.K0 = New ArrayList(p.K) : col.Kf = New ArrayList(p.K)
            col.compids = New ArrayList(p.Compounds)
            Return col
        End Function

        ''' <summary>
        ''' Reads the stage profiles from the text of a solved ChemSep input file. The compounds are matched to
        ''' <paramref name="flowsheetNames"/> by CAS number, then by name, then by position; without a flowsheet the
        ''' ChemSep names and order are kept.
        ''' </summary>
        Public Shared Function ParseChemSepProfiles(text As String, flowsheetNames As IList(Of String), flowsheetCAS As IList(Of String)) As ChemSepColumnProfiles

            Dim ci = CultureInfo.InvariantCulture

            ' ChemSep compounds, in its order
            Dim csNames As New List(Of String), csCAS As New List(Of String)
            Dim comps = Section(text, "Components")
            If comps IsNot Nothing Then
                For Each ln In Lines(comps)
                    Dim mc = Regex.Match(ln, "CAS=(\S+)")
                    If mc.Success Then csCAS.Add(mc.Groups(1).Value) : csNames.Add("")
                    If ln.StartsWith("Name=") AndAlso csNames.Count > 0 Then csNames(csNames.Count - 1) = ln.Substring(5).Trim()
                Next
            End If

            Dim prof = Section(text, "Profiles")
            If prof Is Nothing Then Throw New Exception("The ChemSep column doesn't have results yet. Solve it first.")

            Dim stages As New List(Of Integer), tList As New List(Of Double), pList As New List(Of Double), vList As New List(Of Double), lList As New List(Of Double), qList As New List(Of Double)
            For Each ln In Lines(prof)
                Dim f = Fields(ln)
                Dim st As Integer
                If f.Length >= 5 AndAlso Integer.TryParse(f(0), NumberStyles.Integer, ci, st) Then
                    stages.Add(st)
                    tList.Add(Num(f(1))) : pList.Add(Num(f(2))) : vList.Add(1000.0 * Num(f(3))) : lList.Add(1000.0 * Num(f(4)))
                    qList.Add(If(f.Length >= 6, Num(f(5)), 0.0))
                End If
            Next
            Dim ns = stages.Count
            If ns = 0 Then Throw New Exception("The ChemSep column doesn't have results yet. Solve it first.")

            Dim nccs = csNames.Count
            Dim xcs = Block(Section(text, "Liquid phase compositions"), stages, nccs)
            Dim ycs = Block(Section(text, "Vapour phase compositions"), stages, nccs)
            Dim kcs = Block(Section(text, "K-values"), stages, nccs)
            If nccs = 0 Then nccs = If(xcs.Count > 0, xcs.Values.First().Length, 0)

            ' ChemSep compound -> output position
            Dim outNames As List(Of String)
            Dim map(nccs - 1) As Integer
            If flowsheetNames IsNot Nothing AndAlso flowsheetNames.Count > 0 Then
                outNames = flowsheetNames.ToList()
                Dim used As New HashSet(Of Integer)
                For j = 0 To nccs - 1
                    map(j) = -1
                    If j < csCAS.Count AndAlso flowsheetCAS IsNot Nothing Then
                        For i = 0 To outNames.Count - 1
                            If Not used.Contains(i) AndAlso i < flowsheetCAS.Count AndAlso Not String.IsNullOrWhiteSpace(flowsheetCAS(i)) AndAlso
                                String.Equals(flowsheetCAS(i).Trim(), csCAS(j), StringComparison.OrdinalIgnoreCase) Then map(j) = i : Exit For
                        Next
                    End If
                    If map(j) < 0 AndAlso j < csNames.Count Then
                        For i = 0 To outNames.Count - 1
                            If Not used.Contains(i) AndAlso String.Equals(outNames(i), csNames(j), StringComparison.OrdinalIgnoreCase) Then map(j) = i : Exit For
                        Next
                    End If
                    If map(j) < 0 AndAlso nccs = outNames.Count AndAlso Not used.Contains(j) Then map(j) = j
                    If map(j) >= 0 Then used.Add(map(j))
                Next
            Else
                outNames = Enumerable.Range(0, nccs).Select(Function(j) If(j < csNames.Count AndAlso csNames(j) <> "", csNames(j), "Component " & (j + 1))).ToList()
                For j = 0 To nccs - 1 : map(j) = j : Next
            End If
            Dim nc = outNames.Count

            Dim reorder = Function(src As Dictionary(Of Integer, Double()), st As Integer) As Double()
                              Dim r(nc - 1) As Double
                              Dim v As Double() = Nothing
                              If src.TryGetValue(st, v) Then
                                  For j = 0 To Math.Min(v.Length, nccs) - 1
                                      If map(j) >= 0 Then r(map(j)) = v(j)
                                  Next
                              End If
                              Return r
                          End Function

            Dim outp As New ChemSepColumnProfiles With {
                .Compounds = outNames.ToArray(),
                .StageNumbers = stages.ToArray(),
                .Temperature = tList.ToArray(), .Pressure = pList.ToArray(),
                .VaporFlow = vList.ToArray(), .LiquidFlow = lList.ToArray(), .Duty = qList.ToArray(),
                .LiquidSideDraw = New Double(ns - 1) {}, .VaporSideDraw = New Double(ns - 1) {}}
            outp.X = stages.Select(Function(s) reorder(xcs, s)).ToArray()
            outp.Y = stages.Select(Function(s) reorder(ycs, s)).ToArray()
            If kcs.Count > 0 Then
                outp.K = stages.Select(Function(s) reorder(kcs, s)).ToArray()
            Else
                outp.K = Enumerable.Range(0, ns).Select(Function(i) Enumerable.Range(0, nc).Select(Function(j) If(outp.X(i)(j) > 0, outp.Y(i)(j) / outp.X(i)(j), 0.0)).ToArray()).ToArray()
            End If

            ' condenser and reboiler
            Dim op = Section(text, "Operation")
            Dim hasCondenser = op IsNot Nothing AndAlso Regex.IsMatch(op, "^\s*\d+\s+Condenser\s+(?!None)", RegexOptions.Multiline)
            Dim hasReboiler = op IsNot Nothing AndAlso Regex.IsMatch(op, "^\s*\d+\s+Reboiler\s+(?!None)", RegexOptions.Multiline)
            outp.StageNames = Enumerable.Range(0, ns).Select(Function(i)
                                                              If i = 0 AndAlso hasCondenser Then Return "Condenser"
                                                              If i = ns - 1 AndAlso hasReboiler Then Return "Reboiler"
                                                              Return "Stage " & stages(i)
                                                          End Function).ToArray()

            ' side draws: "[Side stream n from stage s]" blocks of the results
            For Each m As Match In Regex.Matches(text, "\[Side stream \d+ from stage (\d+)\][^\r\n]*\r?\n(.*?)(?=\r?\n\[)", RegexOptions.Singleline)
                Dim st = Integer.Parse(m.Groups(1).Value, ci)
                Dim i = stages.IndexOf(st)
                If i < 0 Then Continue For
                Dim flow = Double.NaN, vf = 0.0
                For Each ln In Lines(m.Groups(2).Value)
                    Dim f = Fields(ln)
                    If f.Length < 2 Then Continue For
                    If Double.IsNaN(flow) AndAlso ln.Contains("Flow rate") Then flow = 1000.0 * Num(f(0))
                    If ln.Contains("Vapour fraction") Then vf = Num(f(0))
                Next
                If Double.IsNaN(flow) Then Continue For
                If vf >= 0.5 Then outp.VaporSideDraw(i) += flow Else outp.LiquidSideDraw(i) += flow
            Next

            Dim resSec = Section(text, "Results")
            If resSec IsNot Nothing Then
                outp.Converged = Regex.IsMatch(resSec, "^\s*1\s+Converged", RegexOptions.Multiline)
                Dim mi = Regex.Match(resSec, "(\d+)\s+Iterations")
                If mi.Success Then outp.Iterations = Integer.Parse(mi.Groups(1).Value, ci)
            End If

            Return outp

        End Function

        ''' <summary>The body of a "[name]" section, up to the next section header, or Nothing.</summary>
        Private Shared Function Section(text As String, name As String) As String
            Dim m = Regex.Match(text, "^\[" & Regex.Escape(name) & "\][^\r\n]*\r?\n(.*?)(?=^\[)", RegexOptions.Singleline Or RegexOptions.Multiline)
            Return If(m.Success, m.Groups(1).Value, Nothing)
        End Function

        ''' <summary>
        ''' Reads a ChemSep per-component block ("Component ... on stages: a to b" headers followed by one row per
        ''' compound) into stage -> values in ChemSep's compound order.
        ''' </summary>
        Private Shared Function Block(body As String, stages As List(Of Integer), nc As Integer) As Dictionary(Of Integer, Double())
            Dim result As New Dictionary(Of Integer, Double())
            If body Is Nothing Then Return result
            Dim cur As New List(Of Integer)
            For Each ln In Lines(body)
                Dim mh = Regex.Match(ln, "stages:\s*(\d+)\s+to\s+(\d+)")
                If mh.Success Then
                    cur = Enumerable.Range(Integer.Parse(mh.Groups(1).Value), Integer.Parse(mh.Groups(2).Value) - Integer.Parse(mh.Groups(1).Value) + 1).ToList()
                    Continue For
                End If
                Dim f = Fields(ln)
                Dim j As Integer
                If cur.Count = 0 OrElse f.Length < 2 OrElse Not Integer.TryParse(f(0), NumberStyles.Integer, CultureInfo.InvariantCulture, j) Then Continue For
                j -= 1
                Dim width = Math.Max(nc, j + 1)
                For k = 0 To Math.Min(cur.Count, f.Length - 1) - 1
                    Dim v As Double() = Nothing
                    If Not result.TryGetValue(cur(k), v) Then v = New Double(width - 1) {} : result(cur(k)) = v
                    If j >= v.Length Then Array.Resize(v, j + 1) : result(cur(k)) = v
                    v(j) = Num(f(k + 1))
                Next
            Next
            Return result
        End Function

        Private Shared Function Lines(s As String) As String()
            Return s.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
        End Function

        Private Shared Function Fields(s As String) As String()
            Return s.Split(New Char() {" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
        End Function

        Private Shared Function Num(s As String) As Double
            Dim v As Double
            If Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then Return v
            Return Double.NaN
        End Function

    End Class

End Namespace
