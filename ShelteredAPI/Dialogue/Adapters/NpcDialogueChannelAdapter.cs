namespace ShelteredAPI.Dialogue.Adapters
{
    /// <summary>
    /// Placeholder adapter for future NpcDialoguePanel wrapping.
    /// A full adapter should map NPC and player turns onto NpcDialoguePanel.PushNpcText,
    /// PushPlayerText, and PushPlayerOptions while preserving vanilla panel ownership.
    /// </summary>
    public sealed class NpcDialogueChannelAdapter : IDialogueChannelAdapter
    {
        public string ChannelId
        {
            get { return DialogueChannel.NpcPanel.Id; }
        }

        public bool CanHandle(DialogueRequest request)
        {
            return false;
        }

        public bool TryStart(DialogueRequest request, out string suppressionReason)
        {
            suppressionReason = "NPC dialogue panel adapter is not implemented in this API pass.";
            return false;
        }
    }
}
