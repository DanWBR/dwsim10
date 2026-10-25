'    Rigorous column: the dynamic state an integration step saves and restores
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

Imports DWSIM.Thermodynamics.Streams

Namespace UnitOperations

    Partial Public MustInherit Class Column

        ''' <summary>What the dynamic model of the column carries from one step to the next: the holdup of every
        ''' stage and of the sump, the stage levels, pressures, temperatures and flows, and the state of the
        ''' quasi-steady vapor sweep.</summary>
        Private Class ColumnDynamicState
            Public Base As Object
            Public Holdups As MaterialStream()
            Public Bottoms As MaterialStream
            Public P, T, Level, Vin, Vout, Lin, Lout As Double()
            Public QsExcess, QsVfilt As Double()
        End Class

        ''' <summary>The column keeps its holdups on the stages, which the base class does not see. Integration
        ''' methods that solve a step more than once (implicit Euler, step doubling, adaptive) restore this state
        ''' before each solve; without it the column advanced once per solve instead of once per step.</summary>
        Public Overrides Function SaveDynamicState() As Object

            Dim n = Stages.Count
            Dim st As New ColumnDynamicState With {
                .Base = MyBase.SaveDynamicState(),
                .Holdups = New MaterialStream(n - 1) {},
                .Bottoms = CopyHoldup(BottomsAccumulationStream),
                .P = New Double(n - 1) {}, .T = New Double(n - 1) {}, .Level = New Double(n - 1) {},
                .Vin = New Double(n - 1) {}, .Vout = New Double(n - 1) {}, .Lin = New Double(n - 1) {}, .Lout = New Double(n - 1) {},
                .QsExcess = _qsExcess?.ToArray(), .QsVfilt = _qsVfilt?.ToArray()}

            For i = 0 To n - 1
                Dim s = Stages(i)
                st.Holdups(i) = CopyHoldup(s.AccumulationStream)
                st.P(i) = s.P
                st.T(i) = s.T
                st.Level(i) = s.LiquidLevel
                st.Vin(i) = s.Vin.Value
                st.Vout(i) = s.Vout.Value
                st.Lin(i) = s.Lin.Value
                st.Lout(i) = s.Lout.Value
            Next

            Return st

        End Function

        Public Overrides Sub RestoreDynamicState(state As Object)

            Dim st = TryCast(state, ColumnDynamicState)
            If st Is Nothing Then
                MyBase.RestoreDynamicState(state)
                Exit Sub
            End If

            MyBase.RestoreDynamicState(st.Base)
            If st.Holdups.Length <> Stages.Count Then Exit Sub

            'copies again, so the saved state survives the solve that follows and can be restored once more
            For i = 0 To Stages.Count - 1
                Dim s = Stages(i)
                s.AccumulationStream = CopyHoldup(st.Holdups(i))
                s.P = st.P(i)
                s.T = st.T(i)
                s.LiquidLevel = st.Level(i)
                s.Vin.Value = st.Vin(i)
                s.Vout.Value = st.Vout(i)
                s.Lin.Value = st.Lin(i)
                s.Lout.Value = st.Lout(i)
            Next
            BottomsAccumulationStream = CopyHoldup(st.Bottoms)
            _qsExcess = st.QsExcess?.ToArray()
            _qsVfilt = st.QsVfilt?.ToArray()

        End Sub

        ''' <summary>The mass the column holds: every stage and the sump, in kg. NaN until the holdups are seeded. The
        ''' implicit Euler step reads it to decide whether the solves of a step have settled.</summary>
        Public Overrides Function GetDynamicContents() As Double
            If Stages.Count = 0 OrElse Stages.Any(Function(s) s.AccumulationStream Is Nothing) Then Return MyBase.GetDynamicContents()
            Dim m = Stages.Sum(Function(s) s.AccumulationStream.GetMassFlow())
            If BottomsAccumulationStream IsNot Nothing Then m += BottomsAccumulationStream.GetMassFlow()
            Return m
        End Function

        Private Function CopyHoldup(ms As MaterialStream) As MaterialStream
            If ms Is Nothing Then Return Nothing
            If ms.FlowSheet Is Nothing Then ms.SetFlowsheet(FlowSheet)
            If ms.PropertyPackage Is Nothing Then ms.PropertyPackage = PropertyPackage
            Return DirectCast(ms.CloneXML(), MaterialStream)
        End Function

    End Class

End Namespace
