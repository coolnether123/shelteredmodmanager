using System;
using System.Collections.Generic;
using System.Reflection;

using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Shared;
using ShelteredAPI.Infrastructure;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal sealed class ScheduledJournalRuntimeService : IScenarioEffectHandler
    {
        private static readonly MethodInfo InsertJournalEntryMethod = typeof(JournalManager).GetMethod(
            "InsertJournalEntry",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly HashSet<string> WrittenMinuteKeys = new HashSet<string>(StringComparer.Ordinal);

        private readonly ScenarioActorResolver _actorResolver;

        public ScheduledJournalRuntimeService(ScenarioActorResolver actorResolver)
        {
            _actorResolver = actorResolver;
        }

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.WriteJournalEntry;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null)
            {
                message = "Journal effect was missing.";
                return false;
            }

            JournalManager manager = JournalManager.Instance;
            if (manager == null || InsertJournalEntryMethod == null)
            {
                message = "JournalManager is not ready.";
                return false;
            }

            string writerName = ResolveWriterName(definition, effect);
            string text = ScenarioPropertyBag.GetString(effect.Properties, "text", string.Empty);
            text = ApplySubstitutions(text, writerName);
            if (string.IsNullOrEmpty(text))
            {
                message = "Journal entry text is empty.";
                return false;
            }

            string rendered = !string.IsNullOrEmpty(writerName)
                ? "[b]" + writerName + "[/b]\n" + text
                : text;
            string entryId = ScenarioPropertyBag.GetString(effect.Properties, "entryId", effect.TargetId ?? effect.Id ?? string.Empty);
            if (!ScenarioPropertyBag.GetBool(effect.Properties, "repeatable", false)
                && HasJournalEntry(manager, rendered))
            {
                message = "Duplicate journal write skipped.";
                return true;
            }

            string writeKey = entryId
                + "|"
                + GameTime.Day.ToString()
                + ":"
                + GameTime.Hour.ToString()
                + ":"
                + GameTime.Minute.ToString()
                + "|"
                + rendered;
            lock (WrittenMinuteKeys)
            {
                if (WrittenMinuteKeys.Contains(writeKey))
                {
                    message = "Duplicate journal write skipped.";
                    return true;
                }
                WrittenMinuteKeys.Add(writeKey);
            }

            string seamMessage;
            if (SeamGuard.Run(
                "scenario.journal.insert",
                SeamRecoveryPolicy.RetryOnce,
                delegate { InsertJournalEntryMethod.Invoke(manager, new object[] { rendered, string.Empty, false }); },
                "Journal entry unavailable - scenario still playable.",
                null,
                out seamMessage))
            {
                return true;
            }

            message = seamMessage;
            lock (WrittenMinuteKeys)
                WrittenMinuteKeys.Remove(writeKey);
            return false;
        }

        private static bool HasJournalEntry(JournalManager manager, string rendered)
        {
            if (manager == null || string.IsNullOrEmpty(rendered))
                return false;

            for (int i = 0; i < manager.NumEntries; i++)
            {
                if (string.Equals(manager.GetEntryText(i), rendered, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private string ResolveWriterName(ScenarioDefinition definition, ScenarioEffectDefinition effect)
        {
            FamilyMember writer;
            if (_actorResolver != null
                && effect != null
                && effect.ActorRef != null
                && _actorResolver.TryResolveFamilyMember(definition, effect.ActorRef, out writer)
                && IsPresent(writer))
            {
                return writer.firstName;
            }

            if (effect != null && effect.ActorRef != null && !string.IsNullOrEmpty(effect.ActorRef.DisplayNameFallback))
                return effect.ActorRef.DisplayNameFallback;

            FamilyMember fallback = FindAnyPresentMember();
            return fallback != null ? fallback.firstName : string.Empty;
        }

        private static FamilyMember FindAnyPresentMember()
        {
            List<FamilyMember> members = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (IsPresent(member))
                    return member;
            }
            return null;
        }

        private static bool IsPresent(FamilyMember member)
        {
            return member != null && !member.isAway;
        }

        private static string ApplySubstitutions(string text, string writerName)
        {
            string result = text ?? string.Empty;
            result = result.Replace("{writer}", writerName ?? string.Empty);
            result = result.Replace("{day}", GameTime.Day.ToString());
            return result;
        }
    }

    internal static class ScenarioJournalVanillaPolicyRuntime
    {
        private static JournalVanillaPolicyDefinition _policy;

        public static void SetActiveDefinition(ScenarioDefinition definition)
        {
            _policy = definition != null && definition.Journal != null ? definition.Journal.VanillaPolicy : null;
        }

        public static bool ShouldSuppressFirstEntry()
        {
            return _policy != null && _policy.SuppressFirstEntry;
        }

        public static bool ShouldSuppressCategory(string category)
        {
            if (_policy == null || _policy.SuppressedCategories == null || string.IsNullOrEmpty(category))
                return false;

            for (int i = 0; i < _policy.SuppressedCategories.Count; i++)
            {
                if (string.Equals(_policy.SuppressedCategories[i].ToString(), category, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
