Imports DWSIM.Thermodynamics.BaseClasses
Imports Flee.PublicTypes
Imports System.Math
Imports System.Linq
Imports DWSIM.MathOps.MathEx.Common
Imports DotNumerics.Optimization
Imports DWSIM.MathOps.MathEx
Imports DWSIM.Interfaces.Enums
Imports DWSIM.SharedClasses
Imports DWSIM.Thermodynamics.Streams
Imports DWSIM.Thermodynamics
Imports scaler = DotNumerics.Scaling.Scaler
Imports DWSIM.MathOps
Imports SkiaSharp
Imports System.IO
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects
Imports DWSIM.DrawingTools.Point
Imports DWSIM.Interfaces.Enums.GraphicObjects
Imports Python.Runtime
Imports DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes
Imports System.Text
Imports DWSIM.UI.Shared.Avalonia

Namespace Reactors

    ''' <summary>
    ''' Represents a Gibbs Reactor that uses the Reaktoro library (via Python.NET) to perform
    ''' a Gibbs free-energy minimisation for geochemical and aqueous-phase equilibrium calculations.
    ''' Supports aqueous, gaseous, liquid, and mineral phases.
    ''' </summary>
    <System.Serializable()> Public Partial Class Reactor_ReaktoroGibbs

        Inherits Reactor

        Implements DWSIM.Interfaces.IExternalUnitOperation

        Private ImagePath As String = ""

        Private Image As SKImage

        ''' <summary>Gets or sets the Base64-encoded embedded image data for the custom icon.</summary>
        Public Property EmbeddedImageData As String = ""

        ''' <summary>Gets or sets whether the embedded image is used for the unit operation icon.</summary>
        Public Property UseEmbeddedImage As Boolean = False

        <NonSerialized> <Xml.Serialization.XmlIgnore> Public f As Object

        ''' <summary>Gets or sets the name of the Reaktoro thermodynamic database file (e.g. "supcrt07.xml").</summary>
        Public Property DatabaseName As String = "supcrt07.xml"

        ''' <summary>Gets or sets whether an external (user-supplied) database file is used instead of the built-in one.</summary>
        Public Property UseExternalDatabase As Boolean = False

        ''' <summary>Gets or sets the file path to the external Reaktoro database.</summary>
        Public Property ExternalDatabaseFileName As String = ""

        ''' <summary>Gets or sets the full text contents of the external database file.</summary>
        Public Property ExternalDatabaseContents As String = ""

        ''' <summary>Gets the default name prefix used when adding this unit operation to a flowsheet.</summary>
        Public Property Prefix As String = "RK-" Implements IExternalUnitOperation.Prefix

        ''' <summary>Gets or sets the display name of this unit operation.</summary>
        Public Overrides Property ComponentName As String = GetDisplayName()

        ''' <summary>Gets or sets the display description of this unit operation.</summary>
        Public Overrides Property ComponentDescription As String = GetDisplayDescription()

        ''' <summary>Gets a value indicating this reactor does not support dynamic simulation mode.</summary>
        Public Overrides ReadOnly Property SupportsDynamicMode As Boolean = False

        ''' <summary>Gets a value indicating this reactor has no dedicated dynamic-mode properties.</summary>
        Public Overrides ReadOnly Property HasPropertiesForDynamicMode As Boolean = False

        Private ReadOnly Property IExternalUnitOperation_Name As String = GetDisplayName() Implements IExternalUnitOperation.Name

        ''' <summary>Gets the description of this external unit operation.</summary>
        Public ReadOnly Property Description As String = GetDisplayDescription() Implements IExternalUnitOperation.Description

        ''' <summary>Gets a value indicating this reactor is not compatible with mobile/cross-platform interfaces.</summary>
        Public Overrides ReadOnly Property MobileCompatible As Boolean = False

        ''' <summary>Gets or sets the list of Reaktoro species names participating in the equilibrium calculation.</summary>
        Public Property CompoundsList As New List(Of String)

        ''' <summary>Gets or sets the list of chemical element symbols used for the elemental balance.</summary>
        Public Property ElementsList As New List(Of String)

        ''' <summary>Gets or sets whether an aqueous phase is included in the Reaktoro system.</summary>
        Public Property AqueousPhase As Boolean = True

        ''' <summary>Gets or sets whether a gaseous phase is included in the Reaktoro system.</summary>
        Public Property GaseousPhase As Boolean = True

        ''' <summary>Gets or sets whether a liquid (non-aqueous) phase is included in the Reaktoro system.</summary>
        Public Property LiquidPhase As Boolean = False

        ''' <summary>Gets or sets whether mineral (solid) phases are included in the Reaktoro system.</summary>
        Public Property MineralPhase As Boolean = False

        ''' <summary>Gets or sets a mapping from DWSIM compound names to Reaktoro species names.</summary>
        Public Property CompoundNames As New Dictionary(Of String, String)

        ''' <summary>Gets or sets the mapping from Reaktoro species to DWSIM compounds for output processing.</summary>
        Public Property SpeciesMaps As New Dictionary(Of String, String)

        ''' <summary>Gets or sets the dictionary of compound molar conversions calculated by the reactor.</summary>
        Public Property CompoundConversions As New Dictionary(Of String, Double)

        ''' <summary>Initializes a new default instance of the <see cref="Reactor_ReaktoroGibbs"/> class.</summary>
        Public Sub New()

            MyBase.New()

        End Sub

        ''' <summary>Returns the display name for this unit operation.</summary>
        ''' <returns>The string "Gibbs Reactor (Reaktoro)".</returns>
        Public Overrides Function GetDisplayName() As String

            Return "Gibbs Reactor (Reaktoro)"

        End Function

        ''' <summary>Returns the display description for this unit operation.</summary>
        ''' <returns>The string "Gibbs Reactor (Reaktoro)".</returns>
        Public Overrides Function GetDisplayDescription() As String

            Return "Gibbs Reactor (Reaktoro)"

        End Function

        ''' <summary>Creates a deep copy of this reactor via XML serialization.</summary>
        Public Overrides Function CloneXML() As Object
            Dim obj As ICustomXMLSerialization = New Reactor_ReaktoroGibbs()
            obj.LoadData(Me.SaveData)
            Return obj
        End Function

        ''' <summary>Creates a deep copy of this reactor via JSON serialization.</summary>
        Public Overrides Function CloneJSON() As Object
            Return Newtonsoft.Json.JsonConvert.DeserializeObject(Of Reactor_ReaktoroGibbs)(Newtonsoft.Json.JsonConvert.SerializeObject(Me))
        End Function

        ''' <summary>Restores the reactor state from XML.</summary>
        Public Overrides Function LoadData(data As System.Collections.Generic.List(Of System.Xml.Linq.XElement)) As Boolean

            XMLSerializer.XMLSerializer.Deserialize(Me, data)

            Return True

        End Function

        ''' <summary>Serializes the reactor state to XML.</summary>
        Public Overrides Function SaveData() As System.Collections.Generic.List(Of System.Xml.Linq.XElement)

            Return XMLSerializer.XMLSerializer.Serialize(Me)

        End Function

        ''' <summary>
        ''' Initializes a new instance of the <see cref="Reactor_ReaktoroGibbs"/> class with a name and description.
        ''' </summary>
        ''' <param name="name">The display name of the reactor.</param>
        ''' <param name="description">A brief description of the reactor.</param>
        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.New()
            Me.ComponentName = name
            Me.ComponentDescription = description

        End Sub

        ''' <summary>Validates that inlet and outlet streams are connected before calculation.</summary>
        Public Overrides Sub Validate()

        End Sub

        ''' <summary>Performs post-calculation validation checks.</summary>
        Public Overrides Sub PerformPostCalcValidation()

        End Sub

        ''' <summary>
        ''' Performs the Reaktoro Gibbs reactor calculation by invoking the Reaktoro Python library
        ''' to minimise the Gibbs free energy and determine the equilibrium product distribution.
        ''' </summary>
        ''' <param name="args">Optional calculation arguments (not used).</param>
        Public Overrides Sub Calculate(Optional ByVal args As Object = Nothing)

            If Settings.RunningPlatform() = Settings.Platform.Windows Then

                DWSIM.GlobalSettings.Settings.InitializePythonEnvironment()

            Else

                Throw New Exception("This Unit Operation is not available on Linux/macOS.")

            End If

            Dim libpath = DWSIM.Thermodynamics.ReaktoroPropertyPackage.ReaktoroLoader.Initialize()

            Dim msin = GetInletMaterialStream(0)
            Dim msout = GetOutletMaterialStream(0)

            Dim esout = GetOutletEnergyStream(1)

            Using Py.GIL

                If libpath <> "" Then

                    Dim sys As Object = Py.Import("sys")
                    sys.path.append(libpath)

                    Dim os As Object = Py.Import("os")

                    Dim dllpath = Path.Combine(libpath, "reaktoro")
                    Dim shareddllpath = Path.Combine(Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location), "python_packages", "reaktoro_shared")

                    os.add_dll_directory(dllpath)
                    os.add_dll_directory(shareddllpath)
                    os.add_dll_directory(Settings.PythonPath)

                End If

                Dim reaktoro As Object = Py.Import("reaktoro")

                'Initialize a thermodynamic database

                Dim db As Object = Nothing
                If UseExternalDatabase Then
                    Dim dbpath = Path.Combine(IO.Path.GetTempPath(), ExternalDatabaseFileName)
                    File.WriteAllText(dbpath, ExternalDatabaseContents)
                    dbpath = dbpath.Replace("\", "\\")
                    db = reaktoro.Database(dbpath)
                Else
                    db = reaktoro.Database(DatabaseName)
                End If

                'Define the chemical system
                Dim editor = reaktoro.ChemicalEditor(db)

                Dim elstring As String = ""

                For Each el In ElementsList
                    elstring += el + " "
                Next
                elstring = elstring.Trim()

                If GaseousPhase Then editor.addGaseousPhaseWithElements(elstring)

                If AqueousPhase Then
                    Dim aqueousPhase = editor.addAqueousPhaseWithElements(elstring)
                    aqueousPhase.setChemicalModelHKF()
                    aqueousPhase.setActivityModelDrummondCO2()
                End If

                If LiquidPhase Then editor.addLiquidPhaseWithElements(elstring)

                If MineralPhase Then editor.addMineralPhaseWithElements(elstring)

                'Construct the chemical system

                Dim mySystem = reaktoro.ChemicalSystem(editor)

                'Define the chemical equilibrium problem

                Dim problem = reaktoro.EquilibriumProblem(mySystem)

                problem.setTemperature(msin.GetTemperature(), "kelvin")
                problem.setPressure(msin.GetPressure(), "pascal")

                For Each item In CompoundsList
                    If FlowSheet.SelectedCompounds.ContainsKey(item) Then
                        Dim compound = FlowSheet.SelectedCompounds(item)
                        problem.add(CompoundNames(item), msin.Phases(0).Compounds(item).MolarFlow.GetValueOrDefault(), "mol")
                    End If
                Next

                'Calculate the chemical equilibrium state

                Dim state = reaktoro.equilibrate(problem)

                Dim properties = state.properties

                Dim species = mySystem.species()

                Dim amounts = state.speciesAmounts()

                Dim speciesAmountsFinal As New Dictionary(Of String, Double)
                Dim compoundAmountsFinal As New Dictionary(Of String, Double)

                Dim i As Integer

                Dim newspecies As New List(Of String)

                For i = 0 To species.Length - 1
                    Dim name = species(i).name.ToString()
                    newspecies.Add(name)
                    If Not SpeciesMaps.ContainsKey(name) Then
                        SpeciesMaps.Add(name, "")
                    End If
                    If SpeciesMaps(name) <> "" Then
                        speciesAmountsFinal.Add(name, amounts(i).ToString().ToDoubleFromInvariant())
                        If Not compoundAmountsFinal.ContainsKey(SpeciesMaps(name)) Then
                            compoundAmountsFinal.Add(SpeciesMaps(name), 0.0)
                        End If
                        compoundAmountsFinal(SpeciesMaps(name)) += amounts(i).ToString().ToDoubleFromInvariant()
                    End If
                Next

                Dim oldspecies = SpeciesMaps.Keys.ToList()

                For Each sp In oldspecies
                    If Not newspecies.Contains(sp) Then
                        Try
                            SpeciesMaps.Remove(sp)
                        Catch ex As Exception
                        End Try
                    End If
                Next

                Dim names = msin.Phases(0).Compounds.Keys.ToList()

                Dim N0 = msin.Phases(0).Compounds.Values.Select(Function(c) c.MolarFlow.GetValueOrDefault()).ToList()

                Dim Nf = New List(Of Double)(N0)

                For i = 0 To N0.Count - 1
                    If compoundAmountsFinal.ContainsKey(names(i)) Then
                        Nf(i) = compoundAmountsFinal(names(i))
                    Else
                        Nf(i) = N0(i)
                    End If
                Next

                'conversions

                ComponentConversions.Clear()
                For i = 0 To N0.Count - 1
                    Dim conv = (N0(i) - Nf(i)) / N0(i)
                    If conv > 0 Then
                        ComponentConversions.Add(names(i), conv)
                    End If
                Next

                'reaction heat

                Dim DHr As Double = 0

                For Each sb As Compound In msin.Phases(0).Compounds.Values
                    If compoundAmountsFinal.ContainsKey(sb.Name) Then
                        DHr += -sb.ConstantProperties.IG_Enthalpy_of_Formation_25C * sb.ConstantProperties.Molar_Weight * (Nf(names.IndexOf(sb.Name)) - N0(names.IndexOf(sb.Name))) / 1000.0
                    End If
                Next

                esout.EnergyFlow = DHr

                msout.Clear()
                msout.ClearAllProps()

                msout.SetOverallComposition(Nf.ToArray().MultiplyConstY(1.0 / Nf.Sum))
                msout.SetMolarFlow(Nf.Sum)
                msout.SetPressure(msin.GetPressure - DeltaP.GetValueOrDefault())
                msout.SetTemperature(msin.GetTemperature)
                msout.SetFlashSpec("PT")

                msout.AtEquilibrium = False

            End Using


        End Sub

        ''' <summary>Clears the calculated results from the outlet material streams.</summary>
        Public Overrides Sub DeCalculate()

            Dim j As Integer

            Dim ms As MaterialStream
            Dim cp As IConnectionPoint

            cp = Me.GraphicObject.OutputConnectors(0)
            If cp.IsAttached Then
                ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    Dim comp As BaseClasses.Compound
                    j = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                        j += 1
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.molarfraction = 1
                    .GraphicObject.Calculated = False
                End With
            End If

            cp = Me.GraphicObject.OutputConnectors(1)
            If cp.IsAttached Then
                ms = FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedTo.Name)
                With ms
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.enthalpy = Nothing
                    Dim comp As BaseClasses.Compound
                    j = 0
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                        j += 1
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.massfraction = 1
                    .Phases(0).Properties.molarfraction = 1
                    .GraphicObject.Calculated = False
                End With
            End If

        End Sub

        ''' <summary>Returns the icon bitmap as a byte array.</summary>
        Public Overrides Function GetIconBitmapBytes() As Byte()

            Return GetBytesFromResource("DWSIM.UnitOperations.reactor_reaktoro.png")

        End Function

        ''' <summary>Draws the unit operation icon on the given SkiaSharp canvas.</summary>
        ''' <param name="g">The graphics canvas object.</param>
        Public Sub Draw(g As Object) Implements IExternalUnitOperation.Draw

            Dim canvas As SKCanvas = DirectCast(g, SKCanvas)

            If UseEmbeddedImage = True AndAlso EmbeddedImageData <> "" Then

                Try
                    Dim p As New SKPaint
                    With p
                        p.IsAntialias = GlobalSettings.Settings.DrawingAntiAlias
                        p.FilterQuality = SKFilterQuality.High
                    End With

                    Using image As SKImage = EmbeddedImageGraphic.Base64ToImage(EmbeddedImageData)
                        canvas.DrawImage(image, New SKRect(GraphicObject.X, GraphicObject.Y, GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height), p)
                    End Using
                Catch ex As Exception
                End Try

            Else

                If Image Is Nothing Then

                    Using streamBG = New MemoryStream(GetBytesFromResource("DWSIM.UnitOperations.reactor_reaktoro.png"))
                        Using bitmap = SKBitmap.Decode(streamBG)
                            Image = SKImage.FromBitmap(bitmap)
                        End Using
                    End Using

                    Try
                        File.Delete(ImagePath)
                    Catch ex As Exception
                    End Try

                End If

                Using p As New SKPaint With {.IsAntialias = GlobalSettings.Settings.DrawingAntiAlias, .FilterQuality = SKFilterQuality.High}
                    canvas.DrawImage(Image, New SKRect(GraphicObject.X, GraphicObject.Y, GraphicObject.X + GraphicObject.Width, GraphicObject.Y + GraphicObject.Height), p)
                End Using

            End If

        End Sub

        ''' <summary>Creates the graphic connector (port) definitions for the unit operation on the flowsheet.</summary>
        Public Sub CreateConnectors() Implements IExternalUnitOperation.CreateConnectors

            Dim w, h, x, y As Double
            w = GraphicObject.Width
            h = GraphicObject.Height
            x = GraphicObject.X
            y = GraphicObject.Y

            Dim myIC1 As New ConnectionPoint

            myIC1.Position = New Point(x, y + h / 2)
            myIC1.Type = ConType.ConIn
            myIC1.Direction = ConDir.Right

            Dim myOC1 As New ConnectionPoint
            myOC1.Position = New Point(x + w, y + h / 2)
            myOC1.Type = ConType.ConOut
            myOC1.Direction = ConDir.Right

            Dim myOC2 As New ConnectionPoint
            myOC2.Position = New Point(x + w / 2, y + h)
            myOC2.Type = ConType.ConOut
            myOC2.Direction = ConDir.Down
            myOC2.Type = ConType.ConEn

            With GraphicObject.InputConnectors
                If .Count = 1 Then
                    .Item(0).Position = New Point(x, y + h / 2)
                Else
                    .Add(myIC1)
                End If
                .Item(0).ConnectorName = "Inlet"
            End With

            With GraphicObject.OutputConnectors
                If .Count = 2 Then
                    .Item(0).Position = New Point(x + w, y + h / 2)
                    .Item(1).Position = New Point(x + w / 2, y + h)
                Else
                    .Add(myOC1)
                    .Add(myOC2)
                End If
                .Item(0).ConnectorName = "Outlet"
                .Item(1).ConnectorName = "Heat Outlet"
            End With

            Me.GraphicObject.EnergyConnector.Active = False

        End Sub

        ''' <summary>Populates the cross-platform editor panel with controls (not implemented for this UO).</summary>
        Public Sub PopulateEditorPanel(ctner As Object) Implements IExternalUnitOperation.PopulateEditorPanel

            If TypeOf ctner Is AvaloniaEditorPanel Then PopulateEditorPanelAvalonia(DirectCast(ctner, AvaloniaEditorPanel)) : Return
        End Sub

        Private Sub PopulateEditorPanelAvalonia(container As AvaloniaEditorPanel)

            container.CreateAndAddLabelRow("Reaktoro Database")

            container.CreateAndAddStringEditorRow("Database Name", DatabaseName,
                                                  Sub(tb, e) DatabaseName = tb.Text)

            container.CreateAndAddCheckBoxRow("Use External Database File", UseExternalDatabase,
                                              Sub(cb, e)
                                                  UseExternalDatabase = cb.IsChecked.GetValueOrDefault()
                                              End Sub)

            container.CreateAndAddStringEditorRow("External Database File Name", ExternalDatabaseFileName,
                                                  Sub(tb, e) ExternalDatabaseFileName = tb.Text)

            container.CreateAndAddLabelRow("Phases")

            container.CreateAndAddCheckBoxRow("Aqueous Phase", AqueousPhase,
                                              Sub(cb, e) AqueousPhase = cb.IsChecked.GetValueOrDefault())

            container.CreateAndAddCheckBoxRow("Gaseous Phase", GaseousPhase,
                                              Sub(cb, e) GaseousPhase = cb.IsChecked.GetValueOrDefault())

            container.CreateAndAddCheckBoxRow("Liquid Phase", LiquidPhase,
                                              Sub(cb, e) LiquidPhase = cb.IsChecked.GetValueOrDefault())

            container.CreateAndAddCheckBoxRow("Mineral Phase", MineralPhase,
                                              Sub(cb, e) MineralPhase = cb.IsChecked.GetValueOrDefault())

            container.CreateAndAddDescriptionRow("Compound / element / species mapping is configured through the Windows editor. Re-open from Object Properties.")

        End Sub

        ''' <summary>Creates and returns a new instance of this unit operation type for deserialization.</summary>
        Public Function ReturnInstance(typename As String) As Object Implements IExternalUnitOperation.ReturnInstance

            Return New Reactor_ReaktoroGibbs

        End Function

        ''' <summary>Returns an array of property identifiers for the specified property type.</summary>
        Public Overrides Function GetProperties(proptype As PropertyType) As String()

            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Dim basecol = MyBase.GetProperties(proptype)
            If basecol.Length > 0 Then proplist.AddRange(basecol)
            Select Case proptype
                Case PropertyType.WR
                    proplist.Add("Pressure Drop")
                Case PropertyType.ALL
                    For Each item In ComponentConversions
                        proplist.Add(item.Key + ": Conversion")
                    Next
            End Select

            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing

        End Function

        ''' <summary>Returns the unit string for the specified property.</summary>
        Public Overrides Function GetPropertyUnit(prop As String, Optional su As IUnitsOfMeasure = Nothing) As String

            If su Is Nothing Then su = New SystemsOfUnits.SI()
            If prop.Contains("Conversion") Then
                Return "%"
            ElseIf prop.Equals("Pressure Drop") Then
                Return su.deltaP
            End If

        End Function

        ''' <summary>Returns the value of the specified property, converted to the given unit system.</summary>
        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object

            If su Is Nothing Then su = New SystemsOfUnits.SI()
            Dim val0 As Object = MyBase.GetPropertyValue(prop, su)

            If Not val0 Is Nothing Then
                Return val0
            Else
                Dim value As Double
                If prop.Contains("Conversion") Then
                    Dim comp = prop.Split(": ")(0)
                    If ComponentConversions.ContainsKey(comp) Then
                        value = ComponentConversions(comp) * 100
                    Else
                        value = 0.0
                    End If
                ElseIf prop.Equals("Pressure Drop") Then
                    Return DeltaP.GetValueOrDefault().ConvertFromSI(su.deltaP)
                End If
                Return value
            End If

        End Function

        ''' <summary>Sets the value of the specified property from the given value and unit system.</summary>
        Public Overrides Function SetPropertyValue(prop As String, propval As Object, Optional su As IUnitsOfMeasure = Nothing) As Boolean

            If su Is Nothing Then su = New SystemsOfUnits.SI()
            If prop.Equals("Pressure Drop") Then
                DeltaP = Convert.ToDouble(propval).ConvertToSI(su.deltaP)
            End If
            Return True

        End Function

        ''' <summary>Returns a newline-separated list of Reaktoro species names from the selected database.</summary>
        Public Function GetListOfCompounds() As String

            If Settings.RunningPlatform() = Settings.Platform.Windows Then

                DWSIM.GlobalSettings.Settings.InitializePythonEnvironment()

            End If

            Dim libpath = DWSIM.Thermodynamics.ReaktoroPropertyPackage.ReaktoroLoader.Initialize()

            Using Py.GIL

                If libpath <> "" Then

                    Dim sys As Object = Py.Import("sys")
                    sys.path.append(libpath)

                    Dim os As Object = Py.Import("os")

                    Dim dllpath = Path.Combine(libpath, "reaktoro")
                    Dim shareddllpath = Path.Combine(Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location), "python_packages", "reaktoro_shared")

                    os.add_dll_directory(dllpath)
                    os.add_dll_directory(shareddllpath)
                    os.add_dll_directory(Settings.PythonPath)

                End If

                Dim reaktoro As Object = Py.Import("reaktoro")

                'Initialize a thermodynamic database
                Dim db As Object = Nothing
                If UseExternalDatabase Then
                    Dim dbpath = Path.Combine(IO.Path.GetTempPath(), ExternalDatabaseFileName)
                    File.WriteAllText(dbpath, ExternalDatabaseContents)
                    db = reaktoro.Database(dbpath)
                Else
                    db = reaktoro.Database(DatabaseName)
                End If

                Dim aql As Object = db.aqueousSpecies()
                Dim gql As Object = db.gaseousSpecies()
                Dim lql As Object = db.liquidSpecies()
                Dim mql As Object = db.mineralSpecies()

                Dim sb As New StringBuilder
                Dim i As Integer = 0

                sb.AppendLine("Aqueous Species:")
                sb.AppendLine()
                For i = 0 To aql.Length - 1
                    sb.AppendLine(aql(i).name.ToString() + " (" + aql(i).formula.ToString() + ")")
                Next
                sb.AppendLine()
                sb.AppendLine("Gaseous Species:")
                sb.AppendLine()
                For i = 0 To gql.Length - 1
                    sb.AppendLine(gql(i).name.ToString() + " (" + gql(i).formula.ToString() + ")")
                Next
                sb.AppendLine()
                sb.AppendLine("Liquid (Non-Aqueous) Species:")
                sb.AppendLine()
                For i = 0 To lql.Length - 1
                    sb.AppendLine(lql(i).name.ToString() + " (" + lql(i).formula.ToString() + ")")
                Next
                sb.AppendLine()
                sb.AppendLine("Mineral Species:")
                sb.AppendLine()
                For i = 0 To mql.Length - 1
                    sb.AppendLine(mql(i).name.ToString() + " (" + mql(i).formula.ToString() + ")")
                Next

                Return sb.ToString()

            End Using

        End Function

    End Class

End Namespace