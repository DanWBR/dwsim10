'    Flowsheet Object Base Classes - the equation-oriented model of a simulation object
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

Imports DWSIM.Interfaces

Namespace UnitOperations

    Partial Public MustInherit Class BaseClass

        Implements IEquationOrientedModel

        ''' <summary>False: the equation-oriented solver takes the object as a black box. A unit with equations of its own returns True in the modes they cover.</summary>
        Public Overridable ReadOnly Property EOHasEquations As Boolean Implements IEquationOrientedModel.EOHasEquations
            Get
                Return False
            End Get
        End Property

        ''' <summary>Nothing: as a black box, the object's own calculation reads any of its properties.</summary>
        Public Overridable Function EOParameters() As String() Implements IEquationOrientedModel.EOParameters
            Return Nothing
        End Function

        Public Overridable Function EOEquationCount(context As IEquationOrientedContext) As Integer Implements IEquationOrientedModel.EOEquationCount
            Return context.BlackBoxEquationCount(Me)
        End Function

        Public Overridable Function EOVariables(context As IEquationOrientedContext) As Integer() Implements IEquationOrientedModel.EOVariables
            Return context.BlackBoxVariables(Me)
        End Function

        Public Overridable Sub EOResiduals(context As IEquationOrientedContext, x As Double(), r As Double(), row As Integer) Implements IEquationOrientedModel.EOResiduals
            context.BlackBoxResiduals(Me, x, r, row)
        End Sub

        ''' <summary>False: a black box is differenced by the solver.</summary>
        Public Overridable Function EOJacobian(context As IEquationOrientedContext, x As Double(), add As Action(Of Integer, Integer, Double)) As Boolean Implements IEquationOrientedModel.EOJacobian
            Return False
        End Function

        Public Overridable Function EOEquationName(context As IEquationOrientedContext, k As Integer) As String Implements IEquationOrientedModel.EOEquationName
            Return context.BlackBoxEquationName(Me, k)
        End Function

        Public Overridable Function EOIsSmooth(context As IEquationOrientedContext, x As Double(), k As Integer) As Boolean Implements IEquationOrientedModel.EOIsSmooth
            Return True
        End Function

        ''' <summary>The variable offset of the material stream on input connector port, or -1.</summary>
        Protected Function EOInletOffset(context As IEquationOrientedContext, port As Integer) As Integer
            Dim c = GraphicObject.InputConnectors(port)
            Return If(c.IsAttached, context.StreamOffset(c.AttachedConnector.AttachedFrom.Name), -1)
        End Function

        ''' <summary>The variable offset of the material stream on output connector port, or -1.</summary>
        Protected Function EOOutletOffset(context As IEquationOrientedContext, port As Integer) As Integer
            Dim c = GraphicObject.OutputConnectors(port)
            Return If(c.IsAttached, context.StreamOffset(c.AttachedConnector.AttachedTo.Name), -1)
        End Function

        ''' <summary>The variables of the streams at these offsets (T, P and the compound flows of each), then the parameters the solver moves.</summary>
        Protected Function EOStreamVariables(context As IEquationOrientedContext, offsets As Integer(), parameters As String()) As Integer()
            Dim v As New List(Of Integer)
            For Each o In offsets
                For j = 0 To context.CompoundCount + 1
                    v.Add(o + j)
                Next
            Next
            If parameters IsNot Nothing Then
                For Each p In parameters
                    Dim k = context.ParameterIndex(Me, p)
                    If k >= 0 Then v.Add(k)
                Next
            End If
            Return v.Distinct().ToArray()
        End Function

        ''' <summary>A parameter's value: the solver's variable when it moves it, else the value the object holds.</summary>
        Protected Function EOParameterValue(context As IEquationOrientedContext, x As Double(), propertyCode As String, value As Double) As Double
            Dim k = context.ParameterIndex(Me, propertyCode)
            Return If(k >= 0, x(k), value)
        End Function

    End Class

End Namespace
