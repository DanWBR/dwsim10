'    Specification Calculation Routines 
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


Imports Flee.PublicTypes
Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports System.Linq

Namespace SpecialOps

    ''' <summary>
    ''' Represents a Spec (specification) block that copies a property from a source simulation object,
    ''' evaluates an optional mathematical expression using that value, and writes the result to a target
    ''' simulation object. Optionally clamps the result within configurable minimum/maximum bounds.
    ''' </summary>
    <System.Serializable()> Public Partial Class Spec

        Inherits UnitOperations.SpecialOpBaseClass

        Implements ISpec

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Holds the compiled expression between calculations.</summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Private _expressions As New ExpressionCache

        Protected m_SourceObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo
        Protected m_TargetObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo

        Protected m_CalculateTargetObject As Boolean = False

        Protected m_CV_OK As Boolean = False
        Protected m_MV_OK As Boolean = False

        Protected m_SourceObject As SharedClasses.UnitOperations.BaseClass
        Protected m_TargetObject As SharedClasses.UnitOperations.BaseClass

        Protected m_SourceVariable As String = ""
        Protected m_TargetVariable As String = ""

        Protected m_Expression As String = ""

        Protected m_Status As String = ""

        Protected m_minVal As Nullable(Of Double) = Nothing
        Protected m_maxVal As Nullable(Of Double) = Nothing

        <System.NonSerialized()> Protected m_e As IGenericExpression(Of Double)
        <System.NonSerialized()> Protected m_eopt As ExpressionContext

        <System.NonSerialized()> Protected formC As IFlowsheet
        Protected su As SystemsOfUnits.Units
        Protected cv As New SystemsOfUnits.Converter
        Protected nf As String = ""

        ''' <summary>Gets a value indicating whether this block supports dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Gets a value indicating whether this block exposes properties for dynamic mode.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        ''' <summary>Gets or sets the calculation mode that determines when this spec block is evaluated.</summary>
        Public Property SpecCalculationMode As SpecCalcMode2 = SpecCalcMode2.GlobalSetting Implements ISpec.SpecCalculationMode

        ''' <summary>Gets or sets the unique ID of the reference simulation object used for relative expressions.</summary>
        Public Property ReferenceObjectID As String = "" Implements ISpec.ReferenceObjectID

        ''' <summary>Creates a deep copy of this spec block via XML serialization.</summary>
        ''' <returns>A new <see cref="Spec"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Spec()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this spec block via JSON serialization.</summary>
        ''' <returns>A new <see cref="Spec"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Spec)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets whether the target simulation object should be recalculated after the specification is applied.</summary>
        Public Property CalculateTargetObject() As Boolean
            Get
                Return Me.m_CalculateTargetObject
            End Get
            Set(ByVal value As Boolean)
                Me.m_CalculateTargetObject = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional upper bound clamping value applied to the evaluated expression result.</summary>
        Public Property MaxVal() As Nullable(Of Double)
            Get
                Return m_maxVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_maxVal = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional lower bound clamping value applied to the evaluated expression result.</summary>
        Public Property MinVal() As Nullable(Of Double)
            Get
                Return m_minVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_minVal = value
            End Set
        End Property

        ''' <summary>Gets or sets the compiled generic expression object used to evaluate the specification formula (not serialized).</summary>
        Public Property Expr() As IGenericExpression(Of Double)
            Get
                Return m_e
            End Get
            Set(ByVal value As IGenericExpression(Of Double))
                m_e = value
            End Set
        End Property

        ''' <summary>Gets or sets the expression evaluation context providing variable bindings and math imports (not serialized).</summary>
        Public Property ExpContext() As ExpressionContext
            Get
                Return m_eopt
            End Get
            Set(ByVal value As ExpressionContext)
                m_eopt = value
            End Set
        End Property

        ''' <summary>Gets or sets a status message describing the outcome of the last calculation attempt.</summary>
        Public Property Status() As String
            Get
                Return Me.m_Status
            End Get
            Set(ByVal value As String)
                Me.m_Status = value
            End Set
        End Property

        ''' <summary>Gets or sets the mathematical expression string evaluated by this spec block (variables: X = source value, Y = target value).</summary>
        Public Property Expression() As String
            Get
                Return Me.m_Expression
            End Get
            Set(ByVal value As String)
                Me.m_Expression = value
            End Set
        End Property

        ''' <summary>Gets or sets the metadata describing the source simulation object and property whose value is read.</summary>
        Public Property SourceObjectData() As SpecialOps.Helpers.SpecialOpObjectInfo
            Get
                Return Me.m_SourceObjectData
            End Get
            Set(ByVal value As SpecialOps.Helpers.SpecialOpObjectInfo)
                Me.m_SourceObjectData = value
            End Set
        End Property

        ''' <summary>Gets or sets the metadata describing the target simulation object and property that receives the expression result.</summary>
        Public Property TargetObjectData() As SpecialOps.Helpers.SpecialOpObjectInfo
            Get
                Return Me.m_TargetObjectData
            End Get
            Set(ByVal value As SpecialOps.Helpers.SpecialOpObjectInfo)
                Me.m_TargetObjectData = value
            End Set
        End Property

        ''' <summary>Gets or sets the source simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property SourceObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_SourceObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_SourceObject = value
            End Set
        End Property

        ''' <summary>Gets or sets the target simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property TargetObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_TargetObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_TargetObject = value
            End Set
        End Property

        ''' <summary>
        ''' Restores the spec block state from a list of XML elements.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            MyBase.LoadData(data)

            Dim xel As XElement

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "SourceObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_SourceObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "TargetObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_TargetObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                End With

            End If

            Try
                Me.SourceObject = Me.FlowSheet.SimulationObjects(Me.SourceObjectData.ID)
                If Not Me.SourceObject Is Nothing Then Me.SourceObject.IsSpecAttached = True
            Catch ex As Exception

            End Try
            Try
                Me.TargetObject = Me.FlowSheet.SimulationObjects(Me.TargetObjectData.ID)
                If Not Me.TargetObject Is Nothing Then Me.TargetObject.IsSpecAttached = True
            Catch ex As Exception

            End Try
            Return True
        End Function

        ''' <summary>
        ''' Serializes the spec block state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            If m_SourceObjectData Is Nothing Then m_SourceObjectData = New Helpers.SpecialOpObjectInfo()

            If m_SourceObjectData.ID = Nothing Then m_SourceObjectData.ID = ""
            If m_SourceObjectData.Name = Nothing Then m_SourceObjectData.Name = ""
            If m_SourceObjectData.PropertyName = Nothing Then m_SourceObjectData.PropertyName = ""
            If m_SourceObjectData.ObjectType = Nothing Then m_SourceObjectData.ObjectType = ""

            If m_TargetObjectData Is Nothing Then m_TargetObjectData = New Helpers.SpecialOpObjectInfo()

            If m_TargetObjectData.ID = Nothing Then m_TargetObjectData.ID = ""
            If m_TargetObjectData.Name = Nothing Then m_TargetObjectData.Name = ""
            If m_TargetObjectData.PropertyName = Nothing Then m_TargetObjectData.PropertyName = ""
            If m_TargetObjectData.ObjectType = Nothing Then m_TargetObjectData.ObjectType = ""

            With elements
                .Add(New XElement("SourceObjectData", New XAttribute("ID", m_SourceObjectData.ID),
                                  New XAttribute("Name", m_SourceObjectData.Name),
                                  New XAttribute("Property", m_SourceObjectData.PropertyName),
                                  New XAttribute("Type", m_SourceObjectData.ObjectType)))
                .Add(New XElement("TargetObjectData", New XAttribute("ID", m_TargetObjectData.ID),
                                  New XAttribute("Name", m_TargetObjectData.Name),
                                  New XAttribute("Property", m_TargetObjectData.PropertyName),
                                  New XAttribute("Type", m_TargetObjectData.ObjectType)))
            End With

            Return elements

        End Function

        ''' <summary>Initializes a new default instance of the <see cref="Spec"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Spec"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the spec block.</param>
        ''' <param name="description">A brief description of the block.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()

            m_SourceObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_TargetObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo

            m_eopt = New ExpressionContext
            With m_eopt
                .Imports.AddType(GetType(System.Math))
            End With

            Me.ComponentName = name
            Me.ComponentDescription = description



            '// Define the context of our expression
            'ExpressionContext context = new ExpressionContext();
            '// Import all members of the Math type into the default namespace
            'context.Imports.ImportStaticMembers(typeof(Math));

            '// Define an int variable
            'context.Variables.DefineVariable(Flowsheet.GetTranslatedString("a"), typeof(int));
            'context.Variables.SetVariableValue(Flowsheet.GetTranslatedString("a"), 100);

            '// Create a dynamic expression that evaluates to an Object
            'IDynamicExpression eDynamic = ExpressionFactory.CreateDynamic("sqrt(a) + 1", context);
            '// Create a generic expression that evaluates to a double
            'IGenericExpression<double> eGeneric = ExpressionFactory.CreateGeneric<double>("sqrt(a) + 1", context);

            '// Evaluate the expressions
            'double result = (double)eDynamic.Evaluate();
            'result = eGeneric.Evaluate();

            '// Update the value of our variable
            'context.Variables.SetVariableValue(Flowsheet.GetTranslatedString("a"), 144);
            '// Evaluate again to get the updated result
            'result = eGeneric.Evaluate();

        End Sub

        ''' <summary>
        ''' Returns the current value of the target variable in the active unit system.
        ''' </summary>
        ''' <returns>The target property value, or <c>Nothing</c> if the target object is not configured.</returns>
        Public Function GetTargetVarValue()

            formC = Me.FlowSheet
            Me.su = formC.FlowsheetOptions.SelectedUnitSystem
            Me.nf = formC.FlowsheetOptions.NumberFormat

            If Not Me.TargetObjectData Is Nothing Then

                If formC.SimulationObjects.ContainsKey(Me.TargetObjectData.ID) Then

                    With Me.TargetObjectData
                        Return Me.formC.SimulationObjects(.ID).GetPropertyValue(.PropertyName, su)
                    End With

                Else

                    Return Nothing

                End If

            Else

                Return Nothing

            End If


        End Function

        ''' <summary>
        ''' Returns the current value of the source variable in the active unit system.
        ''' </summary>
        ''' <returns>The source property value, or <c>Nothing</c> if the source object is not configured.</returns>
        Public Function GetSourceVarValue()

            formC = Me.FlowSheet
            Me.su = formC.FlowsheetOptions.SelectedUnitSystem
            Me.nf = formC.FlowsheetOptions.NumberFormat

            If Not Me.SourceObjectData Is Nothing Then

                If formC.SimulationObjects.ContainsKey(Me.SourceObjectData.ID) Then

                    With Me.SourceObjectData
                        Return Me.formC.SimulationObjects(.ID).GetPropertyValue(.PropertyName, su)
                    End With

                Else

                    Return Nothing

                End If


            Else

                Return Nothing

            End If
        End Function

        ''' <summary>
        ''' Writes a value to the target variable of the configured target simulation object.
        ''' </summary>
        ''' <param name="val">The value to assign, expressed in the active unit system.</param>
        ''' <returns>1 if the value was written successfully, 0 if the target object was not found.</returns>
        Public Function SetTargetVarValue(ByVal val As Nullable(Of Double))

            formC = Me.FlowSheet
            Me.su = formC.FlowsheetOptions.SelectedUnitSystem
            Me.nf = formC.FlowsheetOptions.NumberFormat

            If Not Me.TargetObjectData Is Nothing Then

                If formC.SimulationObjects.ContainsKey(Me.TargetObjectData.ID) Then

                    With Me.TargetObjectData
                        Return Me.formC.SimulationObjects(.ID).SetPropertyValue(.PropertyName, val, su)
                    End With

                Else

                    Return 0

                End If

            Else

                Return 0

            End If

            Return 1

        End Function

        ''' <summary>Returns the unit string for the target variable in the active unit system.</summary>
        ''' <returns>A unit string for the target property.</returns>
        Public Function GetTargetVarUnit()

            Return Me.FlowSheet.SimulationObjects(Me.TargetObjectData.ID).GetPropertyUnit(Me.TargetObjectData.PropertyName, Me.FlowSheet.FlowsheetOptions.SelectedUnitSystem)

        End Function

        ''' <summary>Returns the unit string for the source variable in the active unit system.</summary>
        ''' <returns>A unit string for the source property.</returns>
        Public Function GetSourceVarUnit()

            Return Me.FlowSheet.SimulationObjects(Me.SourceObjectData.ID).GetPropertyUnit(Me.SourceObjectData.PropertyName, Me.FlowSheet.FlowsheetOptions.SelectedUnitSystem)

        End Function

        ''' <summary>
        ''' Compiles and evaluates the expression string using the current source (X) and target (Y) variable values.
        ''' </summary>
        ''' <returns>The numeric result of evaluating the expression.</returns>
        Public Function ParseExpression() As Double

            Dim context = _expressions.GetContext("XY")

            ExpressionCache.SetVariable(context, "X", Double.Parse(Me.GetSourceVarValue))
            ExpressionCache.SetVariable(context, "Y", Double.Parse(Me.GetTargetVarValue))

            Return _expressions.GetCompiled("XY", Me.Expression).Evaluate

        End Function

        ''' <summary>
        ''' Evaluates the expression and writes the result to the target simulation object's property.
        ''' Clamps the result within <see cref="MinVal"/> and <see cref="MaxVal"/> if both are set.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Me.GraphicObject.Active Then

                If Not Me.GetSourceVarValue Is Nothing And Not Me.GetTargetVarValue Is Nothing Then

                    Try

                        With Me

                            Dim context = _expressions.GetContext("XY")

                            ExpressionCache.SetVariable(context, "X", Double.Parse(.GetSourceVarValue))
                            ExpressionCache.SetVariable(context, "Y", Double.Parse(.GetTargetVarValue))

                            Dim val = _expressions.GetCompiled("XY", .Expression).Evaluate

                            If Not Me.MaxVal.HasValue And Not Me.MinVal.HasValue Then
                                Me.SetTargetVarValue(val)
                            Else
                                If val < Me.MinVal.Value Then
                                    Me.SetTargetVarValue(Me.MinVal.Value)
                                ElseIf val > Me.MaxVal.Value Then
                                    Me.SetTargetVarValue(Me.MaxVal.Value)
                                Else
                                    Me.SetTargetVarValue(val)
                                End If
                                Exit Sub
                            End If

                        End With

                    Catch ex As Exception

                        If GraphicObject IsNot Nothing Then

                            Throw New Exception(GraphicObject.Tag + ": error parsing expression - " + ex.Message)

                        Else

                            Throw New Exception("Spec Logical Op: error parsing expression - " + ex.Message)

                        End If


                    End Try

                    Me.GraphicObject.Calculated = True

                Else

                    Me.GraphicObject.Calculated = True
                    Throw New Exception(FlowSheet.GetTranslatedString("Existeumerronaconfig"))

                End If


            End If


        End Sub

        ''' <summary>
        ''' Returns the value of the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Select Case prop
                    Case "SpecMin"
                        Return MinVal.GetValueOrDefault
                    Case "SpecMax"
                        Return MaxVal.GetValueOrDefault
                    Case "Expression"
                        Return Expression
                    Case Else
                        Return Nothing
                End Select
            End If
        End Function

        ''' <summary>
        ''' Returns the list of property identifiers available for this spec block.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            proplist.Add("SpecMin")
            proplist.Add("SpecMax")
            proplist.Add("Expression")
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        ''' <summary>
        ''' Sets the value of the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Select Case prop
                Case "SpecMin"
                    MinVal = propval
                Case "SpecMax"
                    MaxVal = propval
                Case "Expression"
                    Expression = propval
            End Select
            Return True
        End Function

        ''' <summary>
        ''' Returns the unit string for the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A unit string, or an empty string if the property has no units.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If
        End Function

        ''' <summary>Returns the raw bytes of the spec block icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.spec.png")

        End Function

        ''' <summary>Returns the localized display description for the spec block type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("SPEC_Desc")
        End Function

        ''' <summary>Returns the localized display name for the spec block type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("SPEC_Name")
        End Function

        ''' <summary>Gets a value indicating whether this block is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace




