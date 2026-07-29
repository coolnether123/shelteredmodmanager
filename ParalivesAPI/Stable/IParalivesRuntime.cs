using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesRuntime
    {
        string GameId { get; }

        string DisplayName { get; }

        string ApiVersion { get; }

        string AdapterVersion { get; }

        ParalivesApiVersion Version { get; }

        ParalivesCapabilityRegistry Capabilities { get; }

        IParalivesCompatibility Compatibility { get; }

        string[] CapabilityStrings { get; }

        bool HasCapability(string capability);

        IParalivesCharacters Characters { get; }

        IParalivesInteractions Interactions { get; }

        IParalivesActions Actions { get; }

        IParalivesOccupations Occupations { get; }

        IParalivesUi Ui { get; }

        IParalivesSaveStorage SaveStorage { get; }
    }
}
