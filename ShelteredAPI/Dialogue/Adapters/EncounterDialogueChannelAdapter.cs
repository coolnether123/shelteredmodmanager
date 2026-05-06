namespace ShelteredAPI.Dialogue.Adapters
{
    /// <summary>
    /// Placeholder adapter for future EncounterDialoguePanel wrapping.
    /// A full adapter should route player, NPC, and option turns through
    /// EncounterDialoguePanel.PushPlayerText, PushNpcText, and PushPlayerOptions
    /// without replacing the vanilla BaseDialogueStage flow.
    /// </summary>
    public sealed class EncounterDialogueChannelAdapter : IDialogueChannelAdapter
    {
        public string ChannelId
        {
            get { return DialogueChannel.EncounterPanel.Id; }
        }

        public bool CanHandle(DialogueRequest request)
        {
            return false;
        }

        public bool TryStart(DialogueRequest request, out string suppressionReason)
        {
            suppressionReason = "Encounter dialogue panel adapter is not implemented in this API pass.";
            return false;
        }
    }
}
