using System;
using System.Collections.Generic;
using System.ComponentModel;
using DWSIM.Interfaces;

namespace DWSIM.AI.ConvergenceAssistant.Classes
{
    public class ConvergenceHelperTrainingData : IConvergenceHelperTrainingData
    {
        public ConvergenceHelperRequestType RequestType { get; set; }
        public string ModelName { get; set; }
        public List<IReaction> Reactions { get; set; }
        public string Hash { get; set; }
        public int NumberOfCompounds { get; set; }
        public string[] CompoundNames { get; set; }
        public string Temperature { get; set; }
        public string Temperature2 { get; set; }
        public string Pressure { get; set; }
        public string MassEnthalpy { get; set; }
        public string MassEntropy { get; set; }
        public string VaporMolarFraction { get; set; }
        public string[] MixtureMolarFlows { get; set; }
        public string[] MixtureMolarFlows2 { get; set; }
        public string[] VaporMolarFlows { get; set; }
        public string[] Liquid1MolarFlows { get; set; }
        public string[] Liquid2MolarFlows { get; set; }
        public string[] SolidMolarFlows { get; set; }
        public string[] KValuesVL1 { get; set; }
        public string[] KValuesVL2 { get; set; }
        public string[] ReactionExtents { get; set; }

        public string GetBase64StringHash()
        {
            var options = new Newtonsoft.Json.JsonSerializerSettings();
            var jsondata = Newtonsoft.Json.JsonConvert.SerializeObject(this);
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(jsondata);
            return Convert.ToBase64String(plainTextBytes);
        }
    }

    public class ConvergenceHelperRequest : IConvergenceHelperRequest
    {
        public ConvergenceHelperRequestType RequestType { get; set; }
        public string ModelName { get; set; } = "";
        public int NumberOfCompounds { get; set; }
        public string[] CompoundNames { get; set; }
        public double? Temperature { get; set; }
        public double? Pressure { get; set; }
        public double? MassEnthalpy { get; set; }
        public double? MassEntropy { get; set; }
        public double? VaporMolarFraction { get; set; }
        public double[] MixtureMolarFlows { get; set; }
    }

    public class ConvergenceHelperResponse : IConvergenceHelperResponse
    {
        public ConvergenceHelperRequestType RequestType { get; set; } = ConvergenceHelperRequestType.PVFlash;
        public IConvergenceHelperMetaData MetaData { get; set; } = new ConvergenceHelperMetaData();
        public string ModelName { get; set; } = "";
        public bool IsValid { get; set; }
        public string Reason { get; set; } = "";
        public Exception InnerException { get; set; }
        public double? Temperature { get; set; }
        public double? Pressure { get; set; }
        public double? MassEnthalpy { get; set; }
        public double? MassEntropy { get; set; }
        public double? VaporMolarFraction { get; set; }
        public double[] MixtureMolarFlows { get; set; }
        public double[] VaporMolarFlows { get; set; }
        public double[] Liquid1MolarFlows { get; set; }
        public double[] Liquid2MolarFlows { get; set; }
        public double[] SolidMolarFlows { get; set; }
        public double[] KValuesVL1 { get; set; }
        public double[] KValuesVL2 { get; set; }
        public double[] MixtureMolarFlows2 { get; set; }
        public double? Temperature2 { get; set; }
        public double[] ReactionExtents { get; set; }
    }

    public class ConvergenceHelperMetaData : IConvergenceHelperMetaData
    {
        [ReadOnly(true)]
        [Category("General")]
        public ConvergenceHelperRequestType RequestType { get; set; } = ConvergenceHelperRequestType.PVFlash;
        [ReadOnly(true)]
        [Category("General")]
        public string ModelName { get; set; } = "";
        [ReadOnly(true)]
        [Category("Data")]
        public string PropertyPackageName { get; set; } = "";
        [ReadOnly(true)]
        [Category("General")]
        public DateTime CreatedOn { get; set; } = DateTime.MinValue;
        [ReadOnly(true)]
        [Category("General")]
        public DateTime LastUpdatedOn { get; set; } = DateTime.MinValue;
        [ReadOnly(true)]
        [Category("Data")]
        public int NumberOfCompounds { get; set; }
        [ReadOnly(true)]
        [Category("Data")]
        public int NumberOfSamples { get; set; }
        [ReadOnly(true)]
        [Category("Data")]
        public int NumberOfReactions { get; set; }
        [ReadOnly(true)]
        [Category("Data")]
        public string[] CompoundNames { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public float[] TemperatureRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public float[] PressureRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public float[] MassEnthalpyRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public float[] MassEntropyRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public float[] VaporMolarFractionRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Details")]
        public List<float[]> MolarCompositionRange { get; set; }
        [ReadOnly(true)]
        [Category("Model Accuracy")]
        public float TrainingDataMSE { get; set; }
        [ReadOnly(true)]
        [Category("Model Accuracy")]
        public float TestingDataMSE { get; set; }
    }

    public class PhaseEnvelopeRequest : IPhaseEnvelopeRequest
    {
        public string[] CompoundNames { get; set; }
        public double[] MolarComposition { get; set; }
        public string ModelName { get; set; }
        public List<Tuple<string, string, double[]>> ModelParameters { get; set; }
    }

    public class PhaseEnvelopeResult : IPhaseEnvelopeResult
    {
        public double[] BubbleTemperatures { get; set; }
        public double[] BubblePressures { get; set; }
        public double[] DewTemperatures { get; set; }
        public double[] DewPressures { get; set; }
        public List<double[]> CriticalPoints { get; set; }
    }

}
