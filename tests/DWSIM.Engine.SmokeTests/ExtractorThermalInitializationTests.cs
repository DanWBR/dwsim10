using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DWSIM.GlobalSettings;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;
using NUnit.Framework;
namespace DWSIM.Engine.SmokeTests {
[TestFixture]
public class ExtractorThermalInitializationTests {
 [OneTimeSetUp] public void Setup() {Settings.AutomationMode=true;Settings.InspectorEnabled=false;Settings.CultureInfo="en";DWSIM.FlowsheetBase.FlowsheetBase.AddPropPacks();}
 private static DWSIM.DynamicRunner.Flowsheet Load(string sample) {
  var folder=TestContext.CurrentContext.TestDirectory;
  while(folder!=null && !Directory.Exists(Path.Combine(folder,"tests","flowsheets")))folder=Path.GetDirectoryName(folder);
  Assert.That(folder,Is.Not.Null);var fs=new DWSIM.DynamicRunner.Flowsheet(null,null);fs.Init();fs.LoadZippedXML(Path.Combine(folder,"tests","flowsheets",sample+".dwxmz"));return fs;
 }
 [TestCase("BiodieselProduction",false,false)] [TestCase("BiodieselProduction",true,false, TestName="BiodieselAutomaticNonnegative")] [TestCase("BiodieselProduction",true,true)]
 [TestCase("LiquidLiquidExtraction",false,false)] [TestCase("LiquidLiquidExtraction",true,false)] [TestCase("LiquidLiquidExtraction",true,true)]
 public void ExtractorsConvergeWithoutSavedTemperatures(string sample,bool automatic,bool poison) {
  var fs=Load(sample);var col=fs.SimulationObjects.Values.OfType<Column>().Single();col.UseTemperatureEstimates=false;
  if(automatic){col.UseLiquidFlowEstimates=false;col.UseVaporFlowEstimates=false;col.UseCompositionEstimates=false;}
  if(poison){foreach(var t in col.InitialEstimates.StageTemps)t.Value=600;foreach(var f in col.InitialEstimates.LiqMolarFlows)f.Value=1e4;foreach(var f in col.InitialEstimates.VapMolarFlows)f.Value=0.001;foreach(var x in col.InitialEstimates.LiqCompositions)foreach(var c in x.Values)c.Value=-1;foreach(var x in col.InitialEstimates.VapCompositions)foreach(var c in x.Values)c.Value=99;}
  Assert.That(col.InternalLoopTolerance,Is.EqualTo(1e-6));Assert.That(col.ExternalLoopTolerance,Is.EqualTo(1e-5));
  Assert.That(fs.SolveFlowsheet2(),Is.Empty);Assert.That(col.Calculated,Is.True);var pp=(PropertyPackage)col.PropertyPackage;
  for(int i=0;i<col.NumberOfStages;i++){
   var x=(double[])col.xf[i];var y=(double[])col.yf[i];var k=pp.DW_CalcKvalue(x,y,col.Tf[i],col.Stages[i].P,"LL");
   Assert.That(col.Lf[i],Is.GreaterThan(0));Assert.That(col.Vf[i],Is.GreaterThan(0));
   Assert.That(x.All(z=>double.IsFinite(z)&&z>=0),Is.True);Assert.That(y.All(z=>double.IsFinite(z)&&z>=0),Is.True);
   Assert.That(x.Sum(),Is.EqualTo(1).Within(1e-10));Assert.That(y.Sum(),Is.EqualTo(1).Within(1e-10));
   Assert.That(x.Zip(y,(a,b)=>Math.Abs(a-b)).Max(),Is.GreaterThan(1e-3));
   for(int j=0;j<x.Length;j++)Assert.That(Math.Abs(k[j]*x[j]-y[j]),Is.LessThan(1e-6));
  }
 }
 [TestCase("tiny")] [TestCase("large")] [TestCase("nan")] [TestCase("positive")] [TestCase("temperature")]
 public void RoundoffProjectionIsBoundedAndDoesNotMutateInput(string mode) {
  var method=typeof(DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps.SolvingMethods.NaphtaliSandholmMethod).GetMethod("ProjectExtractorRoundoff",BindingFlags.Static|BindingFlags.NonPublic);Assert.That(method,Is.Not.Null);
  double[] x={1.0,0.5,0.2,0.1,0.4,1.0,0.5,0.2,0.1,0.4};
  if(mode=="tiny")x[2]=-1e-17;if(mode=="large")x[2]=-1e-5;if(mode=="nan")x[2]=double.NaN;if(mode=="temperature")x[5]=-1e-17;
  var before=(double[])x.Clone();var projected=(double[])method.Invoke(null,new object[]{x,2});Assert.That(x,Is.EqualTo(before));
  if(mode=="tiny"){Assert.That(projected,Is.Not.SameAs(x));Assert.That(projected[2],Is.EqualTo(0));for(int i=0;i<x.Length;i++)if(i!=2)Assert.That(projected[i],Is.EqualTo(x[i]));}
  else Assert.That(projected,Is.SameAs(x));
 }
 [TestCase("BiodieselProduction")] [TestCase("LiquidLiquidExtraction")]
 public void TrialTemperaturesCloseEnergyWithOriginalInputUntouched(string sample) {
  var fs=Load(sample);var col=fs.SimulationObjects.Values.OfType<Column>().Single();col.UseTemperatureEstimates=false;
  var d=col.GetSolverInputData();var original=d.StageTemperatures.ToArray();var pp=(PropertyPackage)col.PropertyPackage;
  var method=typeof(Column).GetMethod("ExtractorTrialTemperatures",BindingFlags.Static|BindingFlags.NonPublic);Assert.That(method,Is.Not.Null);
  var t=(List<double>)method.Invoke(null,new object[]{d,pp});Assert.That(d.StageTemperatures,Is.EqualTo(original));Assert.That(t.All(v=>v>0&&double.IsFinite(v)),Is.True);
  double H(int i,bool second){var x=second?d.VaporCompositions[i]:d.LiquidCompositions[i];return (second?d.VaporFlows[i]:d.LiquidFlows[i])*pp.DW_CalcEnthalpy(x,t[i],d.StagePressures[i],State.Liquid)*pp.AUX_MMM(x)/1000;}
  for(int i=0;i<t.Count;i++){double residual=H(i,false)+H(i,true)-d.FeedFlows[i]*d.FeedEnthalpies[i]/1000-d.StageHeats[i];if(i>0)residual-=H(i-1,false);if(i+1<t.Count)residual-=H(i+1,true);Assert.That(Math.Abs(residual),Is.LessThan(1e-7));}
  d.LiquidFlows[0]=-1;var rollback=(List<double>)method.Invoke(null,new object[]{d,pp});Assert.That(rollback,Is.EqualTo(original));
  var previous=Settings.TaskCancellationTokenSource;Settings.TaskCancellationTokenSource=new CancellationTokenSource();Settings.TaskCancellationTokenSource.Cancel();
  try{var ex=Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,new object[]{d,pp}));Assert.That(ex.InnerException,Is.InstanceOf<OperationCanceledException>());}finally{Settings.TaskCancellationTokenSource=previous;}
 }
}
}
