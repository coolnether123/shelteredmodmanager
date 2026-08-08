using System;
using System.Collections.Generic;
using ModAPI.Actors;
using ModAPI.Core;

namespace ShelteredAPI.Actors.Authoring
{
    internal sealed class ActorAuthoringCapabilityRegistry : IActorAuthoringCapabilityRegistry
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, IActorAuthoringCapabilityProvider> _providers =
            new Dictionary<string, IActorAuthoringCapabilityProvider>(StringComparer.OrdinalIgnoreCase);

        public bool RegisterProvider(IActorAuthoringCapabilityProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.ProviderId) || string.IsNullOrEmpty(provider.ProviderModId))
                return false;

            lock (_lock)
            {
                _providers[provider.ProviderId] = provider;
            }

            MMLog.WriteDebug("[ActorAuthoring] Registered actor field provider '" + provider.ProviderId + "' from " + provider.ProviderModId + ".");
            return true;
        }

        public bool UnregisterProvider(string providerId)
        {
            if (string.IsNullOrEmpty(providerId))
                return false;

            lock (_lock)
            {
                return _providers.Remove(providerId);
            }
        }

        public IList<IActorAuthoringCapabilityProvider> GetProviders()
        {
            List<IActorAuthoringCapabilityProvider> providers;
            lock (_lock)
            {
                providers = new List<IActorAuthoringCapabilityProvider>(_providers.Values);
            }

            providers.Sort(CompareProviders);
            return providers;
        }

        public IList<ActorAuthoringFieldDefinition> GetFields(ActorKind actorKind)
        {
            List<ActorAuthoringFieldDefinition> fields = new List<ActorAuthoringFieldDefinition>();
            IList<IActorAuthoringCapabilityProvider> providers = GetProviders();
            for (int i = 0; providers != null && i < providers.Count; i++)
            {
                IActorAuthoringCapabilityProvider provider = providers[i];
                IList<ActorAuthoringFieldDefinition> providerFields = null;
                try { providerFields = provider != null ? provider.GetFields() : null; }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ActorAuthoring] Provider '" + (provider != null ? provider.ProviderId : "<null>") + "' failed to list fields: " + ex.Message);
                }

                for (int f = 0; providerFields != null && f < providerFields.Count; f++)
                {
                    ActorAuthoringFieldDefinition field = providerFields[f];
                    if (IsValid(field) && AppliesTo(field, actorKind))
                        fields.Add(field);
                }
            }

            fields.Sort(CompareFields);
            return fields;
        }

        internal bool HasProviderMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;

            IList<IActorAuthoringCapabilityProvider> providers = GetProviders();
            for (int i = 0; providers != null && i < providers.Count; i++)
            {
                IActorAuthoringCapabilityProvider provider = providers[i];
                if (provider != null && string.Equals(provider.ProviderModId, modId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsValid(ActorAuthoringFieldDefinition field)
        {
            return field != null
                && !string.IsNullOrEmpty(field.Id)
                && !string.IsNullOrEmpty(field.Label)
                && !string.IsNullOrEmpty(field.ComponentId)
                && !string.IsNullOrEmpty(field.RequiredModId);
        }

        private static bool AppliesTo(ActorAuthoringFieldDefinition field, ActorKind actorKind)
        {
            if (field.ApplicableActorKinds == null || field.ApplicableActorKinds.Length == 0)
                return true;

            for (int i = 0; i < field.ApplicableActorKinds.Length; i++)
            {
                if (field.ApplicableActorKinds[i] == actorKind)
                    return true;
            }

            return false;
        }

        private static int CompareProviders(IActorAuthoringCapabilityProvider left, IActorAuthoringCapabilityProvider right)
        {
            int priority = (left != null ? left.Priority : 0).CompareTo(right != null ? right.Priority : 0);
            if (priority != 0)
                return priority;
            return string.Compare(left != null ? left.ProviderId : null, right != null ? right.ProviderId : null, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareFields(ActorAuthoringFieldDefinition left, ActorAuthoringFieldDefinition right)
        {
            int component = string.Compare(left != null ? left.ComponentId : null, right != null ? right.ComponentId : null, StringComparison.OrdinalIgnoreCase);
            if (component != 0)
                return component;
            return string.Compare(left != null ? left.Id : null, right != null ? right.Id : null, StringComparison.OrdinalIgnoreCase);
        }
    }
}
