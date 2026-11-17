'    Three-phase (vapor-liquid-liquid) stages of the rigorous column
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

Imports DWSIM.Thermodynamics.PropertyPackages
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.SolvingMethods

Namespace UnitOperations

    Partial Public MustInherit Class Column

        ''' <summary>
        ''' Stages whose liquid may split into two liquid phases (vapor-liquid-liquid equilibrium): empty for none, "all",
        ''' or a list of stage numbers and ranges counted from the top stage as 1, such as "18-26, 30". On those stages the
        ''' solvers use apparent K-values of the total liquid; a liquid side draw from them carries both liquids.
        ''' </summary>
        Public Property ThreePhaseStages As String = ""

        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _threePhaseParsed As String = Nothing
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _threePhaseAll As Boolean
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _threePhaseSet As HashSet(Of Integer)

        ''' <summary>Last liquid split of each three-phase stage, kept through a solution of the column.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _threePhaseCache As ThreePhaseStage.SplitCache

        ''' <summary>Forgets the liquid splits of the last solution (a new solution starts from the stability test).</summary>
        Public Sub ResetThreePhaseCache()
            _threePhaseCache = New ThreePhaseStage.SplitCache
        End Sub

        ''' <summary>True when the stage of index <paramref name="index"/> (0 for the top stage) may hold two liquids.</summary>
        Public Function IsThreePhaseStage(index As Integer) As Boolean
            Dim spec = If(ThreePhaseStages, "").Trim()
            If spec.Length = 0 Then Return False
            If spec <> _threePhaseParsed Then
                Dim all = spec.Equals("all", StringComparison.OrdinalIgnoreCase)
                Dim hs As New HashSet(Of Integer)
                If Not all Then
                    For Each part In spec.Split(New Char() {","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)
                        Dim p = part.Trim()
                        Dim dash = p.IndexOf("-"c)
                        Dim a, b As Integer
                        If dash > 0 Then
                            If Integer.TryParse(p.Substring(0, dash).Trim(), a) AndAlso Integer.TryParse(p.Substring(dash + 1).Trim(), b) Then
                                For k = Math.Min(a, b) To Math.Max(a, b)
                                    hs.Add(k)
                                Next
                            End If
                        ElseIf Integer.TryParse(p, a) Then
                            hs.Add(a)
                        End If
                    Next
                End If
                _threePhaseSet = hs
                _threePhaseAll = all
                _threePhaseParsed = spec
            End If
            Return _threePhaseAll OrElse _threePhaseSet.Contains(index + 1)
        End Function

        ''' <summary>Bubble point of the total liquid of a three-phase stage from the apparent K-values.</summary>
        Public Function StageBubbleTemperature(pp As PropertyPackage, index As Integer, x As Double(), P As Double, Tguess As Double, ByRef K As Double()) As Double
            If _threePhaseCache Is Nothing Then _threePhaseCache = New ThreePhaseStage.SplitCache
            Return ThreePhaseStage.BubbleTemperature(pp, x, P, Tguess, K, index, _threePhaseCache)
        End Function

        ''' <summary>True when any stage may hold two liquids.</summary>
        Public Function HasThreePhaseStages() As Boolean
            Return If(ThreePhaseStages, "").Trim().Length > 0
        End Function

        ''' <summary>K-values of a stage for the solvers: apparent ones on a three-phase stage, the package's otherwise.</summary>
        Public Function StageKvalues(pp As PropertyPackage, index As Integer, x As Double(), y As Double(), T As Double, P As Double) As Double()
            If IsThreePhaseStage(index) Then
                If _threePhaseCache Is Nothing Then _threePhaseCache = New ThreePhaseStage.SplitCache
                Return ThreePhaseStage.Kvalues(pp, x, y, T, P, index, _threePhaseCache)
            End If
            Return pp.DW_CalcKvalue(x, y, T, P)
        End Function

        ''' <summary>Liquid splits of the three-phase stages through a dynamic run.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _threePhaseDynCache As ThreePhaseStage.SplitCache

        ''' <summary>
        ''' Calculates the holdup stream of the stage of index <paramref name="index"/> (0 for the top stage) at the
        ''' flash spec it carries; on a three-phase stage the flash may give two liquids.
        ''' </summary>
        Friend Sub CalculateStageHoldup(st As Thermodynamics.Streams.MaterialStream, index As Integer)
            If index < 0 OrElse Not IsThreePhaseStage(index) Then
                st.Calculate()
                Return
            End If
            If _threePhaseDynCache Is Nothing Then _threePhaseDynCache = New ThreePhaseStage.SplitCache
            st.AssignSelfToPP()
            Dim pp = DirectCast(st.PropertyPackage, PropertyPackage)
            st.FlashOverride = Nothing
            Dim baseFlash = pp.FlashBase
            st.FlashOverride = New ThreePhaseHoldupFlash(baseFlash, index, _threePhaseDynCache)
            Try
                st.Calculate()
            Finally
                st.FlashOverride = Nothing
            End Try
        End Sub

        ''' <summary>
        ''' Puts the liquid of a holdup into <paramref name="target"/> (a liquid transfer or a liquid product): the first
        ''' liquid when there is one, both liquids together, at their mean enthalpy, when the holdup has two.
        ''' </summary>
        Friend Shared Sub AssignHoldupLiquid(target As Thermodynamics.Streams.MaterialStream, holdup As Thermodynamics.Streams.MaterialStream, SetW As Boolean)
            Dim l2 = holdup.Phases(4).Properties
            If l2.molarfraction.GetValueOrDefault() <= 0.0 Then
                target.AssignFromPhase(Interfaces.Enums.PhaseLabel.Liquid1, holdup, SetW)
                Return
            End If
            target.AssignFromPhase(Interfaces.Enums.PhaseLabel.LiquidMixture, holdup, SetW)
            Dim l1 = holdup.Phases(3).Properties
            Dim w1 = l1.massflow.GetValueOrDefault(), w2 = l2.massflow.GetValueOrDefault()
            Dim h1 = l1.enthalpy.GetValueOrDefault(), h2 = l2.enthalpy.GetValueOrDefault()
            If w1 + w2 > 0.0 AndAlso h1.IsValidDouble() AndAlso h2.IsValidDouble() Then target.SetMassEnthalpy((w1 * h1 + w2 * h2) / (w1 + w2))
        End Sub

    End Class

End Namespace
