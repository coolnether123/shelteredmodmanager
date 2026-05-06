using System;
using System.Collections.Generic;
using System.Reflection;
using ShelteredAPI.Dialogue.Runtime;

namespace ShelteredAPI.Dialogue.Selection
{
    /// <summary>
    /// Anti-repeat weighted selector for reusable mod dialogue pools.
    /// </summary>
    public sealed class DefaultDialogueLineSelector : IDialogueLineSelector
    {
        public const float TraitWeightBoost = 1.25f;

        private readonly IDialogueHistoryStore _historyStore;
        private readonly IDialogueRandom _random;

        public DefaultDialogueLineSelector()
            : this(new BoundedDialogueHistoryStore(), new SystemDialogueRandom())
        {
        }

        public DefaultDialogueLineSelector(IDialogueHistoryStore historyStore)
            : this(historyStore, new SystemDialogueRandom())
        {
        }

        internal DefaultDialogueLineSelector(IDialogueHistoryStore historyStore, IDialogueRandom random)
        {
            _historyStore = historyStore ?? new BoundedDialogueHistoryStore();
            _random = random ?? new SystemDialogueRandom();
        }

        public bool TrySelectLine(DialogueSelectionContext context, IList<DialogueLineOption> options, out string line)
        {
            line = string.Empty;
            if (options == null || options.Count == 0)
                return false;

            if (context == null)
                context = new DialogueSelectionContext();

            List<DialogueLineOption> valid = new List<DialogueLineOption>();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != null && !string.IsNullOrEmpty(options[i].Text))
                    valid.Add(options[i]);
            }

            if (valid.Count == 0)
                return false;

            int cooldown = context.RepeatCooldownTicks > 0 ? context.RepeatCooldownTicks : Math.Max(1, valid.Count);
            List<DialogueLineOption> available = new List<DialogueLineOption>();
            for (int i = 0; i < valid.Count; i++)
            {
                int recency = _historyStore.GetTicksSinceLastUse(context, valid[i].Text, context.Tick);
                if (recency == int.MaxValue || recency >= cooldown)
                    available.Add(valid[i]);
            }

            if (available.Count == 0)
                available = valid;

            DialogueLineOption selected = SelectWeighted(available, context.Speaker);
            if (selected == null || string.IsNullOrEmpty(selected.Text))
                return false;

            line = selected.Text;
            _historyStore.Remember(context, line, context.Tick);
            return true;
        }

        private DialogueLineOption SelectWeighted(IList<DialogueLineOption> options, DialogueSpeakerRef speaker)
        {
            if (options == null || options.Count == 0)
                return null;

            if (options.Count == 1)
                return options[0];

            float totalWeight = 0f;
            float[] weights = new float[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                float weight = options[i].Weight <= 0f ? 1.0f : options[i].Weight;
                if (!string.IsNullOrEmpty(options[i].TraitId) && HasTrait(speaker, options[i].TraitId))
                    weight *= TraitWeightBoost;

                weights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return options[0];

            float roll = _random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < options.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return options[i];
            }

            return options[options.Count - 1];
        }

        private static bool HasTrait(DialogueSpeakerRef speaker, string traitId)
        {
            if (speaker == null || speaker.Target == null || string.IsNullOrEmpty(traitId))
                return false;

            try
            {
                object traits = ReadMemberValue(speaker.Target, "traits");
                if (traits == null)
                    return false;

                if (InvokeBool(traits, "HasTrait", traitId))
                    return true;

                if (InvokeBool(traits, "HasWeakness", traitId))
                    return true;
            }
            catch
            {
            }

            return false;
        }

        private static object ReadMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(target, null);

            return null;
        }

        private static bool InvokeBool(object target, string methodName, string argument)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string) },
                null);
            if (method == null)
                return false;

            object value = method.Invoke(target, new object[] { argument });
            return value is bool && (bool)value;
        }
    }
}
