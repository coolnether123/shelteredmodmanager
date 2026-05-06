using System;
using System.Collections.Generic;
using ShelteredAPI.Dialogue.Runtime;
using ShelteredAPI.Dialogue.Selection;

namespace ShelteredAPI.Dialogue.Diagnostics
{
    /// <summary>
    /// Developer-only verification harness for the shared dialogue queue.
    /// It is not wired into startup; call Run() from a debug mod or immediate window.
    /// </summary>
    internal static class DialogueFrameworkVerification
    {
        public static DialogueVerificationResult Run()
        {
            DialogueVerificationResult result = new DialogueVerificationResult();
            VerifySingleRequestQueueing(result);
            VerifySequenceOrdering(result);
            VerifyValidationSkipping(result);
            VerifyAntiRepeatSelection(result);
            VerifyOwnerScopedClear(result);
            VerifyNullSpeakerSuppression(result);
            return result;
        }

        private static void VerifySingleRequestQueueing(DialogueVerificationResult result)
        {
            FakeClock clock = new FakeClock();
            RecordingAdapter adapter = new RecordingAdapter(DialogueChannel.AmbientSurvivorSpeech.Id);
            ShelteredDialogueService service = CreateService(clock, adapter);

            DialogueRequest request = BuildRequest("mod.a", "single");
            DialogueRequestResult queued = service.Queue(request);
            Assert(queued.Accepted, "Single dialogue request was not queued.", result);
            service.Update();
            Assert(adapter.Started.Count == 1 && adapter.Started[0] == "single", "Single dialogue request did not start.", result);
        }

        private static void VerifySequenceOrdering(DialogueVerificationResult result)
        {
            FakeClock clock = new FakeClock();
            RecordingAdapter adapter = new RecordingAdapter(DialogueChannel.AmbientSurvivorSpeech.Id);
            ShelteredDialogueService service = CreateService(clock, adapter);

            DialogueSequence sequence = new DialogueSequence();
            sequence.OwnerId = "mod.sequence";
            sequence.MinTurnDelaySeconds = 1f;
            sequence.MaxTurnDelaySeconds = 1f;
            sequence.Turns.Add(new DialogueTurn(new DialogueSpeakerRef("one", null), "first"));
            sequence.Turns.Add(new DialogueTurn(new DialogueSpeakerRef("two", null), "second"));
            sequence.Turns.Add(new DialogueTurn(new DialogueSpeakerRef("three", null), "third"));

            service.QueueSequence(sequence);
            service.Update();
            clock.Advance(1.1f);
            service.Update();
            clock.Advance(1.1f);
            service.Update();

            Assert(
                adapter.Started.Count == 3 &&
                adapter.Started[0] == "first" &&
                adapter.Started[1] == "second" &&
                adapter.Started[2] == "third",
                "Dialogue sequence turns did not start in queued order.",
                result);
        }

        private static void VerifyValidationSkipping(DialogueVerificationResult result)
        {
            FakeClock clock = new FakeClock();
            RecordingAdapter adapter = new RecordingAdapter(DialogueChannel.AmbientSurvivorSpeech.Id);
            ShelteredDialogueService service = CreateService(clock, adapter);
            bool skipped = false;
            service.DialogueSkipped += delegate(DialogueRequest skippedRequest, DialogueRequestResult skip)
            {
                if (skip.Status == DialogueRequestResultStatus.SkippedValidation)
                    skipped = true;
            };

            DialogueRequest request = BuildRequest("mod.validation", "skip me");
            request.Validation = delegate { return false; };
            service.Queue(request);
            service.Update();

            Assert(skipped, "Invalid dialogue request was not reported as skipped.", result);
            Assert(adapter.Started.Count == 0, "Invalid dialogue request was delivered.", result);
        }

        private static void VerifyAntiRepeatSelection(DialogueVerificationResult result)
        {
            FakeRandom random = new FakeRandom();
            BoundedDialogueHistoryStore history = new BoundedDialogueHistoryStore();
            DefaultDialogueLineSelector selector = new DefaultDialogueLineSelector(history, random);
            DialogueSelectionContext context = new DialogueSelectionContext();
            context.OwnerId = "mod.selection";
            context.ContextKey = "greeting";
            context.Speaker = new DialogueSpeakerRef("speaker", null);
            context.RepeatCooldownTicks = 3;

            List<DialogueLineOption> options = new List<DialogueLineOption>();
            options.Add(new DialogueLineOption("A"));
            options.Add(new DialogueLineOption("B"));
            options.Add(new DialogueLineOption("C"));

            string first;
            string second;
            selector.TrySelectLine(context, options, out first);
            context.Tick++;
            selector.TrySelectLine(context, options, out second);

            Assert(!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second) && first != second, "Anti-repeat selector repeated a recent line.", result);
        }

