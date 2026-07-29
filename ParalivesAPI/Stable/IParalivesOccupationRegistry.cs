using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationRegistry
    {
        int RegisteredOccupationCount { get; }

        ParalivesOccupationRegistrationResult RegisterOccupation(ParalivesOccupationDefinition definition);

        ParalivesOccupationRegistrationResult ApplyWhenReady();
    }
}
