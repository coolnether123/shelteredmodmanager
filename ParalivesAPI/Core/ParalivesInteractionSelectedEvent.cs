namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionSelectedEvent
    {
        public int PlayerIndex { get; internal set; }

        public ulong ActorCharacterGuid { get; internal set; }

        public ulong SelectedCharacterGuid
        {
            get { return ActorCharacterGuid; }
        }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong ClickedCharacterGuid
        {
            get { return TargetCharacterGuid; }
        }

        public ulong InteractionGuid
        {
            get { return InteractionSettingGuid; }
        }

        public ulong InteractionSettingGuid { get; internal set; }

        public ulong InteractionGroupGuid { get; internal set; }

        public ulong RootInteractionGroupGuid { get; internal set; }

        public int ItemInstanceId { get; internal set; }

        public ulong LotGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }
    }
}
