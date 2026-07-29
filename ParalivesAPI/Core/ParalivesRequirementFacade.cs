using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesInteractionRequirementRequest
    {
        public ParalivesInteractionRequirementRequest()
        {
            ItemInstanceId = -1;
            State = global::CanCharacterDoInteractionState.QueuingInteraction;
        }

        public ulong CharacterGuid { get; set; }

        public ulong InteractionGuid { get; set; }

        public ulong OtherCharacterGuid { get; set; }

        public ulong LotGuid { get; set; }

        public ulong OwnerCharacterGuid { get; set; }

        public int ItemInstanceId { get; set; }

        public global::CanCharacterDoInteractionState State { get; set; }
    }

    public sealed class ParalivesRequirementFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        internal ParalivesRequirementFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public bool CharacterHasRequirement(ulong characterGuid, ulong requirementGuid)
        {
            if (requirementGuid == 0UL)
                return true;
            if (characterGuid == 0UL)
                return false;

            try
            {
                return global::CharacterManager.Instance != null
                    && global::CharacterManager.Instance.CharacterHasCharacterRequirement(characterGuid, requirementGuid);
            }
            catch
            {
                return false;
            }
        }

        public bool CharacterHasRequirement(global::AssetCharacter character, ulong requirementGuid)
        {
            if (requirementGuid == 0UL)
                return true;
            if (character == null)
                return false;

            try
            {
                return global::CharacterManager.Instance != null
                    && global::CharacterManager.Instance.CharacterHasCharacterRequirement(character, requirementGuid);
            }
            catch
            {
                return false;
            }
        }

        public bool AnyCharacterHasRequirement(IEnumerable<ulong> characterGuids, ulong requirementGuid)
        {
            if (requirementGuid == 0UL)
                return true;
            if (characterGuids == null)
                return false;

            foreach (ulong characterGuid in characterGuids)
            {
                if (CharacterHasRequirement(characterGuid, requirementGuid))
                    return true;
            }

            return false;
        }

        public bool CanDoInteraction(ulong characterGuid, ulong interactionGuid)
        {
            return CanDoInteraction(new ParalivesInteractionRequirementRequest
            {
                CharacterGuid = characterGuid,
                InteractionGuid = interactionGuid,
                OwnerCharacterGuid = characterGuid
            });
        }

        public bool CanDoInteraction(
            ulong characterGuid,
            ulong interactionGuid,
            ulong otherCharacterGuid,
            ulong lotGuid)
        {
            return CanDoInteraction(new ParalivesInteractionRequirementRequest
            {
                CharacterGuid = characterGuid,
                InteractionGuid = interactionGuid,
                OtherCharacterGuid = otherCharacterGuid,
                LotGuid = lotGuid,
                OwnerCharacterGuid = characterGuid
            });
        }

        public bool CanDoInteraction(ParalivesInteractionRequirementRequest request)
        {
            bool canDo;
            return TryCanDoInteraction(request, out canDo) && canDo;
        }

        public bool TryCanDoInteraction(ParalivesInteractionRequirementRequest request, out bool canDo)
        {
            canDo = false;
            if (request == null || request.CharacterGuid == 0UL || request.InteractionGuid == 0UL)
                return false;

            global::AssetCharacter character;
            if (!_characters.TryGet(request.CharacterGuid, out character))
                return false;

            InteractionUnit interaction;
            if (!TryGetInteraction(request.InteractionGuid, out interaction))
                return false;

            return TryCanDoInteraction(character, interaction, request, out canDo);
        }

        public bool TryCanDoInteraction(
            global::AssetCharacter character,
            InteractionUnit interaction,
            ParalivesInteractionRequirementRequest request,
            out bool canDo)
        {
            canDo = false;
            if (character == null || interaction == null || request == null)
                return false;

            try
            {
                if (global::Settings.Instance == null || global::InteractionManager.Instance == null)
                    return false;

                InteractionRequirementsCheckData checkData = new InteractionRequirementsCheckData
                {
                    CharacterGUID = character.GUID,
                    OtherCharactersGUID = request.OtherCharacterGuid,
                    LotGUID = request.LotGuid,
                    OwnerCharacterGUID = request.OwnerCharacterGuid == 0UL
                        ? character.GUID
                        : request.OwnerCharacterGuid,
                    ItemInstanceID = request.ItemInstanceId
                };

                canDo = global::InteractionManager.Instance.CanCharacterDoInteraction(
                    interaction,
                    checkData,
                    request.State);
                return true;
            }
            catch
            {
                canDo = false;
                return false;
            }
        }

        private static bool TryGetInteraction(ulong interactionGuid, out InteractionUnit interaction)
        {
            interaction = null;
            if (interactionGuid == 0UL || global::Settings.Instance == null)
                return false;

            try
            {
                Interactions interactions = global::Settings.Get<Interactions>();
                if (interactions == null)
                    return false;

                interaction = interactions.GetInteractionByGUID(interactionGuid);
                return interaction != null;
            }
            catch
            {
                interaction = null;
                return false;
            }
        }
    }
}
