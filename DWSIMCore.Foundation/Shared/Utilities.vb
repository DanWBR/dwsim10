Imports System.Reflection
Imports System.IO

Public Class Utility

    Shared Function GetSimulationFileDetails(xdoc As XDocument) As Dictionary(Of String, String)

        Dim props As New Dictionary(Of String, String)

        'check version

        Dim sver = New Version("1.0.0.0")

        Try
            props.Add("DWSIMVersion", xdoc.Element("DWSIM_Simulation_Data").Element("GeneralInfo").Element("BuildVersion").Value)
            props.Add("OSInfo", xdoc.Element("DWSIM_Simulation_Data").Element("GeneralInfo").Element("OSInfo").Value)
            props.Add("SavedOn", xdoc.Element("DWSIM_Simulation_Data").Element("GeneralInfo").Element("SavedOn").Value)
        Catch ex As Exception
        End Try
        Try
            props.Add("SimName", xdoc.Element("DWSIM_Simulation_Data").Element("Settings").Element("SimulationName").Value)
        Catch ex As Exception
            props.Add("SimName", "")
        End Try
        Try
            props.Add("SimAuthor", xdoc.Element("DWSIM_Simulation_Data").Element("Settings").Element("SimulationAuthor").Value)
        Catch ex As Exception
            props.Add("SimAuthor", "")
        End Try

        props.Add("Compounds", xdoc.Element("DWSIM_Simulation_Data").Element("Compounds").Elements.Count)
        props.Add("PropertyPackages", xdoc.Element("DWSIM_Simulation_Data").Element("PropertyPackages").Elements.Count)
        props.Add("SimulationObjects", xdoc.Element("DWSIM_Simulation_Data").Element("SimulationObjects").Elements.Count)
        props.Add("Reactions", xdoc.Element("DWSIM_Simulation_Data").Element("Reactions").Elements.Count)

        Return props

    End Function

    Shared Sub UpdateElement(xel As XElement)

        If xel.Name.LocalName.Equals("Type") Then

            If xel.Value.StartsWith("DWSIM.Thermodynamics") Then
                xel.Value = xel.Value.Replace("DWSIM.Thermodynamics", "DWSIMCore.Foundation")
            End If

            If xel.Name = "TipoObjeto" Then xel.Name = "ObjectType"
            If xel.Name = "Nome" Then xel.Name = "Name"
            If xel.Name = "Descricao" Then xel.Name = "Description"
            If xel.Name = "Tipo" Then xel.Name = "Type"
            If xel.Name = "FracaoMolar" Then xel.Name = "MoleFraction"
            If xel.Name = "FracaoMassica" Then xel.Name = "MassFraction"
            If xel.Name = "FracaoDePetroleo" Then xel.Name = "PetroleumFraction"
            If xel.Value = "Nenhum" Then xel.Value = "None"
            If xel.Value = "Destino" Then xel.Value = "Target"
            If xel.Value = "Fonte" Then xel.Value = "Source"
            If xel.Value = "Manipulada" Then xel.Value = "Manipulated"
            If xel.Value = "Referencia" Then xel.Value = "Referenced"
            If xel.Value = "Controlada" Then xel.Value = "Controlled"

            If xel.Value.EndsWith("Streams.MaterialStream") Then xel.Value = "DWSIMCore.Foundation.Streams.MaterialStream"

            If xel.Value.EndsWith("Streams.EnergyStream") Then xel.Value = "DWSIMCore.Foundation.Streams.EnergyStream"

            If xel.Value.EndsWith("UnitOperations.Vessel") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Vessel"
            If xel.Value.EndsWith("UnitOperations.Compressor") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Compressor"
            If xel.Value.EndsWith("UnitOperations.Expander") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Expander"
            If xel.Value.EndsWith("UnitOperations.Heater") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Heater"
            If xel.Value.EndsWith("UnitOperations.Cooler") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Cooler"
            If xel.Value.EndsWith("UnitOperations.HeatExchanger") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.HeatExchanger"
            If xel.Value.EndsWith("UnitOperations.Filter") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Filter"
            If xel.Value.EndsWith("UnitOperations.Mixer") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Mixer"
            If xel.Value.EndsWith("UnitOperations.Splitter") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Splitter"
            If xel.Value.EndsWith("UnitOperations.ComponentSeparator") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.ComponentSeparator"
            If xel.Value.EndsWith("UnitOperations.ShortcutColumn") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.ShortcutColumn"
            If xel.Value.EndsWith("UnitOperations.CustomUO") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.CustomUO"
            If xel.Value.EndsWith("UnitOperations.CapeOpenUO") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.CapeOpenUO"
            If xel.Value.EndsWith("UnitOperations.Flowsheet") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Flowsheet"
            If xel.Value.EndsWith("UnitOperations.OrificePlate") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.OrificePlate"
            If xel.Value.EndsWith("UnitOperations.SolidsSeparator") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.SolidsSeparator"
            If xel.Value.EndsWith("UnitOperations.ExcelUO") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.ExcelUO"
            If xel.Value.EndsWith("UnitOperations.Tank") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Tank"
            If xel.Value.EndsWith("UnitOperations.Valve") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Valve"
            If xel.Value.EndsWith("UnitOperations.Pump") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Pump"
            If xel.Value.EndsWith("UnitOperations.Pipe") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.Pipe"
            If xel.Value.EndsWith("UnitOperations.DistillationColumn") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.DistillationColumn"
            If xel.Value.EndsWith("UnitOperations.AbsorptionColumn") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.AbsorptionColumn"
            If xel.Value.EndsWith("UnitOperations.ReboiledAbsorber") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.ReboiledAbsorber"
            If xel.Value.EndsWith("UnitOperations.RefluxedAbsorber") Then xel.Value = "DWSIMCore.Foundation.UnitOperations.RefluxedAbsorber"

            If xel.Value.EndsWith("Reactors.Reactor_Conversion") Then xel.Value = "DWSIMCore.Foundation.Reactors.Reactor_Conversion"
            If xel.Value.EndsWith("Reactors.Reactor_Equilibrium") Then xel.Value = "DWSIMCore.Foundation.Reactors.Reactor_Equilibrium"
            If xel.Value.EndsWith("Reactors.Reactor_Gibbs") Then xel.Value = "DWSIMCore.Foundation.Reactors.Reactor_Gibbs"
            If xel.Value.EndsWith("Reactors.Reactor_PFR") Then xel.Value = "DWSIMCore.Foundation.Reactors.Reactor_PFR"
            If xel.Value.EndsWith("Reactors.Reactor_CSTR") Then xel.Value = "DWSIMCore.Foundation.Reactors.Reactor_CSTR"

            If xel.Value.EndsWith("SpecialOps.Adjust") Then xel.Value = "DWSIMCore.Foundation.SpecialOps.Adjust"
            If xel.Value.EndsWith("SpecialOps.Spec") Then xel.Value = "DWSIMCore.Foundation.SpecialOps.Spec"
            If xel.Value.EndsWith("SpecialOps.Recycle") Then xel.Value = "DWSIMCore.Foundation.SpecialOps.Recycle"
            If xel.Value.EndsWith("SpecialOps.EnergyRecycle") Then xel.Value = "DWSIMCore.Foundation.SpecialOps.EnergyRecycle"


            If xel.Name = "Fill" Then xel.Value = "True"

            If xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorEquilibriumGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.EquilibriumReactorGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorConversionGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.ConversionReactorGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorGibbsGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.GibbsReactorGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorCSTRGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.CSTRGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorPFRGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.PFRGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.NodeInGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.MixerGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.NodeOutGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.SplitterGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.HeaterGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.HeaterGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CoolerGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.CoolerGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CompressorGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.CompressorGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ExpanderGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.ExpanderGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ShorcutColumnGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.ShortcutColumnGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.DistillationColumnGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.RigorousColumnGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.AbsorptionColumnGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.AbsorptionColumnGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.FlowsheetUOGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.FlowsheetGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CustomUOGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.ScriptGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CapeOpenUOGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.CAPEOPENGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ExcelUOGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.SpreadsheetGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.PipeGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.PipeSegmentGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.MaterialStreamGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.MaterialStreamGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.EnergyStreamGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.EnergyStreamGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.VesselGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.VesselGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ValveGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.ValveGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.RecycleGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.RecycleGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.EnergyRecycleGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.EnergyRecycleGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.SpecGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.SpecGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.TextGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.TextGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.OrificePlateGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.OrificePlateGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.AdjustGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.AdjustGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.AnalogGaugeGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.AnalogGaugeGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.DigitalGaugeGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.DigitalGaugeGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.InputGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.InputGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.LevelGaugeGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.LevelGaugeGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.PIDControllerGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.PIDControllerGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.SwitchGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.SwitchGraphic"
            ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.HeatExchangerGraphic") Then
                xel.Value = "DWSIMCore.Foundation.GraphicObjects.Shapes.HeatExchangerGraphic"
            End If

            If xel.Value.StartsWith("DWSIM.DrawingTools.GraphicObjects.Shapes") Then
                xel.Value = xel.Value.Replace("DWSIM.DrawingTools.GraphicObjects", "DWSIMCore.Foundation.GraphicObjects.Shapes")
            End If

            If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp") Then
                xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp", "DWSIMCore.Foundation")
            End If

            If xel.Value.StartsWith("DWSIM.GraphicObjects.Tables") Then
                xel.Value = xel.Value.Replace("DWSIM.GraphicObjects", "DWSIMCore.Foundation.GraphicObjects.Tables")
            End If

        End If

    End Sub

    Shared Sub UpdateElementForNewUI(xel As XElement)

        If xel.Name = "TipoObjeto" Then xel.Name = "ObjectType"
        If xel.Name = "Nome" Then xel.Name = "Name"
        If xel.Name = "Descricao" Then xel.Name = "Description"
        If xel.Name = "Tipo" Then xel.Name = "Type"
        If xel.Name = "FracaoMolar" Then xel.Name = "MoleFraction"
        If xel.Name = "FracaoMassica" Then xel.Name = "MassFraction"
        If xel.Name = "FracaoDePetroleo" Then xel.Name = "PetroleumFraction"
        If xel.Value = "Nenhum" Then xel.Value = "None"
        If xel.Value = "Destino" Then xel.Value = "Target"
        If xel.Value = "Fonte" Then xel.Value = "Source"
        If xel.Value = "Manipulada" Then xel.Value = "Manipulated"
        If xel.Value = "Referencia" Then xel.Value = "Referenced"
        If xel.Value = "Controlada" Then xel.Value = "Controlled"

        If xel.Name = "Fill" Then xel.Value = "True"

        If xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorEquilibriumGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EquilibriumReactorGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorConversionGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ConversionReactorGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorGibbsGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.GibbsReactorGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorCSTRGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CSTRGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ReactorPFRGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PFRGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.NodeInGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.MixerGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.NodeOutGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SplitterGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.HeaterGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CoolerGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CoolerGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CompressorGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ExpanderGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExpanderGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ShorcutColumnGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ShortcutColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.DistillationColumnGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RigorousColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.AbsorptionColumnGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AbsorptionColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.FlowsheetUOGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.FlowsheetGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CustomUOGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ScriptGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.CapeOpenUOGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CAPEOPENGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.ExcelUOGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpreadsheetGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.PipeGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PipeSegmentGraphic"
        ElseIf xel.Value.Equals("DWSIM.DrawingTools.GraphicObjects.TextGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.TextGraphic"
        End If

        If xel.Value.StartsWith("DWSIM.DrawingTools.GraphicObjects") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.DrawingTools.GraphicObjects", "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes")
        End If

        If xel.Value.StartsWith("DWSIM.GraphicObjects") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.GraphicObjects", "DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables")
        End If

    End Sub

    Shared Sub UpdateElementFromNewUI(xel As XElement)

        If xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EquilibriumReactorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorEquilibriumGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ConversionReactorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorConversionGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.GibbsReactorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorGibbsGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CSTRGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorCSTRGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PFRGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorPFRGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.MixerGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.NodeInGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SplitterGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.NodeOutGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.HeaterGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CoolerGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.CoolerGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.CompressorGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExpanderGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ExpanderGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ShortcutColumnGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ShorcutColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RigorousColumnGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.DistillationColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AbsorptionColumnGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.AbsorptionColumnGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.FlowsheetGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.FlowsheetUOGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ScriptGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.CustomUOGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CAPEOPENGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.CapeOpenUOGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpreadsheetGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ExcelUOGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PipeSegmentGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.PipeGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.TextGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.TextGraphic"
        End If

        If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes", "DWSIM.DrawingTools.GraphicObjects")
        End If

        If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables", "DWSIM.GraphicObjects")
        End If

        If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp.GraphicObjects.Charts", "DWSIM.DrawingTools.GraphicObjects")
        End If

    End Sub

    Shared Sub UpdateElementForMobileXMLLoading(xel As XElement)

        If xel.Name = "TipoObjeto" Then xel.Name = "ObjectType"
        If xel.Name = "Nome" Then xel.Name = "Name"
        If xel.Name = "Descricao" Then xel.Name = "Description"
        If xel.Name = "Tipo" Then xel.Name = "Type"
        If xel.Name = "FracaoMolar" Then xel.Name = "MoleFraction"
        If xel.Name = "FracaoMassica" Then xel.Name = "MassFraction"
        If xel.Name = "FracaoDePetroleo" Then xel.Name = "PetroleumFraction"
        If xel.Value = "Nenhum" Then xel.Value = "None"
        If xel.Value = "Destino" Then xel.Value = "Target"
        If xel.Value = "Fonte" Then xel.Value = "Source"
        If xel.Value = "Manipulada" Then xel.Value = "Manipulated"
        If xel.Value = "Referencia" Then xel.Value = "Referenced"
        If xel.Value = "Controlada" Then xel.Value = "Controlled"

        If xel.Name = "Fill" Then xel.Value = "True"

        If xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.EquilibriumReactorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorEquilibriumGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.ConversionReactorGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorConversionGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.CSTRGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorCSTRGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.PFRGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.ReactorPFRGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.MixerGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.NodeInGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.SplitterGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.NodeOutGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.HeaterCoolerGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.HeaterGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.CompressorExpanderGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.CompressorGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.RigorousColumnGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.DistillationColumnGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.AbsorptionColumnGraphic") Then
            xel.Value = "DWSIM.DrawingTools.GraphicObjects.AbsorptionColumnGraphic"
        End If

        If xel.Value.StartsWith("PortableFlowsheetDrawing.GraphicObjects.Shapes") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("PortableFlowsheetDrawing.GraphicObjects.Shapes", "DWSIM.DrawingTools.GraphicObjects")
        End If

        If xel.Value.EndsWith("Streams.MaterialStream") Then xel.Value = "DWSIM.Thermodynamics.Streams.MaterialStream"
        If xel.Value.EndsWith("Streams.EnergyStream") Then xel.Value = "DWSIM.UnitOperations.Streams.EnergyStream"

        If xel.Value.EndsWith("UnitOperations.Separator") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Vessel"
        If xel.Value.EndsWith("UnitOperations.AdiabaticExpanderCompressor") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Compressor"
        If xel.Value.EndsWith("UnitOperations.HeaterCooler") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Heater"
        If xel.Value.EndsWith("UnitOperations.HeatExchanger") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.HeatExchanger"
        If xel.Value.EndsWith("UnitOperations.Mixer") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Mixer"
        If xel.Value.EndsWith("UnitOperations.Splitter") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Splitter"
        If xel.Value.EndsWith("UnitOperations.ComponentSeparator") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.ComponentSeparator"
        If xel.Value.EndsWith("UnitOperations.ShortcutColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.ShortcutColumn"
        If xel.Value.EndsWith("UnitOperations.Valve") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Valve"
        If xel.Value.EndsWith("UnitOperations.Pump") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Pump"
        If xel.Value.EndsWith("UnitOperations.Pipe") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Pipe"
        If xel.Value.EndsWith("UnitOperations.DistillationColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.DistillationColumn"
        If xel.Value.EndsWith("UnitOperations.AbsorptionColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.AbsorptionColumn"

        If xel.Value.EndsWith("Reactors.Reactor_Conversion") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_Conversion"
        If xel.Value.EndsWith("Reactors.Reactor_Equilibrium") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_Equilibrium"
        If xel.Value.EndsWith("Reactors.Reactor_PFR") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_PFR"
        If xel.Value.EndsWith("Reactors.Reactor_CSTR") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_CSTR"

        If xel.Value.EndsWith("UnitOperations.Adjust") Then xel.Value = "DWSIM.UnitOperations.SpecialOps.Adjust"
        If xel.Value.EndsWith("UnitOperations.Recycle") Then xel.Value = "DWSIM.UnitOperations.SpecialOps.Recycle"

    End Sub

    Shared Sub UpdateElementForMobileXMLLoading_CrossPlatformUI(xel As XElement)

        If xel.Name = "TipoObjeto" Then xel.Name = "ObjectType"
        If xel.Name = "Nome" Then xel.Name = "Name"
        If xel.Name = "Descricao" Then xel.Name = "Description"
        If xel.Name = "Tipo" Then xel.Name = "Type"
        If xel.Name = "FracaoMolar" Then xel.Name = "MoleFraction"
        If xel.Name = "FracaoMassica" Then xel.Name = "MassFraction"
        If xel.Name = "FracaoDePetroleo" Then xel.Name = "PetroleumFraction"
        If xel.Value = "Nenhum" Then xel.Value = "None"
        If xel.Value = "Destino" Then xel.Value = "Target"
        If xel.Value = "Fonte" Then xel.Value = "Source"
        If xel.Value = "Manipulada" Then xel.Value = "Manipulated"
        If xel.Value = "Referencia" Then xel.Value = "Referenced"
        If xel.Value = "Controlada" Then xel.Value = "Controlled"

        If xel.Name = "Fill" Then xel.Value = "True"

        If xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.HeaterCoolerGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterGraphic"
        ElseIf xel.Value.Equals("PortableFlowsheetDrawing.GraphicObjects.Shapes.CompressorExpanderGraphic") Then
            xel.Value = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorGraphic"
        End If

        If xel.Value.StartsWith("PortableFlowsheetDrawing.GraphicObjects.Shapes") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("PortableFlowsheetDrawing.GraphicObjects.Shapes", "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes")
        End If

        If xel.Value.StartsWith("PortableFlowsheetDrawing.GraphicObjects.Tables") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("PortableFlowsheetDrawing.GraphicObjects.Tables", "DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables")
        End If

        If xel.Value.Contains("PortableFlowsheetDrawing.GraphicObjects.TableGraphic") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("PortableFlowsheetDrawing.GraphicObjects.TableGraphic", "DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables.TableGraphic")
        End If

        If xel.Value.EndsWith("Streams.MaterialStream") Then xel.Value = "DWSIM.Thermodynamics.Streams.MaterialStream"
        If xel.Value.EndsWith("Streams.EnergyStream") Then xel.Value = "DWSIM.UnitOperations.Streams.EnergyStream"

        If xel.Value.EndsWith("UnitOperations.Separator") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Vessel"
        If xel.Value.EndsWith("UnitOperations.AdiabaticExpanderCompressor") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Compressor"
        If xel.Value.EndsWith("UnitOperations.HeaterCooler") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Heater"
        If xel.Value.EndsWith("UnitOperations.HeatExchanger") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.HeatExchanger"
        If xel.Value.EndsWith("UnitOperations.Mixer") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Mixer"
        If xel.Value.EndsWith("UnitOperations.Splitter") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Splitter"
        If xel.Value.EndsWith("UnitOperations.ComponentSeparator") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.ComponentSeparator"
        If xel.Value.EndsWith("UnitOperations.ShortcutColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.ShortcutColumn"
        If xel.Value.EndsWith("UnitOperations.Valve") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Valve"
        If xel.Value.EndsWith("UnitOperations.Pump") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Pump"
        If xel.Value.EndsWith("UnitOperations.Pipe") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.Pipe"
        If xel.Value.EndsWith("UnitOperations.DistillationColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.DistillationColumn"
        If xel.Value.EndsWith("UnitOperations.AbsorptionColumn") Then xel.Value = "DWSIM.UnitOperations.UnitOperations.AbsorptionColumn"

        If xel.Value.EndsWith("Reactors.Reactor_Conversion") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_Conversion"
        If xel.Value.EndsWith("Reactors.Reactor_Equilibrium") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_Equilibrium"
        If xel.Value.EndsWith("Reactors.Reactor_PFR") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_PFR"
        If xel.Value.EndsWith("Reactors.Reactor_CSTR") Then xel.Value = "DWSIM.UnitOperations.Reactors.Reactor_CSTR"

        If xel.Value.EndsWith("UnitOperations.Adjust") Then xel.Value = "DWSIM.UnitOperations.SpecialOps.Adjust"
        If xel.Value.EndsWith("UnitOperations.Recycle") Then xel.Value = "DWSIM.UnitOperations.SpecialOps.Recycle"

    End Sub

    Shared Sub UpdateElementForMobileXMLSaving_CrossPlatformUI(xel As XElement)

        If xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterGraphic") Then
            xel.Value = "PortableFlowsheetDrawing.GraphicObjects.Shapes.HeaterCoolerGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CoolerGraphic") Then
            xel.Value = "PortableFlowsheetDrawing.GraphicObjects.Shapes.HeaterCoolerGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorGraphic") Then
            xel.Value = "PortableFlowsheetDrawing.GraphicObjects.Shapes.CompressorExpanderGraphic"
        ElseIf xel.Value.Equals("DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.TurbineGraphic") Then
            xel.Value = "PortableFlowsheetDrawing.GraphicObjects.Shapes.CompressorExpanderGraphic"
        End If

        If xel.Value.StartsWith("DWSIM.Thermodynamics.BaseClasses") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Thermodynamics.BaseClasses", "PortableSharedClasses.BaseClasses")
        End If

        If xel.Value.StartsWith("DWSIM.Thermodynamics.BaseClasses.PhaseProperties") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Thermodynamics.BaseClasses.PhaseProperties", "PortableSharedClasses.BaseClasses.PhaseProperties")
        End If

        If xel.Value.StartsWith("DWSIM.Thermodynamics.PropertyPackages") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Thermodynamics.PropertyPackages", "PortableDTL.DTL.SimulationObjects.PropertyPackages")
        End If

        If xel.Value.Contains("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables.TableGraphic") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables.TableGraphic", "PortableFlowsheetDrawing.GraphicObjects.TableGraphic")
        End If

        If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp.GraphicObjects.Tables", "PortableFlowsheetDrawing.GraphicObjects")
        End If

        If xel.Value.StartsWith("DWSIM.Drawing.SkiaSharp") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.Drawing.SkiaSharp", "PortableFlowsheetDrawing")
        End If

        If xel.Value.StartsWith("DWSIM.DrawingTools") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.DrawingTools", "PortableFlowsheetDrawing")
        End If

        If xel.Value.StartsWith("DWSIM.SharedClasses") And xel.Name = "Type" Then
            xel.Value = xel.Value.Replace("DWSIM.SharedClasses", "PortableSharedClasses")
        End If

        If xel.Value.Equals("DWSIM.Thermodynamics.Streams.MaterialStream") Then xel.Value = "PortableDTL.DTL.SimulationObjects.Streams.MaterialStream"
        If xel.Value.Equals("DWSIM.UnitOperations.Streams.EnergyStream") Then xel.Value = "PortableDTL.DTL.SimulationObjects.Streams.EnergyStream"

        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Vessel") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Separator"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Compressor") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.AdiabaticExpanderCompressor"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Expander") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.AdiabaticExpanderCompressor"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Heater") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.HeaterCooler"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Cooler") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.HeaterCooler"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.HeatExchanger") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.HeatExchanger"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Mixer") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Mixer"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Splitter") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Splitter"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.ComponentSeparator") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.ComponentSeparator"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.ShortcutColumn") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.ShortcutColumn"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Valve") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Valve"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Pump") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Pump"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.Pipe") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Pipe"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.DistillationColumn") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.DistillationColumn"
        If xel.Value.Equals("DWSIM.UnitOperations.UnitOperations.AbsorptionColumn") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.AbsorptionColumn"

        If xel.Value.Equals("DWSIM.UnitOperations.Reactors.Reactor_Conversion") Then xel.Value = "PortableDTL.Reactors.Reactor_Conversion"
        If xel.Value.Equals("DWSIM.UnitOperations.Reactors.Reactor_Equilibrium") Then xel.Value = "PortableDTL.Reactors.Reactor_Equilibrium"
        If xel.Value.Equals("DWSIM.UnitOperations.Reactors.Reactor_PFR") Then xel.Value = "PortableDTL.Reactors.Reactor_PFR"
        If xel.Value.Equals("DWSIM.UnitOperations.Reactors.Reactor_CSTR") Then xel.Value = "PortableDTL.Reactors.Reactor_CSTR"
        If xel.Value.Equals("DWSIM.UnitOperations.Reactors.Reactor_Gibbs") Then xel.Value = "PortableDTL.DTL.Reactors.Reactor_Gibbs"

        If xel.Value.Equals("DWSIM.UnitOperations.SpecialOps.Adjust") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Adjust"
        If xel.Value.Equals("DWSIM.UnitOperations.SpecialOps.Recycle") Then xel.Value = "PortableDTL.DTL.SimulationObjects.UnitOperations.Recycle"

    End Sub

    Shared Function LoadAdditionalUnitOperations() As List(Of IExternalUnitOperation)

        Dim euos As New List(Of IExternalUnitOperation)

        Dim ppath As String = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly.Location), "unitops")
        If Directory.Exists(ppath) Then
            Dim otheruos As String() = Directory.GetFiles(ppath, "*.dll", SearchOption.TopDirectoryOnly)
            For Each fpath In otheruos
                Try
                    Dim pplist As List(Of IExternalUnitOperation) = GetUnitOperations(Assembly.LoadFile(fpath))
                    For Each pp In pplist
                        euos.Add(pp)
                    Next
                Catch ex As Exception
                    'Logging.Logger.LogError("Loading Additional Unit Operations", ex)
                End Try
            Next
        End If

        ppath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly.Location), "extenders")
        If Directory.Exists(ppath) Then
            Dim otheruos As String() = Directory.GetFiles(ppath, "*.dll", SearchOption.TopDirectoryOnly)
            For Each fpath In otheruos
                Try
                    Dim pplist As List(Of IExternalUnitOperation) = GetUnitOperations(Assembly.LoadFile(fpath))
                    For Each pp In pplist
                        euos.Add(pp)
                    Next
                Catch ex As Exception
                    'Logging.Logger.LogError("Loading Additional Unit Operations (Extenders)", ex)
                End Try
            Next
        End If

        Return euos

    End Function

    Shared Function LoadAdditionalPropertyPackages() As List(Of IPropertyPackage)

        Dim ppacks As New List(Of IPropertyPackage)

        Dim thermoceos As String = Path.GetDirectoryName(Assembly.GetAssembly(New SystemsOfUnits.SI().GetType()).Location) + Path.DirectorySeparatorChar + "DWSIM.Thermodynamics.ThermoC.dll"
        If File.Exists(thermoceos) Then
            Dim tca = Assembly.LoadFile(thermoceos)
            Dim pplist As List(Of IPropertyPackage) = GetPropertyPackages(tca)
            For Each pp In pplist
                ppacks.Add(pp)
            Next
        End If

        Dim ppath As String = Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(New SystemsOfUnits.SI().GetType()).Location), "ppacks")
        If Directory.Exists(ppath) Then
            Try
                Dim otherpps As String() = Directory.GetFiles(ppath, "*.dll", SearchOption.TopDirectoryOnly)
                For Each fpath In otherpps
                    Dim pplist As List(Of IPropertyPackage) = GetPropertyPackages(Assembly.LoadFile(fpath))
                    For Each pp In pplist
                        ppacks.Add(pp)
                    Next
                Next
            Catch ex As Exception
                'Logging.Logger.LogError("Loading Additional Property Packages", ex)
            End Try
        End If

        Dim directories = New List(Of String)

        Dim d1 = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "extenders")
        Dim d2 = Path.Combine(Directory.GetParent(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).FullName, "extenders")
        Dim d3 = Path.Combine(Directory.GetCurrentDirectory(), "extenders")

        directories.Add(d1)
        If Not directories.Contains(d2) Then directories.Add(d2)
        If Not directories.Contains(d3) Then directories.Add(d3)

        For Each ppath In directories
            If Directory.Exists(ppath) Then
                Try
                    Dim otherpps As String() = Directory.GetFiles(ppath, "*.dll", SearchOption.TopDirectoryOnly)
                    For Each fpath In otherpps
                        Dim pplist As List(Of IPropertyPackage) = GetPropertyPackages(Assembly.LoadFile(fpath))
                        For Each pp In pplist
                            ppacks.Add(pp)
                        Next
                    Next
                Catch ex As Exception
                    'Logging.Logger.LogError("Loading Additional Property Packages (Extenders)", ex)
                End Try
            End If
        Next

        Return ppacks

    End Function

    Shared Function LoadAdditionalPropertyPackageAssemblies() As List(Of Assembly)

        Dim ppacks As New List(Of Assembly)

        Dim thermoceos As String = Path.GetDirectoryName(Assembly.GetExecutingAssembly.Location) + Path.DirectorySeparatorChar + "DWSIM.Thermodynamics.ThermoC.dll"
        If File.Exists(thermoceos) Then
            Dim tca = Assembly.LoadFile(thermoceos)
            ppacks.Add(tca)
        End If

        Dim ppath As String = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly.Location), "ppacks")
        If Directory.Exists(ppath) Then
            Try
                Dim otherpps As String() = Directory.GetFiles(ppath, "*.dll", SearchOption.TopDirectoryOnly)
                For Each fpath In otherpps
                    ppacks.Add(Assembly.LoadFile(fpath))
                Next
            Catch ex As Exception
                'Logging.Logger.LogError("Loading Additional Property Package Assemblies", ex)
            End Try
        End If

        Return ppacks

    End Function

    Shared Function GetPropertyPackages(ByVal assmbly As Assembly) As List(Of IPropertyPackage)

        Dim availableTypes As New List(Of TypeInfo)()

        Try
            availableTypes.AddRange(assmbly.DefinedTypes())
        Catch ex As Exception
            Console.WriteLine("Error loading types from assembly '" + assmbly.FullName + "': " + ex.ToString)
            'Logging.Logger.LogError("Error loading types from assembly '" + assmbly.FullName, ex)
        End Try

        Dim ppList As List(Of TypeInfo) = availableTypes.FindAll(Function(t) t.GetInterfaces().Contains(GetType(IPropertyPackage)) And Not t.IsAbstract)

        Return ppList.ConvertAll(Of IPropertyPackage)(Function(t As Type) TryCast(Activator.CreateInstance(t), IPropertyPackage))

    End Function

    Shared Function GetUnitOperations(ByVal assmbly As Assembly) As List(Of IExternalUnitOperation)

        Dim availableTypes As New List(Of TypeInfo)()

        Try
            availableTypes.AddRange(assmbly.DefinedTypes())
        Catch ex As Exception
            Console.WriteLine("Error loading types from assembly '" + assmbly.FullName + "': " + ex.ToString)
        End Try

        Dim ppList As List(Of TypeInfo) = availableTypes.FindAll(Function(t) t.GetInterfaces().Contains(GetType(IExternalUnitOperation)) And Not t.IsAbstract And Not Attribute.IsDefined(t, Type.GetType("System.ObsoleteAttribute")))

        Dim list As New List(Of IExternalUnitOperation)

        For Each item In ppList
            Dim obj = Activator.CreateInstance(item)
            If TryCast(obj, IExternalUnitOperation) IsNot Nothing Then
                list.Add(obj)
            End If
        Next

        Return list

    End Function

    Shared Function GetRuntimeVersion() As String

        Return ".NET v" & Environment.Version.ToString()

    End Function

    Public Shared Function GetTempFileName() As String

        Return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp")

    End Function

End Class