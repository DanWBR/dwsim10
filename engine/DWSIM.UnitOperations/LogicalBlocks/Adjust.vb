'    Adjust Calculation Routines 
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


Imports DWSIM.Thermodynamics
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.UnitOperations.Auxiliary
Imports DWSIM.Thermodynamics.BaseClasses
Imports DWSIM.Interfaces.Enums
Imports cv = DWSIM.SharedClasses.SystemsOfUnits.Converter

Namespace SpecialOps

    ''' <summary>
    ''' Represents an Adjust (controller) block that iterates a manipulated variable of one simulation
    ''' object until the controlled variable of another object reaches a specified target value.
    ''' Uses a bisection or secant numerical method with an optional reference object for relative targets.
    ''' </summary>
    <System.Serializable()> Public Partial Class Adjust

        Inherits UnitOperations.SpecialOpBaseClass

        Implements Interfaces.IAdjust

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        Protected m_ManipulatedObject As SharedClasses.UnitOperations.BaseClass
        Protected m_ControlledObject As SharedClasses.UnitOperations.BaseClass
        Protected m_ReferenceObject As SharedClasses.UnitOperations.BaseClass

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

        ''' <summary>Gets or sets the solving method index used by this block (0 = bisection, 1 = secant, etc.).</summary>
        Public Property SolvingMethodSelf As Integer = 0

        ''' <summary>Creates a deep copy of this adjust block via XML serialization.</summary>
        ''' <returns>A new <see cref="Adjust"/> instance with the same data.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Adjust()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this adjust block via JSON serialization.</summary>
        ''' <returns>A new <see cref="Adjust"/> instance with the same data.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Adjust)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Gets or sets whether this block participates in simultaneous adjustment with other Adjust blocks.</summary>
        Public Property SimultaneousAdjust() As Boolean Implements Interfaces.IAdjust.SimultaneousAdjust
            Get
                Return m_IsSimultAdjustEnabled
            End Get
            Set(ByVal value As Boolean)
                m_IsSimultAdjustEnabled = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional initial estimate for the manipulated variable used to seed the solver.</summary>
        Public Property InitialEstimate() As Nullable(Of Double)
            Get
                Return m_initialEstimate
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_initialEstimate = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional upper bound for the manipulated variable search range.</summary>
        Public Property MaxVal() As Nullable(Of Double)
            Get
                Return m_maxVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_maxVal = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional lower bound for the manipulated variable search range.</summary>
        Public Property MinVal() As Nullable(Of Double)
            Get
                Return m_minVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_minVal = value
            End Set
        End Property

        ''' <summary>Gets or sets whether the reference variable object and property are correctly configured.</summary>
        Public Property RvOk() As Boolean
            Get
                Return m_RV_OK
            End Get
            Set(ByVal value As Boolean)
                m_RV_OK = value
            End Set
        End Property

        ''' <summary>Gets or sets whether the manipulated variable object and property are correctly configured.</summary>
        Public Property MvOk() As Boolean
            Get
                Return m_MV_OK
            End Get
            Set(ByVal value As Boolean)
                m_MV_OK = value
            End Set
        End Property

        ''' <summary>Gets or sets whether the controlled variable object and property are correctly configured.</summary>
        Public Property CvOk() As Boolean
            Get
                Return m_CV_OK
            End Get
            Set(ByVal value As Boolean)
                m_CV_OK = value
            End Set
        End Property

        ''' <summary>Gets or sets the metadata describing the manipulated simulation object and its property.</summary>
        Public Property ManipulatedObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ManipulatedObjectData
            Get
                Return Me.m_ManipulatedObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ManipulatedObjectData = value
            End Set
        End Property

        ''' <summary>Gets or sets the metadata describing the controlled simulation object and its property.</summary>
        Public Property ControlledObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ControlledObjectData
            Get
                Return Me.m_ControlledObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ControlledObjectData = value
            End Set
        End Property

        ''' <summary>Gets or sets the metadata describing the optional reference simulation object and its property.</summary>
        Public Property ReferencedObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ReferencedObjectData
            Get
                Return Me.m_ReferencedObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ReferencedObjectData = value
            End Set
        End Property

        ''' <summary>Gets or sets the manipulated simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property ManipulatedObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ManipulatedObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ManipulatedObject = value
            End Set
        End Property

        ''' <summary>Gets or sets the controlled simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property ControlledObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ControlledObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ControlledObject = value
            End Set
        End Property

        ''' <summary>Gets or sets the optional reference simulation object instance (not serialized).</summary>
        <Xml.Serialization.XmlIgnore()> Public Property ReferenceObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ReferenceObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ReferenceObject = value
            End Set
        End Property

        ''' <summary>Gets or sets the property identifier of the variable to be manipulated by the solver.</summary>
        Public Property ManipulatedVariable() As String
            Get
                Return Me.m_ManipulatedVariable
            End Get
            Set(ByVal value As String)
                Me.m_ManipulatedVariable = value
            End Set
        End Property

        ''' <summary>Gets or sets the property identifier of the variable to be controlled (driven to the target value).</summary>
        Public Property ControlledVariable() As String
            Get
                Return Me.m_ControlledVariable
            End Get
            Set(ByVal value As String)
                Me.m_ControlledVariable = value
            End Set
        End Property

        ''' <summary>Gets or sets the property identifier of the optional reference variable used as a relative target basis.</summary>
        Public Property ReferenceVariable() As String
            Get
                Return Me.m_ReferenceVariable
            End Get
            Set(ByVal value As String)
                Me.m_ReferenceVariable = value
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

        ''' <summary>Gets or sets the target (setpoint) value for the controlled variable.</summary>
        Public Property AdjustValue() As Double Implements Interfaces.IAdjust.AdjustValue
            Get
                Return Me.m_AdjustValue
            End Get
            Set(ByVal value As Double)
                Me.m_AdjustValue = value
            End Set
        End Property

        ''' <summary>Gets or sets whether the target is expressed relative to the reference object's variable value.</summary>
        Public Property Referenced() As Boolean Implements Interfaces.IAdjust.Referenced
            Get
                Return Me.m_IsReferenced
            End Get
            Set(ByVal value As Boolean)
                Me.m_IsReferenced = value
            End Set
        End Property

        ''' <summary>Gets or sets the step size used by the initial perturbation of the manipulated variable.</summary>
        Public Property StepSize() As Double
            Get
                Return Me.m_StepSize
            End Get
            Set(ByVal value As Double)
                Me.m_StepSize = value
            End Set
        End Property

        ''' <summary>Gets or sets the convergence tolerance for the controlled variable error.</summary>
        Public Property Tolerance() As Double Implements IAdjust.Tolerance
            Get
                Return Me.m_Tolerance
            End Get
            Set(ByVal value As Double)
                Me.m_Tolerance = value
            End Set
        End Property

        ''' <summary>Gets or sets the maximum number of solver iterations allowed before declaring non-convergence.</summary>
        Public Property MaximumIterations() As Integer
            Get
                Return Me.m_MaxIterations
            End Get
            Set(ByVal value As Integer)
                Me.m_MaxIterations = value
            End Set
        End Property

        ''' <summary>
        ''' Restores the adjust block state from a list of XML elements.
        ''' </summary>
        ''' <param name="data">The list of <see cref="XElement"/> objects containing the serialized state.</param>
        ''' <returns><c>True</c> if the data was loaded successfully.</returns>
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

        ''' <summary>
        ''' Serializes the adjust block state to a list of XML elements for persistence.
        ''' </summary>
        ''' <returns>A list of <see cref="XElement"/> objects representing the current state.</returns>
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

        ''' <summary>Initializes a new default instance of the <see cref="Adjust"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Adjust"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the adjust block.</param>
        ''' <param name="description">A brief description of the block.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            m_ManipulatedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ControlledObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ReferencedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>
        ''' Returns the value of the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier or name string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>The property value as an <see cref="Object"/>.</returns>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
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

        ''' <summary>
        ''' Returns the list of property identifiers available for this adjust block.
        ''' </summary>
        ''' <param name="proptype">The type of properties to retrieve.</param>
        ''' <returns>An array of property identifier strings.</returns>
        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
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

        ''' <summary>
        ''' Sets the value of the specified property.
        ''' </summary>
        ''' <param name="prop">The property identifier or name string.</param>
        ''' <param name="propval">The new value to assign.</param>
        ''' <param name="su">The unit system of the supplied value; defaults to SI if not provided.</param>
        ''' <returns><c>True</c> if the property was set successfully.</returns>
        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then
                Return True
            End If

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

        ''' <summary>
        ''' Returns the unit string for the specified property (always an empty string for this block).
        ''' </summary>
        ''' <param name="prop">The property identifier string.</param>
        ''' <param name="su">The unit system to use; defaults to SI if not provided.</param>
        ''' <returns>A unit string or an empty string if the property has no units.</returns>
        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If
        End Function

        ''' <summary>Returns the raw bytes of the adjust block icon image resource.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.adjust.png")

        End Function

        ''' <summary>Returns the localized display description for the adjust block type.</summary>
        ''' <returns>A localized description string.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("ADJ_Desc")
        End Function

        ''' <summary>Returns the localized display name for the adjust block type.</summary>
        ''' <returns>A localized name string.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("ADJ_Name")
        End Function

        ''' <summary>Gets a value indicating whether this block is compatible with the DWSIM mobile interface.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Public Function PerformAdjust(solver As String, Optional minval As Double = Double.NaN, Optional maxval As Double = Double.NaN) As Double

            Dim su = GetFlowsheet().FlowsheetOptions.SelectedUnitSystem

            Dim mvVal, cvVal, rfVal As Double

            If GetFlowsheet().SimulationObjects(ControlledObjectData.ID).GraphicObject.Calculated Then
                cvVal = Me.GetCtlVarValue()
            End If
            If GetFlowsheet().SimulationObjects(ManipulatedObjectData.ID).GraphicObject.Calculated Then
                mvVal = Me.GetMnpVarValue()
            End If
            If Referenced Then
                If GetFlowsheet().SimulationObjects(ReferencedObjectData.ID).GraphicObject.Calculated Then
                    rfVal = Me.GetRefVarValue()
                End If
            End If
            Dim tol, maxit, adjval, stepsize, max, min As Double
            With Me
                If Referenced Then
                    If Not rfVal = Nothing Then
                        Dim punit = GetFlowsheet().SimulationObjects(ReferencedObjectData.ID).GetPropertyUnit(.ReferencedObjectData.PropertyName, su)
                        If su.GetUnitType(punit) = Enums.UnitOfMeasure.temperature Then
                            adjval = rfVal + cv.ConvertFromSI(punit & ".", .AdjustValue)
                        Else
                            adjval = rfVal + cv.ConvertFromSI(punit, .AdjustValue)
                        End If
                    Else
                        Return Double.NaN
                    End If
                Else
                    adjval = cv.ConvertFromSI(.ControlledObject.GetPropertyUnit(.ControlledObjectData.PropertyName, su), .AdjustValue)
                End If
                maxit = .MaximumIterations
                stepsize = .StepSize
                tol = .Tolerance.ConvertToSI(ControlledObject.GetPropertyUnit(ControlledObjectData.PropertyName, su))
                If Not Double.IsNaN(minval) And Not Double.IsNaN(maxval) Then
                    min = .MinVal.GetValueOrDefault.ConvertToSI(ManipulatedObject.GetPropertyUnit(ManipulatedObjectData.PropertyName, su))
                    max = .MaxVal.GetValueOrDefault.ConvertToSI(ManipulatedObject.GetPropertyUnit(ManipulatedObjectData.PropertyName, su))
                End If
            End With

            Dim fval As Double

            Dim cnt = 0

            Dim mvVal0 = mvVal

            Dim funcproc As Func(Of Double, Double) =
            Function(xval)

                Me.SetMnpVarValue(xval)

                DWSIM.FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(GetFlowsheet(), GlobalSettings.Settings.SolverMode)

                If Referenced Then
                    rfVal = Me.GetRefVarValue()
                    Dim punit = GetFlowsheet().SimulationObjects(ReferencedObjectData.ID).GetPropertyUnit(ReferencedObjectData.PropertyName, su)
                    If su.GetUnitType(punit) = Enums.UnitOfMeasure.temperature Then
                        adjval = rfVal + cv.ConvertFromSI(punit & ".", AdjustValue)
                    Else
                        adjval = rfVal + cv.ConvertFromSI(punit, AdjustValue)
                    End If
                End If

                cvVal = Me.GetCtlVarValue()

                fval = cvVal.ConvertToSI(ControlledObject.GetPropertyUnit(ControlledObjectData.PropertyName, su)) -
                       adjval.ConvertToSI(ControlledObject.GetPropertyUnit(ControlledObjectData.PropertyName, su))

                cnt += 1

                Return fval

            End Function

            Dim funcrestore = Sub(xvar As Double)
                                  Me.SetMnpVarValue(xvar)
                                  DWSIM.FlowsheetSolver.FlowsheetSolver.SolveFlowsheet(GetFlowsheet(), GlobalSettings.Settings.SolverMode)
                              End Sub

            If solver.ToLower() = "secant" Then

                Task.Factory.StartNew(Sub()
                                          mvVal = MathNet.Numerics.RootFinding.Secant.FindRoot(
                                        Function(xval)
                                            If Double.IsNaN(xval) Or Double.IsInfinity(xval) Then
                                                Return 1.0E+20
                                            Else
                                                Return funcproc.Invoke(xval)
                                            End If
                                        End Function, mvVal, mvVal * 1.01, min, max, tol, maxit)
                                      End Sub).ContinueWith(Sub(t)
                                                                If t.Exception IsNot Nothing Then
                                                                    funcrestore.Invoke(mvVal0)
                                                                    Throw t.Exception
                                                                End If
                                                            End Sub).GetAwaiter().GetResult()

            ElseIf solver.ToLower() = "brent" Then

                minval = minval.ConvertToSI(ManipulatedObject.GetPropertyUnit(ManipulatedObjectData.PropertyName, su))
                maxval = maxval.ConvertToSI(ManipulatedObject.GetPropertyUnit(ManipulatedObjectData.PropertyName, su))

                Task.Factory.StartNew(Sub()
                                          mvVal = MathNet.Numerics.RootFinding.Brent.FindRoot(
                                        Function(xval)
                                            Return funcproc.Invoke(xval)
                                        End Function, minval, maxval, tol, maxit)
                                      End Sub).ContinueWith(Sub(t)
                                                                If t.Exception IsNot Nothing Then
                                                                    funcrestore.Invoke(mvVal0)
                                                                    Throw t.Exception
                                                                End If
                                                            End Sub).GetAwaiter().GetResult()

            ElseIf solver.ToLower() = "newton" Then

                Dim nsolv As New DWSIM.MathOps.MathEx.Optimization.NewtonSolver()
                nsolv.EnableDamping = False
                nsolv.MaxIterations = maxit
                nsolv.Tolerance = tol ^ 2

                Task.Factory.StartNew(Sub()
                                          mvVal = nsolv.Solve(Function(xvars)
                                                                  Return New Double() {funcproc.Invoke(xvars(0))}
                                                              End Function, New Double() {mvVal})(0)
                                      End Sub).ContinueWith(Sub(t)
                                                                If t.Exception IsNot Nothing Then
                                                                    funcrestore.Invoke(mvVal0)
                                                                    Throw t.Exception
                                                                End If
                                                            End Sub).GetAwaiter().GetResult()

            ElseIf solver.ToLower() = "ipopt" Then

                Dim isolv As New DWSIM.MathOps.MathEx.Optimization.IPOPTSolver()
                isolv.MaxIterations = maxit
                isolv.Tolerance = tol

                Task.Factory.StartNew(Sub()
                                          mvVal = isolv.Solve(Function(xvars)
                                                                  Return funcproc.Invoke(xvars(0)) ^ 2
                                                              End Function, Nothing, New Double() {mvVal}, New Double() {minval}, New Double() {maxval})(0)
                                      End Sub).ContinueWith(Sub(t)
                                                                If t.Exception IsNot Nothing Then
                                                                    funcrestore.Invoke(mvVal0)
                                                                    Throw t.Exception
                                                                End If
                                                            End Sub).GetAwaiter().GetResult()

            End If

            Return fval

        End Function

        Private Function GetCtlVarValue()

            With ControlledObjectData
                Return GetFlowsheet().SimulationObjects(.ID).GetPropertyValue(.PropertyName, GetFlowsheet().FlowsheetOptions.SelectedUnitSystem)
            End With

        End Function

        Private Function GetMnpVarValue()

            With ManipulatedObjectData
                Return GetFlowsheet().SimulationObjects(.ID).GetPropertyValue(.PropertyName)
            End With

        End Function

        Private Function SetMnpVarValue(ByVal val As Nullable(Of Double))

            With ManipulatedObjectData
                GetFlowsheet().SimulationObjects(.ID).SetPropertyValue(.PropertyName, val)
            End With

            Return 1

        End Function

        Private Function GetRefVarValue()

            With ReferencedObjectData
                Return GetFlowsheet().SimulationObjects(.ID).GetPropertyValue(.PropertyName, GetFlowsheet().FlowsheetOptions.SelectedUnitSystem)
            End With

        End Function

    End Class

End Namespace




