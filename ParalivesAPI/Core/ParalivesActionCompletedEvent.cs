namespace ParalivesAPI.Core
{
    public sealed class ParalivesActionCompletedEvent
    {
        public ulong ActorCharacterGuid { get; internal set; }

        public ulong CharacterGuid
        {
            get { return ActorCharacterGuid; }
        }

        public int PlayerIndex { get; internal set; }

        public ulong OwnerCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong OtherCharacterGuid { get; internal set; }

        public ulong InteractionInstanceGuid { get; internal set; }

        public ulong InteractionSettingGuid { get; internal set; }

        public ulong ActionGuid
        {
            get { return ActionSettingGuid; }
        }

        public ulong ActionSettingGuid { get; internal set; }

        public ulong ActionUnitGuid { get; internal set; }

        public int ActionIndex { get; internal set; }

        public int ActionCount { get; internal set; }

        public bool IsFinalActionInInteraction { get; internal set; }

        public float StartedAtParaTimeMinutes { get; internal set; }

        public float CompletedAtParaTimeMinutes { get; internal set; }

        public float ElapsedParaTimeMinutes { get; internal set; }

        public int ItemInstanceId { get; internal set; }

        public int TargetItemInstanceId { get; internal set; }

        public int UsedItemInstanceId { get; internal set; }

        public int CreatedItemInstanceId { get; internal set; }

        public ulong LotGuid { get; internal set; }

        public ulong SocialGroupGuid { get; internal set; }

        public ulong SocialGroupClusterGuid { get; internal set; }

        public ulong ParentInteractionInstanceGuid { get; internal set; }

        public ulong ChildInteractionInstanceGuid { get; internal set; }

        public ulong SkinGuid { get; internal set; }

        public bool IsFromAutonomy { get; internal set; }

        public bool IsCancelling { get; internal set; }

        public bool HasSuccessBeenDetermined { get; internal set; }

        public bool IsSuccess { get; internal set; }
    }
}
