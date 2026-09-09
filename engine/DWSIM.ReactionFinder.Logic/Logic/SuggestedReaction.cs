using DWSIM.Interfaces;

namespace DWSIM.Extensions.ReactionFinder.Logic
{
    public enum SuggestionCategory
    {
        Combustion,
        Kinetic,
        Catalytic,
        AcidBase,
        Precipitation,
        Library
    }

    /// <summary>
    /// Wraps a ready-to-add IReaction together with metadata used for UI display
    /// and de-duplication.
    /// </summary>
    public class SuggestedReaction
    {
        public SuggestionCategory Category { get; set; }
        public string DisplayName { get; set; }
        public string Equation { get; set; }
        public IReaction Reaction { get; set; }

        /// <summary>
        /// A stable signature (category-independent) used to avoid showing the
        /// same reaction twice when multiple suggesters would produce it.
        /// </summary>
        public string Signature { get; set; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Equation) ? DisplayName : (DisplayName + "  :  " + Equation);
        }
    }
}
