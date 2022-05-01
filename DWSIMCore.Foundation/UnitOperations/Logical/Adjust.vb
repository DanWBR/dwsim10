Imports DWSIMCore.Foundation.FlowsheetObjects

Namespace SpecialOps

    <System.Serializable()> Public Class Adjust

        Inherits UnitOperations.SpecialOpBaseClass

        Implements IAdjust

        Protected m_ManipulatedObject As BaseClass
        Protected m_ControlledObject As BaseClass
        Protected m_ReferenceObject As BaseClass

        Protected m_ManipulatedVariable As String = ""
        Protected m_ControlledVariable As String = ""
        Protected m_ReferenceVariable As String = ""

        Protected m_Status As String = ""

        Protected m_AdjustValue As Double = 1.0#

        Protected m_IsReferenced As Boolean = False
        Protected m_IsSimultAdjustEnabled As Boolean = False

        Protected m_StepSize As Double = 0.1
        Protected m_Tolerance As Double = 0.0001
        Protected m_MaxIterations As Integer = 10

        Protected m_ManipulatedObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo
        Protected m_ControlledObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo
        Protected m_ReferencedObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo

        Protected m_CV_OK As Boolean = False
        Protected m_MV_OK As Boolean = False
        Protected m_RV_OK As Boolean = False

        Protected m_minVal As Nullable(Of Double) = Nothing
        Protected m_maxVal As Nullable(Of Double) = Nothing
        Protected m_initialEstimate As Nullable(Of Double) = Nothing

        Public Property SolvingMethodSelf As Integer = 0

        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Adjust()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Adjust)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Property SimultaneousAdjust() As Boolean Implements IAdjust.SimultaneousAdjust
            Get
                Return m_IsSimultAdjustEnabled
            End Get
            Set(ByVal value As Boolean)
                m_IsSimultAdjustEnabled = value
            End Set
        End Property

        Public Property InitialEstimate() As Nullable(Of Double)
            Get
                Return m_initialEstimate
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_initialEstimate = value
            End Set
        End Property

        Public Property MaxVal() As Nullable(Of Double)
            Get
                Return m_maxVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_maxVal = value
            End Set
        End Property

        Public Property MinVal() As Nullable(Of Double)
            Get
                Return m_minVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_minVal = value
            End Set
        End Property

        Public Property RvOk() As Boolean
            Get
                Return m_RV_OK
            End Get
            Set(ByVal value As Boolean)
                m_RV_OK = value
            End Set
        End Property

        Public Property MvOk() As Boolean
            Get
                Return m_MV_OK
            End Get
            Set(ByVal value As Boolean)
                m_MV_OK = value
            End Set
        End Property

        Public Property CvOk() As Boolean
            Get
                Return m_CV_OK
            End Get
            Set(ByVal value As Boolean)
                m_CV_OK = value
            End Set
        End Property

        Public Property ManipulatedObjectData() As ISpecialOpObjectInfo Implements IAdjust.ManipulatedObjectData
            Get
                Return Me.m_ManipulatedObjectData
            End Get
            Set(ByVal value As ISpecialOpObjectInfo)
                Me.m_ManipulatedObjectData = value
            End Set
        End Property

        Public Property ControlledObjectData() As ISpecialOpObjectInfo Implements IAdjust.ControlledObjectData
            Get
                Return Me.m_ControlledObjectData
            End Get
            Set(ByVal value As ISpecialOpObjectInfo)
                Me.m_ControlledObjectData = value
            End Set
        End Property

        Public Property ReferencedObjectData() As ISpecialOpObjectInfo Implements IAdjust.ReferencedObjectData
            Get
                Return Me.m_ReferencedObjectData
            End Get
            Set(ByVal value As ISpecialOpObjectInfo)
                Me.m_ReferencedObjectData = value
            End Set
        End Property

        <Xml.Serialization.XmlIgnore()> Public Property ManipulatedObject() As BaseClass
            Get
                Return Me.m_ManipulatedObject
            End Get
            Set(ByVal value As BaseClass)
                Me.m_ManipulatedObject = value
            End Set
        End Property

        <Xml.Serialization.XmlIgnore()> Public Property ControlledObject() As BaseClass
            Get
                Return Me.m_ControlledObject
            End Get
            Set(ByVal value As BaseClass)
                Me.m_ControlledObject = value
            End Set
        End Property

        <Xml.Serialization.XmlIgnore()> Public Property ReferenceObject() As BaseClass
            Get
                Return Me.m_ReferenceObject
            End Get
            Set(ByVal value As BaseClass)
                Me.m_ReferenceObject = value
            End Set
        End Property

        Public Property ManipulatedVariable() As String
            Get
                Return Me.m_ManipulatedVariable
            End Get
            Set(ByVal value As String)
                Me.m_ManipulatedVariable = value
            End Set
        End Property

        Public Property ControlledVariable() As String
            Get
                Return Me.m_ControlledVariable
            End Get
            Set(ByVal value As String)
                Me.m_ControlledVariable = value
            End Set
        End Property

        Public Property ReferenceVariable() As String
            Get
                Return Me.m_ReferenceVariable
            End Get
            Set(ByVal value As String)
                Me.m_ReferenceVariable = value
            End Set
        End Property

        Public Property Status() As String
            Get
                Return Me.m_Status
            End Get
            Set(ByVal value As String)
                Me.m_Status = value
            End Set
        End Property

        Public Property AdjustValue() As Double Implements IAdjust.AdjustValue
            Get
                Return Me.m_AdjustValue
            End Get
            Set(ByVal value As Double)
                Me.m_AdjustValue = value
            End Set
        End Property

        Public Property Referenced() As Boolean Implements IAdjust.Referenced
            Get
                Return Me.m_IsReferenced
            End Get
            Set(ByVal value As Boolean)
                Me.m_IsReferenced = value
            End Set
        End Property

        Public Property StepSize() As Double
            Get
                Return Me.m_StepSize
            End Get
            Set(ByVal value As Double)
                Me.m_StepSize = value
            End Set
        End Property

        Public Property Tolerance() As Double Implements IAdjust.Tolerance
            Get
                Return Me.m_Tolerance
            End Get
            Set(ByVal value As Double)
                Me.m_Tolerance = value
            End Set
        End Property

        Public Property MaximumIterations() As Integer
            Get
                Return Me.m_MaxIterations
            End Get
            Set(ByVal value As Integer)
                Me.m_MaxIterations = value
            End Set
        End Property

        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)

            Dim xel As XElement

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ManipulatedObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_ManipulatedObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ControlledObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_ControlledObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ReferencedObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_ReferencedObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If
            Return True
        End Function

        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            If m_ManipulatedObjectData Is Nothing Then m_ManipulatedObjectData = New Helpers.SpecialOpObjectInfo()
            If m_ControlledObjectData Is Nothing Then m_ControlledObjectData = New Helpers.SpecialOpObjectInfo()
            If m_ReferencedObjectData Is Nothing Then m_ReferencedObjectData = New Helpers.SpecialOpObjectInfo()

            If m_ManipulatedObjectData.ObjectType = Nothing Then m_ManipulatedObjectData.ObjectType = ""
            If m_ControlledObjectData.ObjectType = Nothing Then m_ControlledObjectData.ObjectType = ""
            If m_ReferencedObjectData.ObjectType = Nothing Then m_ReferencedObjectData.ObjectType = ""

            With elements
                .Add(New XElement("ManipulatedObjectData", New XAttribute("ID", m_ManipulatedObjectData.ID),
                                  New XAttribute("Name", m_ManipulatedObjectData.Name),
                                  New XAttribute("Property", m_ManipulatedObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ManipulatedObjectData.ObjectType)))
                .Add(New XElement("ControlledObjectData", New XAttribute("ID", m_ControlledObjectData.ID),
                                  New XAttribute("Name", m_ControlledObjectData.Name),
                                  New XAttribute("Property", m_ControlledObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ControlledObjectData.ObjectType)))
                .Add(New XElement("ReferencedObjectData", New XAttribute("ID", m_ReferencedObjectData.ID),
                                  New XAttribute("Name", m_ReferencedObjectData.Name),
                                  New XAttribute("Property", m_ReferencedObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ReferencedObjectData.ObjectType)))
            End With

            Return elements

        End Function

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            m_ManipulatedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ControlledObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ReferencedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As Object
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Select Case prop
                    Case "MinVal"
                        Return MinVal.GetValueOrDefault
                    Case "MaxVal"
                        Return MaxVal.GetValueOrDefault
                    Case "AdjustValue"
                        Return AdjustValue
                    Case "Tolerance"
                        Return Tolerance
                    Case "StepSize"
                        Return StepSize
                    Case "MaximumIterations"
                        Return MaximumIterations
                    Case Else
                        Return Nothing
                End Select
            End If
        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            proplist.Add("MinVal")
            proplist.Add("MaxVal")
            proplist.Add("AdjustValue")
            proplist.Add("Tolerance")
            proplist.Add("StepSize")
            proplist.Add("MaximumIterations")
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Select Case prop
                Case "MinVal"
                    MinVal = propval
                Case "MaxVal"
                    MaxVal = propval
                Case "AdjustValue"
                    AdjustValue = propval
                Case "Tolerance"
                    Tolerance = propval
                Case "StepSize"
                    StepSize = propval
                Case "MaximumIterations"
                    MaximumIterations = propval
            End Select
            Return True
        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If
        End Function

        Public Overrides Function GetIconBitmap() As Object
            Using imgstream As IO.Stream = System.Reflection.Assembly.GetAssembly(Me.GetType).GetManifestResourceStream("DWSIMCore.Foundation.lo_adjust_32.png")
                Using bitmap = SkiaSharp.SKBitmap.Decode(imgstream)
                    Return SkiaSharp.SKImage.FromBitmap(bitmap)
                End Using
            End Using
        End Function

        Public Overrides Function GetDisplayDescription() As String
            Return "Controller"
        End Function

        Public Overrides Function GetDisplayName() As String
            Return "Controller"
        End Function

        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

End Namespace




