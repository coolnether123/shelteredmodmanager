using System;
using System.Collections.Generic;

namespace ShelteredAPI.Dialogue
{
    /// <summary>
    /// Stable identifier for a dialogue delivery surface.
    /// </summary>
    public sealed class DialogueChannel
    {
        public static readonly DialogueChannel AmbientSurvivorSpeech =
            new DialogueChannel("shelteredapi.dialogue.ambient_survivor_speech", "Ambient survivor speech");

        public static readonly DialogueChannel EncounterPanel =
            new DialogueChannel("shelteredapi.dialogue.encounter_panel", "Encounter dialogue panel");

        public static readonly DialogueChannel NpcPanel =
            new DialogueChannel("shelteredapi.dialogue.npc_panel", "NPC dialogue panel");

        public DialogueChannel(string id)
            : this(id, id)
        {
        }

        public DialogueChannel(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return Id;
        }
    }

    public enum DialoguePriority
    {
        Routine = 0,
        Reactive = 1,
        Immediate = 2
    }

    public enum DialogueRequestResultStatus
    {
        Queued = 0,
        RejectedInvalid = 1,
        RejectedBudget = 2,
        RejectedNoChannel = 3,
        Started = 4,
        Completed = 5,
        SkippedValidation = 6,
        Suppressed = 7,
        Cleared = 8
    }

    public sealed class DialogueRequestResult
    {
        public DialogueRequestResult(DialogueRequestResultStatus status, string requestId, string ownerId, string message)
        {
            Status = status;
            RequestId = requestId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public DialogueRequestResultStatus Status { get; private set; }
        public string RequestId { get; private set; }
        public string OwnerId { get; private set; }
        public string Message { get; private set; }

        public bool Accepted
        {
            get { return Status == DialogueRequestResultStatus.Queued; }
        }

        public static DialogueRequestResult Queued(DialogueRequest request)
        {
            return FromRequest(DialogueRequestResultStatus.Queued, request, "Queued.");
        }

        public static DialogueRequestResult FromRequest(DialogueRequestResultStatus status, DialogueRequest request, string message)
        {
            return new DialogueRequestResult(
                status,
                request != null ? request.Id : null,
                request != null ? request.OwnerId : null,
                message);
        }
    }

    /// <summary>
    /// Channel-neutral reference to the speaker. Adapters own interpretation of Target.
    /// </summary>
    public sealed class DialogueSpeakerRef
    {
        public DialogueSpeakerRef()
        {
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public DialogueSpeakerRef(string speakerId, object target)
            : this(speakerId, null, target)
        {
        }

        public DialogueSpeakerRef(string speakerId, string displayName, object target)
            : this()
        {
            SpeakerId = speakerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Target = target;
        }

        public string SpeakerId { get; set; }
        public string DisplayName { get; set; }
        public object Target { get; set; }
        public IDictionary<string, object> Metadata { get; private set; }

        internal DialogueSpeakerRef Clone()
        {
            DialogueSpeakerRef copy = new DialogueSpeakerRef(SpeakerId, DisplayName, Target);
            CopyMetadata(Metadata, copy.Metadata);
            return copy;
        }

        internal static string ResolveSpeakerKey(DialogueSpeakerRef speaker)
        {
            if (speaker == null)
                return "speaker:none";

            if (!string.IsNullOrEmpty(speaker.SpeakerId))
                return speaker.SpeakerId;

            if (speaker.Target != null)
                return speaker.Target.GetHashCode().ToString();

            return "speaker:none";
        }

        internal static void CopyMetadata(IDictionary<string, object> source, IDictionary<string, object> target)
        {
            if (source == null || target == null)
                return;

            foreach (KeyValuePair<string, object> pair in source)
                target[pair.Key] = pair.Value;
        }
    }

    public sealed class DialogueRequest
    {
        public DialogueRequest()
        {
            Channel = DialogueChannel.AmbientSurvivorSpeech;
            Priority = DialoguePriority.Routine;
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; set; }
        public string OwnerId { get; set; }
        public string ContextKey { get; set; }
        public DialogueChannel Channel { get; set; }
        public DialogueSpeakerRef Speaker { get; set; }
        public string Text { get; set; }
        public DialoguePriority Priority { get; set; }
        public Func<bool> Validation { get; set; }

