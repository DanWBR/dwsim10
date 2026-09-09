using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using DWSIM.Interfaces;
using DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    /// <summary>
    /// Loads the built-in reactions library (swreactions.dwrxm) embedded in
    /// DWSIM.Thermodynamics and filters to reactions where every participating
    /// compound exists in the current flowsheet.
    /// </summary>
    public static class LibrarySuggester
    {
        private const string ResourceName = "DWSIM.Thermodynamics.swreactions.dwrxm";

        public static IEnumerable<SuggestedReaction> Suggest(CompoundIndex idx)
        {
            List<Reaction> library;
            try { library = LoadLibrary(); }
            catch { yield break; }

            foreach (var rxn in library)
            {
                // Only keep reactions whose components are all present in the flowsheet
                // (match by compound Name, which is how IReaction.Components is keyed).
                var comps = rxn.Components.Keys.ToList();
                if (comps.Count == 0) continue;
                if (comps.Any(name => idx.FindByName(name) == null)) continue;

                // Refresh the equation string and assign a new ID to avoid collisions
                // with any existing reaction in the flowsheet.
                rxn.ID = Guid.NewGuid().ToString();
                rxn.Equation = ReactionFactory.BuildEquationString(rxn, idx.FormulaOf);

                var stoich = rxn.Components.ToDictionary(kv => kv.Key, kv => kv.Value.StoichCoeff);
                yield return new SuggestedReaction
                {
                    Category = SuggestionCategory.Library,
                    DisplayName = rxn.Name,
                    Equation = rxn.Equation,
                    Reaction = rxn,
                    Signature = "lib:" + ReactionFactory.Signature(stoich)
                };
            }
        }

        private static List<Reaction> LoadLibrary()
        {
            // The resource is embedded in the DWSIM.Thermodynamics assembly. Resolving
            // through a known type from that assembly avoids hard-coding a path.
            var asm = typeof(Reaction).Assembly;
            using (var stream = asm.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Embedded resource not found: " + ResourceName);

                var doc = XDocument.Load(stream);
                var result = new List<Reaction>();
                foreach (var xel in doc.Root.Elements("Reaction"))
                {
                    var r = new Reaction();
                    // The XML stores typed, named attributes as child elements of <Reaction>.
                    // Reaction implements ICustomXMLSerialization, which round-trips through
                    // its LoadData method. Feed it the element's sub-elements.
                    var sub = xel.Elements().ToList();
                    try { r.LoadData(sub); }
                    catch { continue; }
                    if (!string.IsNullOrEmpty(r.Name)) result.Add(r);
                }
                return result;
            }
        }
    }
}
