using System;
using System.IO;
using System.Linq;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.UnitOperations.UnitOperations;
using NUnit.Framework;
namespace DWSIM.Engine.SmokeTests {
[TestFixture]
public class ExtractorPhaseOrderTests {
 [OneTimeSetUp] public void Setup() {Settings.AutomationMode=true;Settings.InspectorEnabled=false;Settings.CultureInfo="en";DWSIM.FlowsheetBase.FlowsheetBase.AddPropPacks();}
 private static DWSIM.DynamicRunner.Flowsheet Load(string sample="LiquidLiquidExtraction.dwxmz") {
  var folder=TestContext.CurrentContext.TestDirectory;
  while(folder!=null && !Directory.Exists(Path.Combine(folder,"tests","flowsheets")))folder=Path.GetDirectoryName(folder);
  Assert.That(folder,Is.Not.Null);
  var fs=new DWSIM.DynamicRunner.Flowsheet(null,null);fs.Init();fs.LoadZippedXML(Path.Combine(folder,"tests","flowsheets",sample));return fs;
 }
 [TestCase("LiquidLiquidExtraction.dwxmz", "Water")]
 [TestCase("BiodieselProduction.dwxmz", "EtP")]
 public void TheFirstGeneratedExtractorTrialMatchesTheFeedCarrierPhases(string sample, string carrier) {
  var fs=Load(sample);var col=fs.SimulationObjects.Values.OfType<Column>().Single();col.UseCompositionEstimates=false;var d=col.GetSolverInputData();
  var names=((PropertyPackage)col.PropertyPackage).RET_VNAMES();var c=Array.IndexOf(names,carrier);
  Assert.That(c,Is.GreaterThanOrEqualTo(0));Assert.That(d.x1trials.Count,Is.GreaterThan(0));
  Assert.That(d.x1trials[0][0][c],Is.GreaterThan(d.x2trials[0][0][c]), $"{carrier}, first generated trial");
 }
 [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
 public void ExtractorConvergesWithoutSavedCompositions(bool allAutomatic, bool poisonDisabledGuesses) {
  var fs=Load();var col=fs.SimulationObjects.Values.OfType<Column>().Single();col.UseCompositionEstimates=false;
  if(allAutomatic){col.UseLiquidFlowEstimates=false;col.UseVaporFlowEstimates=false;col.UseTemperatureEstimates=false;}
  if(poisonDisabledGuesses){
   foreach(var x in col.InitialEstimates.StageTemps)x.Value=600;
   foreach(var x in col.InitialEstimates.LiqMolarFlows)x.Value=10000;
   foreach(var x in col.InitialEstimates.VapMolarFlows)x.Value=0.001;
   foreach(var stage in col.InitialEstimates.LiqCompositions)foreach(var x in stage.Values)x.Value=-1;
   foreach(var stage in col.InitialEstimates.VapCompositions)foreach(var x in stage.Values)x.Value=99;
  }
  Assert.That(col.InternalLoopTolerance,Is.EqualTo(1e-6));Assert.That(col.ExternalLoopTolerance,Is.EqualTo(1e-5));
  Assert.That(fs.SolveFlowsheet2(),Is.Empty);Assert.That(col.Calculated,Is.True);
  var pp=(PropertyPackage)col.PropertyPackage;
  for(int i=0;i<col.NumberOfStages;i++){
   var x=(double[])col.xf[i];var y=(double[])col.yf[i];var k=pp.DW_CalcKvalue(x,y,col.Tf[i],col.Stages[i].P,"LL");
   Assert.That(col.Lf[i],Is.GreaterThan(0));Assert.That(col.Vf[i],Is.GreaterThan(0));
   Assert.That(x.All(z=>double.IsFinite(z)&&z>=0),Is.True);Assert.That(y.All(z=>double.IsFinite(z)&&z>=0),Is.True);
   Assert.That(x.Sum(),Is.EqualTo(1).Within(1e-10));Assert.That(y.Sum(),Is.EqualTo(1).Within(1e-10));
   Assert.That(x.Zip(y,(a,b)=>Math.Abs(a-b)).Max(),Is.GreaterThan(1e-3));
   for(int j=0;j<x.Length;j++)Assert.That(Math.Abs(k[j]*x[j]-y[j]),Is.LessThan(1e-6));
  }
 }
}
}