        public bool HasCustomDelay { get; set; }
        public float MinDelaySeconds { get; set; }
        public float MaxDelaySeconds { get; set; }

        public bool UseDailyBudget { get; set; }
        public int MaxPerDay { get; set; }
        public int MaxPerSpeakerPerDay { get; set; }
        public string BudgetKey { get; set; }

        public IDictionary<string, object> Metadata { get; private set; }

        internal DialogueRequest Clone()
        {
            DialogueRequest copy = new DialogueRequest();
            copy.Id = Id;
            copy.OwnerId = OwnerId;
            copy.ContextKey = ContextKey;
            copy.Channel = Channel;
            copy.Speaker = Speaker != null ? Speaker.Clone() : null;
            copy.Text = Text;
            copy.Priority = Priority;
            copy.Validation = Validation;
            copy.HasCustomDelay = HasCustomDelay;
            copy.MinDelaySeconds = MinDelaySeconds;
            copy.MaxDelaySeconds = MaxDelaySeconds;
            copy.UseDailyBudget = UseDailyBudget;
            copy.MaxPerDay = MaxPerDay;
            copy.MaxPerSpeakerPerDay = MaxPerSpeakerPerDay;
            copy.BudgetKey = BudgetKey;
            DialogueSpeakerRef.CopyMetadata(Metadata, copy.Metadata);
            return copy;
        }
    }

    public sealed class DialogueTurn
    {
        public DialogueTurn()
        {
        }

        public DialogueTurn(DialogueSpeakerRef speaker, string text)
            : this(speaker, text, null)
        {
        }

        public DialogueTurn(DialogueSpeakerRef speaker, string text, Func<bool> validation)
        {
            Speaker = speaker;
            Text = text;
            Validation = validation;
        }

        public DialogueSpeakerRef Speaker { get; set; }
        public string Text { get; set; }
        public DialogueChannel Channel { get; set; }
        public Func<bool> Validation { get; set; }
        public IDictionary<string, object> Metadata
        {
            get
            {
                if (_metadata == null)
                    _metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                return _metadata;
            }
        }

        private IDictionary<string, object> _metadata;
    }

    public sealed class DialogueSequence
    {
        public DialogueSequence()
        {
            Channel = DialogueChannel.AmbientSurvivorSpeech;
            Priority = DialoguePriority.Reactive;
            MinTurnDelaySeconds = 1.0f;
            MaxTurnDelaySeconds = 2.5f;
            Turns = new List<DialogueTurn>();
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public string Id { get; set; }
        public string OwnerId { get; set; }
        public string ContextKey { get; set; }
        public DialogueChannel Channel { get; set; }
        public DialoguePriority Priority { get; set; }
        public IList<DialogueTurn> Turns { get; private set; }
        public Func<bool> Validation { get; set; }
        public float MinTurnDelaySeconds { get; set; }
        public float MaxTurnDelaySeconds { get; set; }

        public bool UseDailyBudget { get; set; }
        public int MaxPerDay { get; set; }
        public int MaxPerSpeakerPerDay { get; set; }
        public string BudgetKey { get; set; }

        public IDictionary<string, object> Metadata { get; private set; }
    }

    public sealed class DialogueSelectionContext
    {
        public DialogueSelectionContext()
        {
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public string OwnerId { get; set; }
        public string ContextKey { get; set; }
        public DialogueSpeakerRef Speaker { get; set; }
        public int CurrentDay { get; set; }
        public int Tick { get; set; }
        public int RepeatCooldownTicks { get; set; }
        public IDictionary<string, object> Metadata { get; private set; }
    }

    public sealed class DialogueLineOption
    {
        public DialogueLineOption()
        {
            Weight = 1.0f;
        }

        public DialogueLineOption(string text)
            : this(text, 1.0f, null)
        {
        }

        public DialogueLineOption(string text, float weight)
            : this(text, weight, null)
        {
        }

        public DialogueLineOption(string text, float weight, string traitId)
        {
            Text = text ?? string.Empty;
            Weight = weight <= 0f ? 1.0f : weight;
            TraitId = traitId ?? string.Empty;
        }

        public string Text { get; set; }
        public float Weight { get; set; }
        public string TraitId { get; set; }
    }
}
