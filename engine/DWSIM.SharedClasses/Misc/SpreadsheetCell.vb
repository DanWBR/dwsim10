'    Copyright 2008 Daniel Wagner O. de Medeiros
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
Imports System.IO
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Runtime.Serialization

''' <summary>
''' Provides cell and variable-type classes used by the embedded spreadsheet unit operation.
''' </summary>
Namespace Spreadsheet

    Public Enum VarType
        Read = 0
        Write = 1
        Expression = 2
        None = 3
        Unit = 4
    End Enum

    <System.Serializable()> Public Class SpreadsheetCellParameters

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Public CellType As VarType = VarType.Expression
        Public RelativeTolerance As Double = 0.01
        Public ObjectID As String = ""
        Public PropID As String = ""
        Public PropUnit As String = ""
        Public Expression As String = ""
        Public CurrVal As String = ""
        Public PrevVal As String = ""
        Public CalcOrder As Integer = 0
        Public References As List(Of String)
        Public ToolTipText As String = ""
        <Xml.Serialization.XmlIgnore> Public CellString As String = ""
        Public RawValue As Double = 0.0#

        Sub New()
            References = New List(Of String)
        End Sub

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim copy As New SpreadsheetCellParameters()
            copy.LoadData(SaveData())

            Return copy

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData
            XMLSerializer.XMLSerializer.Deserialize(Me, data, True)
            ToolTipText = Xml.XmlConvert.DecodeName(ToolTipText)
            Return True
        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData
            ToolTipText = Xml.XmlConvert.EncodeName(ToolTipText)
            If ToolTipText.Length > 65536 Then ToolTipText = Xml.XmlConvert.EncodeName("")
            If Expression <> "" Then
                Return XMLSerializer.XMLSerializer.Serialize(Me, True)
            Else
                Return New List(Of XElement)
            End If
        End Function
    End Class

End Namespace

