Imports System.IO
Imports System.Runtime.Serialization.Formatters.Binary
Imports System.Runtime.Serialization
Imports System.Globalization
Imports System.Reflection

'    Petroleum Assay Class
'    Copyright 2012-2026 Daniel Wagner O. de Medeiros
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

''' <summary>
''' Contains the petroleum assay class for managing distillation curve and bulk property data
''' used in petroleum characterization workflows.
''' </summary>
Namespace Utilities.PetroleumCharacterization.Assay

    <System.Serializable()> Public Class Assay

        Implements ICloneable, Interfaces.ICustomXMLSerialization

        Private _name As String = ""
        Private _isbulk As Boolean = False
        Private _iscurve As Boolean = False

        'Bulk fields
        Private _mw As Double
        Private _sg60 As Double
        Private _nbpavg As Double
        Private _t1 As Double
        Private _t2 As Double
        Private _v1 As Double
        Private _v2 As Double

        'Curve fields
        Private _nbptype As Integer = 0
        Private _hasmwcurve As Boolean = False
        Private _hassgcurve As Boolean = False
        Private _sgcurvetype As String = "SG20"
        Private _hasvisccurves As Boolean = False
        Private _curvebasis As String = ""
        Private _api As Double
        Private _k_api As Double
        Private _px As ArrayList
        Private _py_nbp As ArrayList
        Private _py_mw As ArrayList
        Private _py_sg As ArrayList
        Private _py_v1 As ArrayList
        Private _py_v2 As ArrayList

        'Bulk contaminant totals (whole crude / whole assay)
        Private _bulk_wtpct_s As Double = 0.0
        Private _bulk_wtpct_n As Double = 0.0
        Private _bulk_ni_ppm As Double = 0.0
        Private _bulk_v_ppm As Double = 0.0
        Private _bulk_fe_ppm As Double = 0.0
        Private _bulk_na_ppm As Double = 0.0
        Private _bulk_ccr_wtpct As Double = 0.0
        Private _bulk_asphaltenes_wtpct As Double = 0.0
        Private _bulk_tan As Double = 0.0
        Private _bulk_mercaptan_s_wtpct As Double = 0.0
        Private _bsw_vol_pct As Double = 0.0
        Private _salt_ptb As Double = 0.0

        'Optional per-NBP contaminant curves (share _px as the cut-basis grid)
        Private _has_s_curve As Boolean = False
        Private _has_n_curve As Boolean = False
        Private _has_metals_curve As Boolean = False
        Private _has_ccr_curve As Boolean = False
        Private _py_s As ArrayList
        Private _py_n As ArrayList
        Private _py_ni As ArrayList
        Private _py_v As ArrayList
        Private _py_ccr As ArrayList

        'Constructors

        Sub New(ByVal k_api As Double, ByVal mw As Double, ByVal api As Double, ByVal t1 As Double, ByVal t2 As Double, ByVal nbptype As Integer, ByVal sgtype As String, ByVal px As ArrayList, ByVal pynbp As ArrayList, ByVal pymw As ArrayList, ByVal pysg As ArrayList, ByVal pyv1 As ArrayList, ByVal pyv2 As ArrayList)
            Me.New()
            _k_api = k_api
            _mw = mw
            _api = api
            _t1 = t1
            _t2 = t2
            _nbptype = nbptype
            _sgcurvetype = sgtype
            _px = px
            _py_nbp = pynbp
            If pymw.Count = 0 Then _hasmwcurve = False Else _hasmwcurve = True
            _py_mw = pymw
            If pysg.Count = 0 Then _hassgcurve = False Else _hassgcurve = True
            _py_sg = pysg
            If pyv1.Count = 0 Then _hasvisccurves = False Else _hasvisccurves = True
            _py_v1 = pyv1
            _py_v2 = pyv2
            _iscurve = True
        End Sub

        Sub New(ByVal mw As Double, ByVal sg60 As Double, ByVal nbpavg As Double, ByVal t1 As Double, ByVal t2 As Double, ByVal v1 As Double, ByVal v2 As Double)
            Me.New()
            _mw = mw
            _sg60 = sg60
            _nbpavg = nbpavg
            _t1 = t1
            _t2 = t2
            _v1 = v1
            _v2 = v2
            _isbulk = True
        End Sub

        Sub New()
            _px = New ArrayList
            _py_nbp = New ArrayList
            _py_mw = New ArrayList
            _py_sg = New ArrayList
            _py_v1 = New ArrayList
            _py_v2 = New ArrayList
            _py_s = New ArrayList
            _py_n = New ArrayList
            _py_ni = New ArrayList
            _py_v = New ArrayList
            _py_ccr = New ArrayList
        End Sub

        'Properties

        Public Property CurveBasis() As String
            Get
                Return _curvebasis
            End Get
            Set(ByVal value As String)
                _curvebasis = value
            End Set
        End Property

        Public Property PY_V2() As ArrayList
            Get
                Return _py_v2
            End Get
            Set(ByVal value As ArrayList)
                _py_v2 = value
            End Set
        End Property

        Public Property PY_V1() As ArrayList
            Get
                Return _py_v1
            End Get
            Set(ByVal value As ArrayList)
                _py_v1 = value
            End Set
        End Property

        Public Property PY_SG() As ArrayList
            Get
                Return _py_sg
            End Get
            Set(ByVal value As ArrayList)
                _py_sg = value
            End Set
        End Property

        Public Property PY_MW() As ArrayList
            Get
                Return _py_mw
            End Get
            Set(ByVal value As ArrayList)
                _py_mw = value
            End Set
        End Property

        Public Property PY_NBP() As ArrayList
            Get
                Return _py_nbp
            End Get
            Set(ByVal value As ArrayList)
                _py_nbp = value
            End Set
        End Property

        Public Property PX() As ArrayList
            Get
                Return _px
            End Get
            Set(ByVal value As ArrayList)
                _px = value
            End Set
        End Property

        Public Property K_API() As Double
            Get
                Return _k_api
            End Get
            Set(ByVal value As Double)
                _k_api = value
            End Set
        End Property

        Public Property API() As Double
            Get
                Return _api
            End Get
            Set(ByVal value As Double)
                _api = value
            End Set
        End Property

        Public Property SGCurveType() As String
            Get
                Return _sgcurvetype
            End Get
            Set(ByVal value As String)
                _sgcurvetype = value
            End Set
        End Property

        Public Property HasViscCurves() As Boolean
            Get
                Return _hasvisccurves
            End Get
            Set(ByVal value As Boolean)
                _hasvisccurves = value
            End Set
        End Property

        Public Property HasSGCurve() As Boolean
            Get
                Return _hassgcurve
            End Get
            Set(ByVal value As Boolean)
                _hassgcurve = value
            End Set
        End Property

        Public Property HasMWCurve() As Boolean
            Get
                Return _hasmwcurve
            End Get
            Set(ByVal value As Boolean)
                _hasmwcurve = value
            End Set
        End Property

        Public Property NBPType() As Integer
            Get
                Return _nbptype
            End Get
            Set(ByVal value As Integer)
                _nbptype = value
            End Set
        End Property

        Public Property V2() As Double
            Get
                Return _v2
            End Get
            Set(ByVal value As Double)
                _v2 = value
            End Set
        End Property

        Public Property V1() As Double
            Get
                Return _v1
            End Get
            Set(ByVal value As Double)
                _v1 = value
            End Set
        End Property

        Public Property T2() As Double
            Get
                Return _t2
            End Get
            Set(ByVal value As Double)
                _t2 = value
            End Set
        End Property

        Public Property T1() As Double
            Get
                Return _t1
            End Get
            Set(ByVal value As Double)
                _t1 = value
            End Set
        End Property

        Public Property NBPAVG() As Double
            Get
                Return _nbpavg
            End Get
            Set(ByVal value As Double)
                _nbpavg = value
            End Set
        End Property

        Public Property SG60() As Double
            Get
                Return _sg60
            End Get
            Set(ByVal value As Double)
                _sg60 = value
            End Set
        End Property

        Public Property MW() As Double
            Get
                Return _mw
            End Get
            Set(ByVal value As Double)
                _mw = value
            End Set
        End Property

        Public Property IsCurve() As Boolean
            Get
                Return _iscurve
            End Get
            Set(ByVal value As Boolean)
                _iscurve = value
            End Set
        End Property

        Public Property IsBulk() As Boolean
            Get
                Return _isbulk
            End Get
            Set(ByVal value As Boolean)
                _isbulk = value
            End Set
        End Property

        Public Property Name() As String
            Get
                Return _name
            End Get
            Set(ByVal value As String)
                _name = value
            End Set
        End Property

        ' Bulk contaminant totals

        Public Property BulkSulfurWtPct() As Double
            Get
                Return _bulk_wtpct_s
            End Get
            Set(ByVal value As Double)
                _bulk_wtpct_s = value
            End Set
        End Property

        Public Property BulkNitrogenWtPct() As Double
            Get
                Return _bulk_wtpct_n
            End Get
            Set(ByVal value As Double)
                _bulk_wtpct_n = value
            End Set
        End Property

        Public Property BulkMercaptanSulfurWtPct() As Double
            Get
                Return _bulk_mercaptan_s_wtpct
            End Get
            Set(ByVal value As Double)
                _bulk_mercaptan_s_wtpct = value
            End Set
        End Property

        Public Property BulkNiPpm() As Double
            Get
                Return _bulk_ni_ppm
            End Get
            Set(ByVal value As Double)
                _bulk_ni_ppm = value
            End Set
        End Property

        Public Property BulkVPpm() As Double
            Get
                Return _bulk_v_ppm
            End Get
            Set(ByVal value As Double)
                _bulk_v_ppm = value
            End Set
        End Property

        Public Property BulkFePpm() As Double
            Get
                Return _bulk_fe_ppm
            End Get
            Set(ByVal value As Double)
                _bulk_fe_ppm = value
            End Set
        End Property

        Public Property BulkNaPpm() As Double
            Get
                Return _bulk_na_ppm
            End Get
            Set(ByVal value As Double)
                _bulk_na_ppm = value
            End Set
        End Property

        Public Property BulkCCRWtPct() As Double
            Get
                Return _bulk_ccr_wtpct
            End Get
            Set(ByVal value As Double)
                _bulk_ccr_wtpct = value
            End Set
        End Property

        Public Property BulkAsphaltenesWtPct() As Double
            Get
                Return _bulk_asphaltenes_wtpct
            End Get
            Set(ByVal value As Double)
                _bulk_asphaltenes_wtpct = value
            End Set
        End Property

        Public Property BulkTAN() As Double
            Get
                Return _bulk_tan
            End Get
            Set(ByVal value As Double)
                _bulk_tan = value
            End Set
        End Property

        Public Property BSWVolPct() As Double
            Get
                Return _bsw_vol_pct
            End Get
            Set(ByVal value As Double)
                _bsw_vol_pct = value
            End Set
        End Property

        Public Property SaltPTB() As Double
            Get
                Return _salt_ptb
            End Get
            Set(ByVal value As Double)
                _salt_ptb = value
            End Set
        End Property

        ' Per-NBP contaminant curves (aligned with PX)

        Public Property HasSulfurCurve() As Boolean
            Get
                Return _has_s_curve
            End Get
            Set(ByVal value As Boolean)
                _has_s_curve = value
            End Set
        End Property

        Public Property HasNitrogenCurve() As Boolean
            Get
                Return _has_n_curve
            End Get
            Set(ByVal value As Boolean)
                _has_n_curve = value
            End Set
        End Property

        Public Property HasMetalsCurve() As Boolean
            Get
                Return _has_metals_curve
            End Get
            Set(ByVal value As Boolean)
                _has_metals_curve = value
            End Set
        End Property

        Public Property HasCCRCurve() As Boolean
            Get
                Return _has_ccr_curve
            End Get
            Set(ByVal value As Boolean)
                _has_ccr_curve = value
            End Set
        End Property

        Public Property PY_Sulfur() As ArrayList
            Get
                Return _py_s
            End Get
            Set(ByVal value As ArrayList)
                _py_s = value
            End Set
        End Property

        Public Property PY_Nitrogen() As ArrayList
            Get
                Return _py_n
            End Get
            Set(ByVal value As ArrayList)
                _py_n = value
            End Set
        End Property

        Public Property PY_Ni() As ArrayList
            Get
                Return _py_ni
            End Get
            Set(ByVal value As ArrayList)
                _py_ni = value
            End Set
        End Property

        Public Property PY_V() As ArrayList
            Get
                Return _py_v
            End Get
            Set(ByVal value As ArrayList)
                _py_v = value
            End Set
        End Property

        Public Property PY_CCR() As ArrayList
            Get
                Return _py_ccr
            End Get
            Set(ByVal value As ArrayList)
                _py_ccr = value
            End Set
        End Property

        Public Function Clone() As Object Implements System.ICloneable.Clone

            Dim copy As New Assay()
            copy.LoadData(SaveData())

            Return copy

        End Function

        Public Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean Implements Interfaces.ICustomXMLSerialization.LoadData

            Return XMLSerializer.XMLSerializer.Deserialize(Me, data)

        End Function

        Public Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement) Implements Interfaces.ICustomXMLSerialization.SaveData

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

    End Class

End Namespace