        private static void VerifyOwnerScopedClear(DialogueVerificationResult result)
        {
            FakeClock clock = new FakeClock();
            RecordingAdapter adapter = new RecordingAdapter(DialogueChannel.AmbientSurvivorSpeech.Id);
            ShelteredDialogueService service = CreateService(clock, adapter);

            service.Queue(BuildRequest("mod.keep", "keep"));
            service.Queue(BuildRequest("mod.clear", "clear"));
            int removed = service.Clear("mod.clear");
            service.Update();

            Assert(removed == 1, "Owner-scoped clear removed the wrong number of queued requests.", result);
            Assert(adapter.Started.Count == 1 && adapter.Started[0] == "keep", "Owner-scoped clear removed or delivered the wrong request.", result);
        }

        private static void VerifyNullSpeakerSuppression(DialogueVerificationResult result)
        {
            FakeClock clock = new FakeClock();
            RecordingAdapter adapter = new RecordingAdapter(DialogueChannel.AmbientSurvivorSpeech.Id);
            adapter.RequireSpeaker = true;
            ShelteredDialogueService service = CreateService(clock, adapter);
            bool suppressed = false;
            service.DialogueSkipped += delegate(DialogueRequest skippedRequest, DialogueRequestResult skip)
            {
                if (skip.Status == DialogueRequestResultStatus.Suppressed)
                    suppressed = true;
            };

            DialogueRequest request = BuildRequest("mod.suppress", "missing speaker");
            request.Speaker = null;
            service.Queue(request);
            service.Update();

            Assert(suppressed, "Null speaker request was not suppressed.", result);
            Assert(adapter.Started.Count == 0, "Null speaker request was delivered.", result);
        }

        private static ShelteredDialogueService CreateService(FakeClock clock, RecordingAdapter adapter)
        {
            BoundedDialogueHistoryStore history = new BoundedDialogueHistoryStore();
            ShelteredDialogueService service = new ShelteredDialogueService(
                clock,
                new FakeRandom(),
                history,
                new DefaultDialogueLineSelector(history, new FakeRandom()));
            service.RegisterChannel(DialogueChannel.AmbientSurvivorSpeech, adapter);
            return service;
        }

        private static DialogueRequest BuildRequest(string ownerId, string text)
        {
            DialogueRequest request = new DialogueRequest();
            request.OwnerId = ownerId;
            request.ContextKey = "verification";
            request.Channel = DialogueChannel.AmbientSurvivorSpeech;
            request.Speaker = new DialogueSpeakerRef("speaker", null);
            request.Text = text;
            request.Priority = DialoguePriority.Immediate;
            return request;
        }

        private static void Assert(bool condition, string message, DialogueVerificationResult result)
        {
            if (!condition)
                result.AddFailure(message);
        }

        private sealed class FakeClock : IDialogueClock
        {
            private float _time;

            public float TimeSeconds
            {
                get { return _time; }
            }

            public int CurrentDay
            {
                get { return 0; }
            }

            public void Advance(float seconds)
            {
                _time += seconds;
            }
        }

        private sealed class FakeRandom : IDialogueRandom
        {
            public float Range(float minInclusive, float maxInclusive)
            {
                return minInclusive;
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }

        private sealed class RecordingAdapter : IDialogueChannelAdapter
        {
            public readonly List<string> Started = new List<string>();
            public bool RequireSpeaker;
            private readonly string _channelId;

            public RecordingAdapter(string channelId)
            {
                _channelId = channelId;
            }

            public string ChannelId
            {
                get { return _channelId; }
            }

            public bool CanHandle(DialogueRequest request)
            {
                if (!RequireSpeaker)
                    return request != null;

                return request != null && request.Speaker != null;
            }

            public bool TryStart(DialogueRequest request, out string suppressionReason)
            {
                suppressionReason = null;
                if (!CanHandle(request))
                {
                    suppressionReason = "Speaker is unavailable.";
                    return false;
                }

                Started.Add(request.Text);
                return true;
            }
        }
    }

    internal sealed class DialogueVerificationResult
    {
        private readonly List<string> _failures = new List<string>();

        public bool Passed
        {
            get { return _failures.Count == 0; }
        }

        public string[] Failures
        {
            get { return _failures.ToArray(); }
        }

        public void AddFailure(string message)
        {
            _failures.Add(message ?? "Unknown dialogue verification failure.");
        }
    }
}
