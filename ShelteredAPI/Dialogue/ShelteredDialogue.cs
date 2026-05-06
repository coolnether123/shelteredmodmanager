using System.Collections.Generic;
using ShelteredAPI.Dialogue.Runtime;

namespace ShelteredAPI.Dialogue
{
    /// <summary>
    /// Stable mod-facing facade for shared Sheltered dialogue services.
    /// </summary>
    public static class ShelteredDialogue
    {
        public static IShelteredDialogueService Service
        {
            get { return ShelteredDialogueRuntime.Service; }
        }

        public static DialogueRequestResult Queue(DialogueRequest request)
        {
            return Service.Queue(request);
        }

        public static DialogueRequestResult QueueSequence(DialogueSequence sequence)
        {
            return Service.QueueSequence(sequence);
        }

        public static DialogueRequestResult QueueAmbientSpeech(FamilyMember speaker, string text)
        {
            return QueueAmbientSpeech(speaker, text, null, DialoguePriority.Routine);
        }

        public static DialogueRequestResult QueueAmbientSpeech(FamilyMember speaker, string text, string ownerId)
        {
            return QueueAmbientSpeech(speaker, text, ownerId, DialoguePriority.Routine);
        }

        public static DialogueRequestResult QueueAmbientSpeech(FamilyMember speaker, string text, string ownerId, DialoguePriority priority)
        {
            DialogueRequest request = new DialogueRequest();
            request.OwnerId = ownerId;
            request.ContextKey = DialogueChannel.AmbientSurvivorSpeech.Id;
            request.Channel = DialogueChannel.AmbientSurvivorSpeech;
            request.Speaker = ForFamilyMember(speaker);
            request.Text = text;
            request.Priority = priority;
            return Service.Queue(request);
        }

        public static DialogueRequestResult QueueConversation(IList<DialogueTurn> turns, string ownerId)
        {
            return QueueConversation(turns, ownerId, DialoguePriority.Reactive, 1.0f, 2.5f);
        }

        public static DialogueRequestResult QueueConversation(
            IList<DialogueTurn> turns,
            string ownerId,
            DialoguePriority priority,
            float minTurnDelaySeconds,
            float maxTurnDelaySeconds)
        {
            DialogueSequence sequence = new DialogueSequence();
            sequence.OwnerId = ownerId;
            sequence.Channel = DialogueChannel.AmbientSurvivorSpeech;
            sequence.Priority = priority;
            sequence.MinTurnDelaySeconds = minTurnDelaySeconds;
            sequence.MaxTurnDelaySeconds = maxTurnDelaySeconds;
            if (turns != null)
            {
                for (int i = 0; i < turns.Count; i++)
                    sequence.Turns.Add(turns[i]);
            }

            return Service.QueueSequence(sequence);
        }

        public static bool TrySelectLine(DialogueSelectionContext context, IList<DialogueLineOption> options, out string line)
        {
            return Service.LineSelector.TrySelectLine(context, options, out line);
        }

        public static int Clear()
        {
            return Service.Clear();
        }

        public static int Clear(string ownerId)
        {
            return Service.Clear(ownerId);
        }

        public static DialogueSpeakerRef ForFamilyMember(FamilyMember member)
        {
            string speakerId = string.Empty;
            string displayName = string.Empty;
            if (member != null)
            {
                try
                {
                    speakerId = member.GetId().ToString();
                }
                catch
                {
                }

                try
                {
                    displayName = member.firstName;
                }
                catch
                {
                }
            }

            return new DialogueSpeakerRef(speakerId, displayName, member);
        }
    }
}
