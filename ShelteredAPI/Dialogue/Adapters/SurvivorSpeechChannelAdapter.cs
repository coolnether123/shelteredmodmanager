using UnityEngine;

namespace ShelteredAPI.Dialogue.Adapters
{
    public sealed class SurvivorSpeechChannelAdapter : IDialogueChannelAdapter
    {
        public string ChannelId
        {
            get { return DialogueChannel.AmbientSurvivorSpeech.Id; }
        }

        public bool CanHandle(DialogueRequest request)
        {
            FamilyMember member = ResolveMember(request);
            return IsValidMember(member);
        }

        public bool TryStart(DialogueRequest request, out string suppressionReason)
        {
            suppressionReason = null;
            FamilyMember member = ResolveMember(request);
            if (!IsValidMember(member))
            {
                suppressionReason = "Ambient survivor speech speaker is missing or unavailable.";
                return false;
            }

            try
            {
                member.Say(request != null ? request.Text : null);
                return true;
            }
            catch
            {
                suppressionReason = "Ambient survivor speech could not be shown.";
                return false;
            }
        }

        private static FamilyMember ResolveMember(DialogueRequest request)
        {
            if (request == null || request.Speaker == null)
                return null;

            return request.Speaker.Target as FamilyMember;
        }

        private static bool IsValidMember(FamilyMember member)
        {
            if ((Object)member == (Object)null)
                return false;

            return !member.isDead;
        }
    }
}
