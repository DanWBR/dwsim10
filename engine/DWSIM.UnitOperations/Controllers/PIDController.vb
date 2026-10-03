'    Copyright 2008-2020 Daniel Wagner O. de Medeiros
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
Imports DWSIM.SharedClasses
Imports DWSIM.UnitOperations.SpecialOps.Helpers
Imports OxyPlot
Imports OxyPlot.Axes
Imports OxyPlot.Series

Namespace SpecialOps

    ''' <summary>
    ''' Proportional-integral-derivative (PID) feedback controller for dynamic simulations. At each control
    ''' step it reads the controlled (process) variable of one flowsheet object, compares it with the
    ''' setpoint and writes a new value to the manipulated variable of another object. Supports manual
    ''' override, cascade control, feedforward from a measured disturbance and integral anti-windup.
    ''' </summary>
    <System.Serializable()> Public Partial Class PIDController

        Inherits UnitOperations.SpecialOpBaseClass

        Implements Interfaces.IAdjust, IControllableObject

        Public Overrides Property ObjectClass As SimulationObjectClass = SimulationObjectClass.Controllers

        ''' <summary>
        ''' The classic (WinForms) editor window open for this controller, if any. Not saved with the flowsheet.
        ''' </summary>
        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        <Xml.Serialization.XmlIgnore> Public Property ControlPanel As Object Implements IControllableObject.ControlPanel

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

        ''' <summary>
        ''' Manipulated variable value at zero controller output, in the manipulated variable's units. With <see
        ''' cref="ManipulatedVariableSpan"/> at zero it enters the normalized output as a bias of Offset / |SP|;
        ''' with a positive span the manipulated variable is Offset -/+ Output * Span. Default 0.
        ''' </summary>
        Public Property Offset As Double = 0.0

        ''' <summary>
        ''' Range of the MANIPULATED variable, in its own units. Left at zero the controller keeps its
        ''' original arithmetic, in which the output is scaled by the magnitude of the setpoint:
        ''' OutputAbs = (1 -/+ Output) * |SP|. That only holds while the manipulated and the controlled
        ''' variable are of the same order, which is true of a level held by a valve opening and false
        ''' of, say, a flow held by a pressure setpoint, where the controller ends up with an authority
        ''' of a few pascals and pins itself against a limit on the first step.
        '''
        ''' Set to a positive value and the output rides on the manipulated variable's own scale:
        ''' OutputAbs = Offset -/+ Output * Span, with Offset the MV at zero controller output.
        ''' OutputMax - OutputMin is the natural value for it.
        ''' </summary>
        Public Property ManipulatedVariableSpan As Double = 0.0

        ''' <summary>
        ''' Proportional gain, dimensionless, applied to the error normalized by the setpoint magnitude <see
        ''' cref="BaseSP"/>. Default 10.
        ''' </summary>
        Public Property Kp As Double = 10.0

        ''' <summary>
        ''' Derivative gain, in s, applied to the rate of change of the normalized error (or of the process
        ''' variable when <see cref="UseDerivativeOnPV"/> is set). Default 2.
        ''' </summary>
        Public Property Kd As Double = 2.0

        ''' <summary>
        ''' Integral gain, in 1/s, applied to the time integral of the normalized error <see cref="ITerm"/>. In
        ''' the ISA form the integral time is Kp / Ki. Default 2.
        ''' </summary>
        Public Property Ki As Double = 2.0

        ''' <summary>
        ''' Anti-windup limit: the integral term <see cref="ITerm"/> is clamped to the range [-WindupGuard,
        ''' WindupGuard], in normalized error times seconds. Default 20.
        ''' </summary>
        Public Property WindupGuard As Double = 20.0

        ''' <summary>
        ''' Normalized error of the last control step, (PV - SP) / <see cref="BaseSP"/>, dimensionless.
        ''' </summary>
        Public Property CurrentError As Double = 0.0

        ''' <summary>Normalized error of the previous control step, dimensionless.</summary>
        Public Property LastError As Double = 0.0

        ''' <summary>
        ''' Running sum of the absolute normalized error, one term per control step, since the last reset.
        ''' Reported as the integral of the error.
        ''' </summary>
        Public Property CumulativeError As Double = 0.0

        ''' <summary>
        ''' Proportional contribution of the last step: Kp * SetpointWeightP * CurrentError. In the series form
        ''' (<see cref="PIDForm"/> = 2) it carries the interaction factor, Kp * (1 + Td / Ti) * SetpointWeightP *
        ''' CurrentError.
        ''' </summary>
        Public Property PTerm As Double = 0.0

        ''' <summary>
        ''' Time integral of the normalized error, in seconds, accumulated since the last reset. It is clamped
        ''' by <see cref="WindupGuard"/> and held while the output sits at a limit.
        ''' </summary>
        Public Property ITerm As Double = 0.0

        ''' <summary>
        ''' Derivative of the last step, in 1/s: the rate of change of the normalized derivative error (PV -
        ''' <see cref="SetpointWeightD"/> * SP) / <see cref="BaseSP"/>, or of the negative normalized process
        ''' variable when <see cref="UseDerivativeOnPV"/> is set, after the optional derivative filter.
        ''' </summary>
        Public Property DTerm As Double = 0.0

        ''' <summary>
        ''' Normalized (dimensionless) controller output of the last step. It is converted into the manipulated
        ''' variable value <see cref="OutputAbs"/> using the setpoint magnitude or <see
        ''' cref="ManipulatedVariableSpan"/>.
        ''' </summary>
        Public Property Output As Double = 0.0

        ''' <summary>
        ''' Manipulated variable value of the last step, in the manipulated variable's units, clamped to [<see
        ''' cref="OutputMin"/>, <see cref="OutputMax"/>]. Setting it through <c>SetPropertyValue</c> also sets
        ''' the value held in manual mode.
        ''' </summary>
        Public Property OutputAbs As Double = 0.0

        ''' <summary>
        ''' Process variable values recorded at each control step, divided by <see cref="BaseSP"/>, for the
        ''' history chart. Not saved in the XML flowsheet file.
        ''' </summary>
        <Xml.Serialization.XmlIgnore> Public Property PVHistory As New List(Of Double)

        ''' <summary>
        ''' Normalized controller output recorded at each control step, for the history chart. Not saved in the
        ''' XML flowsheet file.
        ''' </summary>
        <Xml.Serialization.XmlIgnore> Public Property MVHistory As New List(Of Double)

        ''' <summary>
        ''' Setpoint values recorded at each control step, divided by <see cref="BaseSP"/>, for the history
        ''' chart. Not saved in the XML flowsheet file.
        ''' </summary>
        <Xml.Serialization.XmlIgnore> Public Property SPHistory As New List(Of Double)

        ''' <summary>
        ''' Setpoint magnitude |SP| captured at the first control step after a reset, used to normalize the
        ''' error, the output and the histories. <c>Nothing</c> until the first step.
        ''' </summary>
        Public BaseSP As Nullable(Of Double)

        ''' <summary>
        ''' Gets or sets whether the controller acts during a dynamic run. The integrator skips an inactive
        ''' controller. Default <c>True</c>.
        ''' </summary>
        Public Property Active As Boolean = True

        ''' <summary>
        ''' When <c>True</c> the controller is in manual: it writes the operator's value <see cref="MVValue"/>
        ''' to the manipulated object and back-calculates <see cref="Output"/> from it. Returning to automatic
        ''' sets the integral term so the output continues from the manual value. Default <c>False</c>.
        ''' </summary>
        Public Property ManualOverride As Boolean = False

        ''' <summary>
        ''' Controller action. <c>False</c> (default): a process variable above the setpoint lowers the
        ''' manipulated variable. <c>True</c>: a process variable above the setpoint raises it.
        ''' </summary>
        Public Property ReverseActing As Boolean = False

        ''' <summary>
        ''' Lower limit of the manipulated variable value <see cref="OutputAbs"/>, in the manipulated variable's
        ''' units. Default -1000.
        ''' </summary>
        Public Property OutputMin As Double = -1000.0

        ''' <summary>
        ''' Upper limit of the manipulated variable value <see cref="OutputAbs"/>, in the manipulated variable's
        ''' units. Default 1000.
        ''' </summary>
        Public Property OutputMax As Double = 1000.0

        ''' <summary>
        ''' Process (controlled) variable value read at the last update, in the controlled variable's units.
        ''' </summary>
        Public Property PVValue As Double = 0.0

        ''' <summary>Setpoint value at the last update, in the controlled variable's units.</summary>
        Public Property SPValue As Double = 0.0

        ''' <summary>
        ''' Manipulated variable value, always in SI units: the value written to the manipulated object at the
        ''' end of each step. In manual mode it holds the operator's value. In automatic mode <see
        ''' cref="UpdateVars"/> refreshes it from the manipulated object and the calculation replaces it with
        ''' <see cref="OutputAbs"/> converted to SI. <see cref="MVValueDisplay"/> gives it in the manipulated
        ''' variable's units.
        ''' </summary>
        Public Property MVValue As Double = 0.0

        ''' <summary>
        ''' <see cref="MVValue"/> converted from SI to the manipulated variable's units (the units of <see
        ''' cref="ManipulatedObjectData"/>, the same as <see cref="OutputAbs"/>, <see cref="PVValue"/> and <see
        ''' cref="SPValue"/> use). Read-only; not saved.
        ''' </summary>
        <Xml.Serialization.XmlIgnore> <Newtonsoft.Json.JsonIgnore> Public ReadOnly Property MVValueDisplay As Double
            Get
                If ManipulatedObjectData Is Nothing Then Return MVValue
                Return SystemsOfUnits.Converter.ConvertFromSI(ManipulatedObjectData.Units, MVValue)
            End Get
        End Property

        ''' <summary>
        ''' Coefficient alpha of the first-order derivative filter, DTerm = alpha * DTerm_previous + (1 - alpha)
        ''' * DTerm_raw. Values outside the open range (0, 1) disable the filter. Default 0.
        ''' </summary>
        Public Property DerivativeFilterCoefficient As Double = 0.0

        ''' <summary>
        ''' When <c>True</c> the derivative term acts on the rate of change of the process variable (derivative
        ''' on measurement), which avoids a derivative kick when the setpoint changes. Default <c>False</c>
        ''' (derivative on the error).
        ''' </summary>
        Public Property UseDerivativeOnPV As Boolean = False

        ''' <summary>
        ''' Setpoint weight (beta) of the proportional term: PTerm = Kp * beta * CurrentError. Default 1.
        ''' </summary>
        Public Property SetpointWeightP As Double = 1.0

        ''' <summary>
        ''' Setpoint weight (gamma) of the derivative term on error: the derivative acts on (PV - gamma * SP) /
        ''' <see cref="BaseSP"/>. At 1 (default) it is the derivative of the error, at 0 the derivative of the
        ''' process variable alone, with no kick when the setpoint moves. Not used when <see
        ''' cref="UseDerivativeOnPV"/> is set.
        ''' </summary>
        Public Property SetpointWeightD As Double = 1.0

        ''' <summary>
        ''' PID algorithm form, with Ti = Kp / Ki and Td = Kd / Kp. 0 = parallel (default): Output = PTerm + Ki *
        ''' ITerm + Kd * DTerm. 1 = ISA (standard, non-interacting): Output = Kp * (beta * e + ITerm / Ti + Td *
        ''' DTerm). 2 = series (interacting), Kp * (1 + 1 / (Ti s)) * (1 + Td s): Output = Kp * ((1 + Td / Ti) *
        ''' beta * e + ITerm / Ti + Td * DTerm), the same as the ISA form with Kp * (1 + Td / Ti), Ti + Td and Ti
        ''' * Td / (Ti + Td). Any other value is calculated with the ISA form.
        ''' Since Ti and Td are taken from Ki and Kd, the ISA output equals the parallel one and both are
        ''' calculated as PTerm + Ki * ITerm + Kd * DTerm, which stays finite at Kp = 0.
        ''' </summary>
        Public Property PIDForm As Integer = 0

        Private FilteredDerivative As Double = 0.0

        Private LastPV As Double = 0.0

        Private WasManualOverride As Boolean = False

        'setpoint of the previous step, for the derivative setpoint weight
        Private LastSetPoint As Nullable(Of Double) = Nothing

        ''' <summary>
        ''' Position of this controller in the order the dynamic integrator runs the PID controllers, in
        ''' ascending order. Default 0.
        ''' </summary>
        Public Property ExecutionOrder As Integer = 0

        ''' <summary>
        ''' Name (ID) of the master PID controller of a cascade. When set, this controller takes its setpoint
        ''' from the master's <see cref="OutputAbs"/> at every step. Empty (default) for no cascade.
        ''' </summary>
        Public Property CascadeMasterID As String = ""

        Protected m_DisturbanceObjectData As New SpecialOps.Helpers.SpecialOpObjectInfo

        ''' <summary>
        ''' Measured disturbance for feedforward control: the object, property and units read at every step.
        ''' </summary>
        Public Property DisturbanceObjectData() As SpecialOps.Helpers.SpecialOpObjectInfo
            Get
                Return m_DisturbanceObjectData
            End Get
            Set(value As SpecialOps.Helpers.SpecialOpObjectInfo)
                m_DisturbanceObjectData = value
            End Set
        End Property

        ''' <summary>
        ''' Feedforward gain, in manipulated variable units per disturbance unit, applied to the change of the
        ''' disturbance since the previous step, after the lead-lag of <see cref="FeedforwardLeadTime"/> and <see
        ''' cref="FeedforwardLagTime"/>, and added to the manipulated variable value. Zero (default) disables
        ''' feedforward.
        ''' </summary>
        Public Property FeedforwardGain As Double = 0.0

        ''' <summary>
        ''' Lead time constant, in s, of the feedforward lead-lag (FeedforwardLeadTime s + 1) /
        ''' (<see cref="FeedforwardLagTime"/> s + 1) applied to the disturbance change before the feedforward
        ''' gain. Zero or less (default 0) leaves the plain lag.
        ''' </summary>
        Public Property FeedforwardLeadTime As Double = 0.0

        ''' <summary>
        ''' Lag time constant, in s, of the feedforward lead-lag applied to the disturbance change before the
        ''' feedforward gain. Zero or less disables the lag. Default 1.
        ''' </summary>
        Public Property FeedforwardLagTime As Double = 1.0

        Private FeedforwardFilterState As Double = 0.0

        'disturbance change of the previous step, for the lead without a lag
        Private LastFeedforwardInput As Double = 0.0

        Private LastDisturbanceValue As Double = 0.0

        Private FeedforwardInitialized As Boolean = False

        ''' <summary>
        ''' Gets or sets the controller setpoint, in the controlled variable's units. Same value as <see
        ''' cref="AdjustValue"/>.
        ''' </summary>
        Public Property SetPoint As Double
            Get
                Return AdjustValue
            End Get
            Set(value As Double)
                AdjustValue = value
            End Set
        End Property

        ''' <summary>Gets a value indicating whether this controller runs in dynamic mode. Always <c>True</c>.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = True

        ''' <summary>Creates a deep copy of this object by round-tripping through XML serialization.</summary>
        ''' <returns>A new <see cref="PIDController"/> instance with the same property values.</returns>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New PIDController()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this object by round-tripping through JSON serialization.</summary>
        ''' <returns>A new <see cref="PIDController"/> instance with the same property values.</returns>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of PIDController)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        Public Property SimultaneousAdjust() As Boolean Implements Interfaces.IAdjust.SimultaneousAdjust
            Get
                Return m_IsSimultAdjustEnabled
            End Get
            Set(ByVal value As Boolean)
                m_IsSimultAdjustEnabled = value
            End Set
        End Property

        ''' <summary>
        ''' Optional initial estimate of the manipulated variable. The PID calculation does not use it.
        ''' </summary>
        Public Property InitialEstimate() As Nullable(Of Double)
            Get
                Return m_initialEstimate
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_initialEstimate = value
            End Set
        End Property

        ''' <summary>
        ''' Optional upper bound of the manipulated variable. The PID calculation does not use it; the output
        ''' limit is <see cref="OutputMax"/>.
        ''' </summary>
        Public Property MaxVal() As Nullable(Of Double)
            Get
                Return m_maxVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_maxVal = value
            End Set
        End Property

        ''' <summary>
        ''' Optional lower bound of the manipulated variable. The PID calculation does not use it; the output
        ''' limit is <see cref="OutputMin"/>.
        ''' </summary>
        Public Property MinVal() As Nullable(Of Double)
            Get
                Return m_minVal
            End Get
            Set(ByVal value As Nullable(Of Double))
                m_minVal = value
            End Set
        End Property

        ''' <summary>
        ''' Flag indicating that the reference variable is defined. The PID calculation does not use it.
        ''' </summary>
        Public Property RvOk() As Boolean
            Get
                Return m_RV_OK
            End Get
            Set(ByVal value As Boolean)
                m_RV_OK = value
            End Set
        End Property

        ''' <summary>
        ''' Flag indicating that the manipulated variable is defined. The PID calculation does not use it.
        ''' </summary>
        Public Property MvOk() As Boolean
            Get
                Return m_MV_OK
            End Get
            Set(ByVal value As Boolean)
                m_MV_OK = value
            End Set
        End Property

        ''' <summary>
        ''' Flag indicating that the controlled variable is defined. The PID calculation does not use it.
        ''' </summary>
        Public Property CvOk() As Boolean
            Get
                Return m_CV_OK
            End Get
            Set(ByVal value As Boolean)
                m_CV_OK = value
            End Set
        End Property

        Public Property ManipulatedObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ManipulatedObjectData
            Get
                Return Me.m_ManipulatedObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ManipulatedObjectData = value
            End Set
        End Property

        Public Property ControlledObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ControlledObjectData
            Get
                Return Me.m_ControlledObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ControlledObjectData = value
            End Set
        End Property

        Public Property ReferencedObjectData() As Interfaces.ISpecialOpObjectInfo Implements Interfaces.IAdjust.ReferencedObjectData
            Get
                Return Me.m_ReferencedObjectData
            End Get
            Set(ByVal value As Interfaces.ISpecialOpObjectInfo)
                Me.m_ReferencedObjectData = value
            End Set
        End Property

        ''' <summary>
        ''' The flowsheet object whose property the controller manipulates. <see cref="UpdateVars"/> resolves it
        ''' from <see cref="ManipulatedObjectData"/> at every step. Not serialized.
        ''' </summary>
        <Xml.Serialization.XmlIgnore()> Public Property ManipulatedObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ManipulatedObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ManipulatedObject = value
            End Set
        End Property

        ''' <summary>
        ''' The flowsheet object whose property is controlled, linked when the flowsheet or the graphic connects
        ''' it. The calculation resolves the object from <see cref="ControlledObjectData"/>. Not serialized.
        ''' </summary>
        <Xml.Serialization.XmlIgnore()> Public Property ControlledObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ControlledObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ControlledObject = value
            End Set
        End Property

        ''' <summary>
        ''' Reference object, linked from <see cref="ReferencedObjectData"/> when the flowsheet loads. The PID
        ''' calculation does not use it. Not serialized.
        ''' </summary>
        <Xml.Serialization.XmlIgnore()> Public Property ReferenceObject() As SharedClasses.UnitOperations.BaseClass
            Get
                Return Me.m_ReferenceObject
            End Get
            Set(ByVal value As SharedClasses.UnitOperations.BaseClass)
                Me.m_ReferenceObject = value
            End Set
        End Property

        ''' <summary>
        ''' Name of the manipulated property. The PID calculation does not read it; the property comes from <see
        ''' cref="ManipulatedObjectData"/>.
        ''' </summary>
        Public Property ManipulatedVariable() As String
            Get
                Return Me.m_ManipulatedVariable
            End Get
            Set(ByVal value As String)
                Me.m_ManipulatedVariable = value
            End Set
        End Property

        ''' <summary>
        ''' Name of the controlled property. The PID calculation does not read it; the property comes from <see
        ''' cref="ControlledObjectData"/>.
        ''' </summary>
        Public Property ControlledVariable() As String
            Get
                Return Me.m_ControlledVariable
            End Get
            Set(ByVal value As String)
                Me.m_ControlledVariable = value
            End Set
        End Property

        ''' <summary>Name of the reference property. The PID calculation does not use it.</summary>
        Public Property ReferenceVariable() As String
            Get
                Return Me.m_ReferenceVariable
            End Get
            Set(ByVal value As String)
                Me.m_ReferenceVariable = value
            End Set
        End Property

        ''' <summary>Free-text status string. The PID calculation does not set it.</summary>
        Public Property Status() As String
            Get
                Return Me.m_Status
            End Get
            Set(ByVal value As String)
                Me.m_Status = value
            End Set
        End Property

        Public Property AdjustValue() As Double Implements Interfaces.IAdjust.AdjustValue
            Get
                Return Me.m_AdjustValue
            End Get
            Set(ByVal value As Double)
                Me.m_AdjustValue = value
            End Set
        End Property

        Public Property Referenced() As Boolean Implements Interfaces.IAdjust.Referenced
            Get
                Return Me.m_IsReferenced
            End Get
            Set(ByVal value As Boolean)
                Me.m_IsReferenced = value
            End Set
        End Property

        ''' <summary>
        ''' Step size inherited from the adjust layout. The PID calculation does not use it. Default 0.1.
        ''' </summary>
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

        ''' <summary>
        ''' Maximum number of iterations inherited from the adjust layout. The PID calculation does not use it.
        ''' Default 10.
        ''' </summary>
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
                    .Units = xel.@PropertyUnits
                    .UnitsType = [Enum].Parse(.UnitsType.GetType, xel.@PropertyUnitsType)
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ControlledObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_ControlledObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                    .Units = xel.@PropertyUnits
                    .UnitsType = [Enum].Parse(.UnitsType.GetType, xel.@PropertyUnitsType)
                End With

            End If

            xel = (From xel2 As XElement In data Select xel2 Where xel2.Name = "ReferencedObjectData").SingleOrDefault

            If Not xel Is Nothing Then

                With m_ReferencedObjectData
                    .ID = xel.@ID
                    .Name = xel.@Name
                    .PropertyName = xel.@Property
                    .ObjectType = xel.@ObjectType
                    .Units = xel.@PropertyUnits
                    .UnitsType = [Enum].Parse(.UnitsType.GetType, xel.@PropertyUnitsType)
                End With

            End If
            Return True
        End Function

        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Dim elements As System.Collections.Generic.List(Of System.Xml.Linq.XElement) = MyBase.SaveData()
            Dim ci As Globalization.CultureInfo = Globalization.CultureInfo.InvariantCulture

            If m_ManipulatedObjectData Is Nothing Then m_ManipulatedObjectData = New SpecialOpObjectInfo()
            If m_ControlledObjectData Is Nothing Then m_ControlledObjectData = New SpecialOpObjectInfo()
            If m_ReferencedObjectData Is Nothing Then m_ReferencedObjectData = New SpecialOpObjectInfo()

            If m_ManipulatedObjectData.ObjectType = Nothing Then m_ManipulatedObjectData.ObjectType = ""
            If m_ControlledObjectData.ObjectType = Nothing Then m_ControlledObjectData.ObjectType = ""
            If m_ReferencedObjectData.ObjectType = Nothing Then m_ReferencedObjectData.ObjectType = ""

            With elements
                .Add(New XElement("ManipulatedObjectData", New XAttribute("ID", m_ManipulatedObjectData.ID),
                                  New XAttribute("Name", m_ManipulatedObjectData.Name),
                                  New XAttribute("Property", m_ManipulatedObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ManipulatedObjectData.ObjectType),
                                  New XAttribute("PropertyUnitsType", m_ManipulatedObjectData.UnitsType),
                                  New XAttribute("PropertyUnits", m_ManipulatedObjectData.Units)))
                .Add(New XElement("ControlledObjectData", New XAttribute("ID", m_ControlledObjectData.ID),
                                  New XAttribute("Name", m_ControlledObjectData.Name),
                                  New XAttribute("Property", m_ControlledObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ControlledObjectData.ObjectType),
                                  New XAttribute("PropertyUnitsType", m_ControlledObjectData.UnitsType),
                                  New XAttribute("PropertyUnits", m_ControlledObjectData.Units)))
                .Add(New XElement("ReferencedObjectData", New XAttribute("ID", m_ReferencedObjectData.ID),
                                  New XAttribute("Name", m_ReferencedObjectData.Name),
                                  New XAttribute("Property", m_ReferencedObjectData.PropertyName),
                                  New XAttribute("ObjectType", m_ReferencedObjectData.ObjectType),
                                  New XAttribute("PropertyUnitsType", m_ReferencedObjectData.UnitsType),
                                  New XAttribute("PropertyUnits", m_ReferencedObjectData.Units)))
            End With

            Return elements

        End Function

        ''' <summary>Initializes a new default instance of the <see cref="PIDController"/> class.</summary>
        Public Sub New()
            MyBase.New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="PIDController"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The name of this controller.</param>
        ''' <param name="description">A brief description of this controller.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            m_ManipulatedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ControlledObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            m_ReferencedObjectData = New SpecialOps.Helpers.SpecialOpObjectInfo
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Readable names for the property identifiers, which are the .NET property names.</summary>
        Public Overrides Function GetPropertyDescription(prop As String) As String
            Select Case prop
                Case "Active" : Return "Active"
                Case "ManualOverride" : Return "Manual Override"
                Case "LastError" : Return "Previous Error"
                Case "CurrentError" : Return "Current Error"
                Case "CumulativeError" : Return "Integral of the Error"
                Case "SetPointAbs" : Return "Set Point"
                Case "Kp" : Return "Proportional Gain (Kp)"
                Case "Ki" : Return "Integral Gain (Ki)"
                Case "Kd" : Return "Derivative Gain (Kd)"
                Case "Output" : Return "Controller Output (normalized)"
                Case "OutputMin" : Return "Controller Output Minimum"
                Case "OutputMax" : Return "Controller Output Maximum"
                Case "OutputAbs" : Return "Manipulated Variable Value"
                Case "Offset" : Return "Manipulated Variable at Zero Output"
                Case "ManipulatedVariableSpan" : Return "Manipulated Variable Span"
                Case Else : Return MyBase.GetPropertyDescription(prop)
            End Select
        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Select Case prop
                    Case "ManualOverride"
                        Return ManualOverride
                    Case "Active"
                        Return Active
                    Case "LastError"
                        Return LastError
                    Case "CurrentError"
                        Return CurrentError
                    Case "CumulativeError"
                        Return CumulativeError
                    Case "SetPointAbs"
                        Return AdjustValue
                    Case "Kp"
                        Return Kp
                    Case "Ki"
                        Return Ki
                    Case "Kd"
                        Return Kd
                    Case "Output"
                        Return Output
                    Case "OutputMin"
                        Return OutputMin
                    Case "OutputMax"
                        Return OutputMax
                    Case "OutputAbs"
                        Return OutputAbs
                    Case "Offset"
                        Return Offset
                    Case "ManipulatedVariableSpan"
                        Return ManipulatedVariableSpan
                    Case Else
                        Return Nothing
                End Select
            End If
        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            proplist.Add("Active")
            proplist.Add("ManualOverride")
            proplist.Add("LastError")
            proplist.Add("CurrentError")
            proplist.Add("CumulativeError")
            proplist.Add("SetPointAbs")
            proplist.Add("Kp")
            proplist.Add("Ki")
            proplist.Add("Kd")
            proplist.Add("Output")
            proplist.Add("OutputMin")
            proplist.Add("OutputMax")
            proplist.Add("OutputAbs")
            proplist.Add("Offset")
            proplist.Add("ManipulatedVariableSpan")
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean

            If MyBase.SetPropertyValue(prop, propval, su) Then Return True

            Select Case prop
                Case "ManualOverride"
                    ManualOverride = propval
                Case "Active"
                    Active = propval
                Case "SetPointAbs"
                    AdjustValue = propval
                Case "OutputMin"
                    OutputMin = propval
                Case "OutputMax"
                    OutputMax = propval
                Case "Kp"
                    Kp = propval
                Case "Ki"
                    Ki = propval
                Case "Kd"
                    Kd = propval
                Case "Offset"
                    Offset = propval
                Case "ManipulatedVariableSpan"
                    ManipulatedVariableSpan = propval
                Case "OutputAbs"
                    'the output in the manipulated variable's own units: in manual it is what the controller
                    'holds and writes to the manipulated object at every step
                    OutputAbs = propval
                    If ManipulatedObjectData IsNot Nothing Then
                        MVValue = SystemsOfUnits.Converter.ConvertToSI(ManipulatedObjectData.Units, Convert.ToDouble(propval))
                    Else
                        MVValue = Convert.ToDouble(propval)
                    End If
                Case Else
                    Return False
            End Select
            Return True
        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Dim u0 As String = MyBase.GetPropertyUnit(prop, su)

            If u0 <> "NF" Then
                Return u0
            Else
                Return ""
            End If
        End Function

        ''' <summary>Returns the raw bytes of the icon image for this controller.</summary>
        ''' <returns>A byte array containing the PNG image data for the icon.</returns>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.control_panel.png")

        End Function

        ''' <summary>Returns the localized description string for this controller type.</summary>
        ''' <returns>A translated description string identifying this controller type.</returns>
        Public Overrides Function GetDisplayDescription() As String
            Return ResMan.GetLocalString("PID_Desc")
        End Function

        ''' <summary>Returns the localized display name for this controller type.</summary>
        ''' <returns>A translated name string for this controller type.</returns>
        Public Overrides Function GetDisplayName() As String
            Return ResMan.GetLocalString("PID_Name")
        End Function

        ''' <summary>Gets a value indicating whether this controller is compatible with mobile interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean
            Get
                Return True
            End Get
        End Property

        Private InitializeFromMV As Boolean = False

        ''' <summary>
        ''' Resets the controller and makes its first step start from the manipulated variable as it is now:
        ''' the output is read back from the valve opening (or whatever the controller moves) and the
        ''' integral term is set to hold it, the same handover as leaving manual. A run that starts from a
        ''' stored flowsheet state continues from the opening the state carries, whatever the tuning.
        ''' </summary>
        Public Sub StartFromManipulatedVariable()
            Reset()
            InitializeFromMV = True
        End Sub

        ''' <summary>
        ''' Clears the controller state: the P, I and D terms, the errors, the output, the derivative filter,
        ''' the histories and <see cref="BaseSP"/>. Tuning, limits and the setpoint are kept.
        ''' </summary>
        Public Sub Reset()

            PTerm = 0.0
            ITerm = 0.0
            DTerm = 0.0

            LastError = 0.0

            Output = 0.0

            CumulativeError = 0.0

            FilteredDerivative = 0.0
            LastPV = 0.0
            LastSetPoint = Nothing
            WasManualOverride = False

            FeedforwardFilterState = 0.0
            LastFeedforwardInput = 0.0
            LastDisturbanceValue = 0.0
            FeedforwardInitialized = False

            PVHistory.Clear()
            MVHistory.Clear()
            SPHistory.Clear()

            BaseSP = Nothing

        End Sub

        ''' <summary>
        ''' Estimates starting values for the tuning from the current error and the current integrator time
        ''' step: Kp and Kd are set from the mid-point of the output range, and Ki is set to zero.
        ''' </summary>
        Public Sub EstimateParameters()

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds
            'in real time the plant moves with the real-time step, and so must the controller
            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0
            'the controllers run every CalculationRateControl steps
            timestep *= Math.Max(1, integrator.CalculationRateControl)

            Dim ControlledObject = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = ControlledObjectData.ID).SingleOrDefault

            Dim ManipulatedObject = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = ManipulatedObjectData.ID).SingleOrDefault

            Dim CurrentValue = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(ControlledObjectData.Units, ControlledObject.GetPropertyValue(ControlledObjectData.PropertyName))

            Dim CurrentManipulatedValue = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(ManipulatedObjectData.Units, ManipulatedObject.GetPropertyValue(ManipulatedObjectData.PropertyName))

            Dim BaseSP = Math.Abs(AdjustValue)

            Dim CurrentError = (CurrentValue - AdjustValue) / BaseSP

            Dim delta_error = 0.01 * CurrentError

            Dim newout As Double

            newout = (OutputMin + OutputMax) / 2

            Dim pidout, dterm As Double

            If Not ReverseActing Then
                pidout = 1.0 - newout / BaseSP
            Else
                pidout = newout / BaseSP - 1.0
            End If

            If Math.Abs(CurrentError) > 0.0 Then dterm = delta_error / timestep

            Ki = 0.0

            If Math.Abs(CurrentError) > 0.0 Then

                Kp = (newout - Offset / BaseSP) * 0.9 / Math.Abs(CurrentError)

            Else

                Kp = 0.0

            End If

            If Math.Abs(dterm) > 0.0 Then

                Kd = (newout - Offset / BaseSP) * 0.1 / Math.Abs(dterm)

            Else

                Kd = 0.0

            End If

        End Sub

        ''' <summary>
        ''' Reads the setpoint, the process variable and, in automatic mode, the manipulated variable from the
        ''' flowsheet into <see cref="SPValue"/>, <see cref="PVValue"/> (both in display units) and <see
        ''' cref="MVValue"/> (in SI), and resolves
        ''' <see cref="ManipulatedObject"/>. Leaves the values unchanged when the controller is not fully
        ''' configured.
        ''' </summary>
        Public Sub UpdateVars()

            ' An unconfigured controller (freshly added, or one being drawn on the flowsheet before its
            ' objects are set) has no controlled/manipulated data, or an id that resolves to nothing.
            ' Leave the last known SP/PV/MV instead of dereferencing Nothing, which otherwise surfaced as
            ' "Error drawing PID-1: Object reference not set to an instance of an object" beside the icon.
            If ControlledObjectData Is Nothing OrElse ManipulatedObjectData Is Nothing Then Exit Sub

            Dim controlled = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = ControlledObjectData.ID).SingleOrDefault

            Dim manipulated = GetFlowsheet.SimulationObjects.Values.Where(Function(x) x.Name = ManipulatedObjectData.ID).SingleOrDefault

            ' Keep the resolved object on the property, not only in a local: the last line of Calculate()
            ' writes the new MV through Me.ManipulatedObject, and nothing but the object editors ever
            ' assigned it. A controller driven from the Automation API or from MCP, or restored from a
            ' file whose editor was never opened, dereferenced Nothing there.
            ManipulatedObject = TryCast(manipulated, SharedClasses.UnitOperations.BaseClass)

            If controlled Is Nothing OrElse manipulated Is Nothing Then Exit Sub

            Dim CurrentValue = SharedClasses.SystemsOfUnits.Converter.ConvertFromSI(ControlledObjectData.Units, controlled.GetPropertyValue(ControlledObjectData.PropertyName))

            SPValue = AdjustValue

            PVValue = CurrentValue

            ' In manual the MV is what the operator asked for, not what the valve reports: refreshing it
            ' from the manipulated object here threw the operator's value away before Calculate could use it.
            ' MVValue is in SI, as the manipulated object reports it and as Calculate writes it back.
            If Not ManualOverride Then MVValue = CDbl(manipulated.GetPropertyValue(ManipulatedObjectData.PropertyName))

        End Sub


        Public Overrides Sub Calculate(Optional args As Object = Nothing)

            ' Calculates PID value for given reference feedback
            ' u(t) = K_p e(t) + K_i \int_{0}^{t} e(t)dt + K_d {de}/{dt}

            Dim integratorID = FlowSheet.DynamicsManager.ScheduleList(FlowSheet.DynamicsManager.CurrentSchedule).CurrentIntegrator
            Dim integrator = FlowSheet.DynamicsManager.IntegratorList(integratorID)

            Dim timestep = integrator.IntegrationStep.TotalSeconds
            'in real time the plant moves with the real-time step, and so must the controller
            If integrator.RealTime Then timestep = Convert.ToDouble(integrator.RealTimeStepMs) / 1000.0
            'the controllers run every CalculationRateControl steps
            timestep *= Math.Max(1, integrator.CalculationRateControl)

            UpdateVars()

            If CascadeMasterID <> "" Then
                Try
                    Dim master = DirectCast(FlowSheet.SimulationObjects.Values.Where(
                        Function(x) x.Name = CascadeMasterID).SingleOrDefault(), PIDController)
                    If master IsNot Nothing Then
                        AdjustValue = master.OutputAbs
                    End If
                Catch
                End Try
            End If

            If BaseSP Is Nothing Then BaseSP = Math.Abs(AdjustValue)

            SPHistory.Add(AdjustValue / BaseSP)

            PVHistory.Add(PVValue / BaseSP)

            Dim prevPV = LastPV
            LastPV = PVValue / BaseSP

            LastError = CurrentError

            CurrentError = (PVValue - AdjustValue) / BaseSP

            CumulativeError += Math.Abs(CurrentError)

            Dim beta = SetpointWeightP

            'integral and derivative times of the ISA and series forms
            Dim Ti As Double = If(Ki > 0, Kp / Ki, 1.0E+20)
            Dim Td As Double = If(Kp > 0, Kd / Kp, 0.0)

            'series (interacting) form: Kp (1 + 1/(Ti s)) (1 + Td s) = Kp [(1 + Td/Ti) + 1/(Ti s) + Td s], so the
            'proportional action carries the interaction factor 1 + Td/Ti and the I and D actions are those of
            'the ISA form with the same Kp, Ti and Td
            Dim seriesFactor As Double = 1.0
            If PIDForm = 2 AndAlso Kp > 0 AndAlso Ki > 0 Then seriesFactor = 1.0 + Td / Ti

            PTerm = Kp * seriesFactor * beta * CurrentError

            Dim rawDerivative As Double = 0.0

            If UseDerivativeOnPV Then
                'the error is (PV - SP) / BaseSP, so on a constant setpoint its change is the change of PV / BaseSP
                If prevPV <> 0.0 Then rawDerivative = (LastPV - prevPV) / timestep
            Else
                Dim delta_error = CurrentError - LastError
                'derivative setpoint weight gamma: the derivative acts on (PV - gamma SP) / BaseSP, which is the
                'error plus (1 - gamma) SP / BaseSP, so its change adds (1 - gamma) times the setpoint change
                If SetpointWeightD <> 1.0 AndAlso LastSetPoint.HasValue Then
                    delta_error += (1.0 - SetpointWeightD) * (AdjustValue - LastSetPoint.Value) / BaseSP
                End If
                If Math.Abs(LastError) > 0.0 Then rawDerivative = delta_error / timestep
            End If

            LastSetPoint = AdjustValue

            Dim alpha = DerivativeFilterCoefficient
            If alpha > 0.0 AndAlso alpha < 1.0 Then
                FilteredDerivative = alpha * FilteredDerivative + (1.0 - alpha) * rawDerivative
                DTerm = FilteredDerivative
            Else
                DTerm = rawDerivative
            End If

            Dim ITermBefore = ITerm

            ITerm += CurrentError * timestep

            If ITerm < -WindupGuard Then
                ITerm = -WindupGuard
            ElseIf ITerm > WindupGuard Then
                ITerm = WindupGuard
            End If

            If InitializeFromMV Then
                InitializeFromMV = False
                If Not ManualOverride AndAlso ManipulatedObject IsNot Nothing Then
                    Dim mvNow = SystemsOfUnits.Converter.ConvertFromSI(ManipulatedObjectData.Units,
                        ManipulatedObject.GetPropertyValue(ManipulatedObjectData.PropertyName))
                    If ManipulatedVariableSpan > 0.0 Then
                        Output = If(ReverseActing, (mvNow - Offset) / ManipulatedVariableSpan, (Offset - mvNow) / ManipulatedVariableSpan)
                    ElseIf ReverseActing Then
                        Output = mvNow / BaseSP - 1.0
                    Else
                        Output = 1.0 - mvNow / BaseSP
                    End If
                    WasManualOverride = True
                End If
            End If

            If Not ManualOverride Then

                If WasManualOverride Then
                    Dim manualOutput = Output
                    Dim pContrib = PTerm
                    Dim dContrib = Kd * DTerm
                    Dim handoverBias = If(ManipulatedVariableSpan > 0.0, 0.0, Offset / BaseSP)
                    ITerm = (manualOutput - handoverBias - pContrib - dContrib) / Math.Max(Ki, 1.0E-20)
                    If ITerm < -WindupGuard Then ITerm = -WindupGuard
                    If ITerm > WindupGuard Then ITerm = WindupGuard
                    WasManualOverride = False
                End If

                ' with a span, the bias is applied on the manipulated variable's own scale further down,
                ' so it must not be folded into the dimensionless controller output here
                Dim bias As Double = If(ManipulatedVariableSpan > 0.0, 0.0, Offset / BaseSP)

                'with Ti = Kp / Ki and Td = Kd / Kp, the ISA form Kp (beta e + ITerm / Ti + Td DTerm) is
                'PTerm + Ki ITerm + Kd DTerm, which stays finite at Kp = 0; the series form differs only by the
                'interaction factor already in PTerm
                Output = PTerm + Ki * ITerm + Kd * DTerm + bias

                Dim ffOutput As Double = 0.0

                If FeedforwardGain <> 0 AndAlso m_DisturbanceObjectData IsNot Nothing AndAlso m_DisturbanceObjectData.ID <> "" Then
                    Try
                        Dim dvObj = FlowSheet.SimulationObjects.Values.Where(
                            Function(x) x.Name = m_DisturbanceObjectData.ID).SingleOrDefault()
                        If dvObj IsNot Nothing Then
                            Dim dvVal = SystemsOfUnits.Converter.ConvertFromSI(
                                m_DisturbanceObjectData.Units,
                                dvObj.GetPropertyValue(m_DisturbanceObjectData.PropertyName))

                            'the first reading is the reference: no change, so no kick on the first step
                            If Not FeedforwardInitialized Then
                                LastDisturbanceValue = dvVal
                                FeedforwardInitialized = True
                            End If
                            Dim dvChange = dvVal - LastDisturbanceValue
                            LastDisturbanceValue = dvVal

                            If FeedforwardLagTime > 0 Then
                                Dim alphaFF = Math.Exp(-timestep / FeedforwardLagTime)
                                FeedforwardFilterState = alphaFF * FeedforwardFilterState + (1.0 - alphaFF) * dvChange
                                If FeedforwardLeadTime > 0 Then
                                    'lead-lag (Tlead s + 1)/(Tlag s + 1) = Tlead/Tlag + (1 - Tlead/Tlag)/(Tlag s + 1):
                                    'a direct share of the input plus the rest through the same lag
                                    Dim leadRatio = FeedforwardLeadTime / FeedforwardLagTime
                                    ffOutput = FeedforwardGain * (leadRatio * dvChange + (1.0 - leadRatio) * FeedforwardFilterState)
                                Else
                                    ffOutput = FeedforwardGain * FeedforwardFilterState
                                End If
                            ElseIf FeedforwardLeadTime > 0 Then
                                'lead without a lag, Tlead s + 1, with a backward difference
                                ffOutput = FeedforwardGain * (dvChange + FeedforwardLeadTime * (dvChange - LastFeedforwardInput) / timestep)
                            Else
                                ffOutput = FeedforwardGain * dvChange
                            End If
                            LastFeedforwardInput = dvChange
                        End If
                    Catch
                    End Try
                End If

                If ManipulatedVariableSpan > 0.0 Then
                    If Not ReverseActing Then
                        OutputAbs = Offset - Output * ManipulatedVariableSpan + ffOutput
                    Else
                        OutputAbs = Offset + Output * ManipulatedVariableSpan + ffOutput
                    End If
                ElseIf Not ReverseActing Then
                    OutputAbs = (1.0 - Output) * BaseSP + ffOutput
                Else
                    OutputAbs = (1.0 + Output) * BaseSP + ffOutput
                End If

                'no integration while the output sits beyond a limit: the integral would wind up through a long
                'saturation (a pressure controller waiting for a column to pressurise) and hold the output at the
                'limit long after the error reversed
                If OutputAbs > OutputMax OrElse OutputAbs < OutputMin Then ITerm = ITermBefore

                If OutputAbs > OutputMax Then OutputAbs = OutputMax

                If OutputAbs < OutputMin Then OutputAbs = OutputMin

                MVValue = SystemsOfUnits.Converter.ConvertToSI(ManipulatedObjectData.Units, OutputAbs)

            Else

                WasManualOverride = True

                OutputAbs = SystemsOfUnits.Converter.ConvertFromSI(ManipulatedObjectData.Units, MVValue)

                If ManipulatedVariableSpan > 0.0 Then
                    If Not ReverseActing Then
                        Output = (Offset - OutputAbs) / ManipulatedVariableSpan
                    Else
                        Output = (OutputAbs - Offset) / ManipulatedVariableSpan
                    End If
                ElseIf Not ReverseActing Then
                    Output = 1.0 - OutputAbs / BaseSP
                Else
                    Output = OutputAbs / BaseSP - 1.0
                End If

            End If

            MVHistory.Add(Output)

            ManipulatedObject.SetPropertyValue(ManipulatedObjectData.PropertyName, MVValue)

        End Sub

        ''' <summary>
        ''' Builds the history chart with the normalized setpoint, process variable and controller output per
        ''' control step.
        ''' </summary>
        ''' <param name="name">The chart name, shown as the subtitle ("History").</param>
        ''' <returns>An OxyPlot <c>PlotModel</c> with the SP, PV and MV series.</returns>
        Public Overrides Function GetChartModel(name As String) As Object

            Dim model = New PlotModel() With {.Subtitle = name, .Title = GraphicObject.Tag}

            Dim xavals = New List(Of Double)
            For i = 0 To PVHistory.Count - 1
                xavals.Add(i)
            Next

            model.TitleFontSize = 12
            model.SubtitleFontSize = 10

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Bottom,
                .FontSize = 10,
                .Title = "Step"
            })

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Left,
                .FontSize = 10,
                .Title = "SP/PV",
                .Key = "0"
            })

            model.Axes.Add(New LinearAxis() With {
                .MajorGridlineStyle = LineStyle.Dash,
                .MinorGridlineStyle = LineStyle.Dot,
                .Position = AxisPosition.Right,
                .FontSize = 10,
                .Title = "MV",
                .Key = "1"
            })

            model.LegendFontSize = 10
            model.LegendPlacement = LegendPlacement.Outside
            model.LegendOrientation = LegendOrientation.Horizontal
            model.LegendPosition = LegendPosition.BottomCenter
            model.TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView

            model.AddLineSeries(xavals, PVHistory, "PV")
            model.AddLineSeries(xavals, SPHistory, "SP")
            model.AddLineSeries(xavals, MVHistory, "MV")

            DirectCast(model.Series.Item(2), LineSeries).YAxisKey = "1"

            Return model

        End Function

        ''' <summary>Returns the names of the charts this controller can embed in the flowsheet.</summary>
        ''' <returns>A list with the single chart name "History".</returns>
        Public Overrides Function GetChartModelNames() As List(Of String)
            Return New List(Of String)({"History"})
        End Function

    End Class

End Namespace




