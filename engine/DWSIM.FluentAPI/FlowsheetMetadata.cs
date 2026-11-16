using System.Collections.Generic;
using DWSIM.Interfaces.Enums;

namespace DWSIM.Automation.FluentAPI
{
    public sealed partial class Flowsheet
    {
        /// <summary>
        /// Sets the flowsheet metadata of the simulation settings: the process type, a short process
        /// description, the key compounds and the key reactants and products. The welcome screens show
        /// them, and the flowsheet summary and score read them. Lists left null are cleared.
        /// </summary>
        public Flowsheet WithMetadata(ProcessType processType, string description,
                                      IEnumerable<string> keyCompounds = null,
                                      IEnumerable<string> keyReactants = null,
                                      IEnumerable<string> keyProducts = null)
        {
            var md = Inner.FlowsheetOptions.Metadata;
            md.ProcessType = processType;
            md.ProcessDescription = description ?? "";
            Fill(md.KeyCompounds, keyCompounds);
            Fill(md.KeyReactants, keyReactants);
            Fill(md.KeyProducts, keyProducts);
            return this;
        }

        private static void Fill(List<string> target, IEnumerable<string> values)
        {
            target.Clear();
            if (values != null) target.AddRange(values);
        }
    }
}
