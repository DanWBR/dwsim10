using DWSIM.Interfaces;

namespace DWSIM.Automation.FluentAPI.Builders
{
    /// <summary>Fluent builder for a reaction set. Add reactions to it via <see cref="Add"/>.</summary>
    public sealed class ReactionSetBuilder
    {
        /// <summary>The underlying DWSIM object / owning flowsheet - escape hatch for advanced use.</summary>
        public Flowsheet Flowsheet { get; }
        /// <summary>The reaction-set ID - used by reactor builders' <c>WithReactionSet(string)</c>.</summary>
        public string Id { get; }

        internal ReactionSetBuilder(Flowsheet flowsheet, string id)
        {
            Flowsheet = flowsheet;
            Id = id;
        }

        /// <summary>Adds an existing reaction to this set.</summary>
        public ReactionSetBuilder Add(IReaction reaction, int rank = 0, bool enabled = true)
        {
            Flowsheet.Inner.AddReactionToSet(reaction.ID, Id, enabled, rank);
            return this;
        }
    }
}
