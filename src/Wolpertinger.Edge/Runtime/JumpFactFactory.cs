using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Facts;
using Wolpertinger.Edge.Kernel;

namespace Wolpertinger.Edge.Runtime;

internal static class JumpFactFactory
{
    internal static JumpFact Create(
        ObservationEnvelope observation,
        EvidenceReference evidenceReference,
        KernelApplyResult result)
    {
        var fact = result.JumpFact ?? throw new InvalidDataException("Applied FSDJump did not return JumpFact.");
        if (fact.Cursor != observation.Cursor || result.Cursor != observation.Cursor)
            throw new InvalidDataException("Kernel JumpFact cursor does not match observation cursor.");
        return new JumpFact(
            observation.Cursor, observation.Profile, evidenceReference, observation.EvidenceDigest, result.StateDigest,
            fact.SystemAddress, fact.StarSystem, fact.Position, fact.JumpDistance, fact.FuelUsed, fact.FuelLevel,
            fact.LocationProvenance, fact.LocationFreshness, fact.FuelProvenance, fact.FuelFreshness);
    }
}
