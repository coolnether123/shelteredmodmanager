using System;
using System.Collections.Generic;
using System.Text;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Interactions;

namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimeObjectPanelRegistry
    {
        private const string InteractionKeyPrefix = "shelteredapi.runtimeui.";
        private const string InteractionLocalizationPrefix = "Object.Interaction.";

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ObjectPanelRegistration> Registrations =
            new Dictionary<string, ObjectPanelRegistration>(StringComparer.OrdinalIgnoreCase);

        public static IDisposable Register(ObjectPanelRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException("registration");
            if (registration.Open == null)
                throw new ArgumentException("Object panel registration requires an Open callback.", "registration");
            if (string.IsNullOrEmpty(registration.InteractionText))
                throw new ArgumentException("Object panel registration requires InteractionText.", "registration");

            ObjectManager.ObjectType objectType = ResolveObjectType(registration);
            if (objectType == ObjectManager.ObjectType.Undefined)
                throw new ArgumentException("Object panel registration requires ObjectType or an ObjectId matching ObjectManager.ObjectType.", "registration");

            string registrationId = BuildRegistrationId(registration, objectType);
            lock (Sync)
                Registrations[registrationId] = registration;

            RegisterInteractionText(registrationId, registration.InteractionText);

            InteractionRegistry.For(objectType)
                .Add(registrationId, typeof(RuntimeObjectPanelInteraction))
                .WithPriority(registration.Priority > 0 ? registration.Priority : 1)
                .OnInjected(delegate(Obj_Base obj, Int_Base interaction)
                {
                    RuntimeObjectPanelInteraction runtime = interaction as RuntimeObjectPanelInteraction;
                    if (runtime != null)
                        runtime.Configure(registrationId);
                })
                .Register();

            return new RegistrationLease(registrationId);
        }

        public static bool CanOpen(string registrationId, Obj_Base target, FamilyMember member)
        {
            ObjectPanelRegistration registration;
            if (!TryGet(registrationId, out registration))
                return false;

            if (registration.CanOpen == null)
                return true;

            try
            {
                return registration.CanOpen(new ObjectPanelContext(registration.ObjectId, target, member));
            }
            catch (Exception ex)
            {
                MMLog.Write("ERROR in RuntimeObjectPanelRegistry.CanOpen: " + ex);
                return false;
            }
        }

        public static bool TryOpen(string registrationId, Obj_Base target, FamilyMember member)
        {
            ObjectPanelRegistration registration;
            if (!TryGet(registrationId, out registration))
                return false;

            if (!CanOpen(registrationId, target, member))
                return false;

            try
            {
                registration.Open(new ObjectPanelContext(registration.ObjectId, target, member));
                return true;
            }
            catch (Exception ex)
            {
                MMLog.Write("ERROR in RuntimeObjectPanelRegistry.Open: " + ex);
                return false;
            }
        }

        private static bool TryGet(string registrationId, out ObjectPanelRegistration registration)
        {
            registration = null;
            if (string.IsNullOrEmpty(registrationId))
                return false;

            lock (Sync)
                return Registrations.TryGetValue(registrationId, out registration);
        }

        private static void Unregister(string registrationId)
        {
            if (string.IsNullOrEmpty(registrationId))
                return;

            lock (Sync)
                Registrations.Remove(registrationId);
        }

        private static string BuildRegistrationId(ObjectPanelRegistration registration, ObjectManager.ObjectType objectType)
        {
            string source = registration.InteractionId;
            if (string.IsNullOrEmpty(source))
            {
                string owner = !string.IsNullOrEmpty(registration.ObjectId)
                    ? registration.ObjectId
                    : objectType.ToString();
                source = owner + "." + registration.InteractionText;
            }

            return InteractionKeyPrefix + NormalizeInteractionId(source);
        }

        private static string NormalizeInteractionId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "panel";

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-' || c == '_';
                if (allowed)
                {
                    builder.Append(c);
                    previousWasSeparator = c == '.' || c == '-' || c == '_';
                    continue;
                }

                if (!previousWasSeparator)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }
            }

            string normalized = builder.Length > 0 ? builder.ToString().Trim('.', '-', '_') : string.Empty;
            return !string.IsNullOrEmpty(normalized) ? normalized : "panel";
        }

        private static void RegisterInteractionText(string registrationId, string interactionText)
        {
            if (string.IsNullOrEmpty(registrationId))
                return;

            ModLocalization.Set(InteractionLocalizationPrefix + registrationId, interactionText ?? string.Empty);
        }

        private static ObjectManager.ObjectType ResolveObjectType(ObjectPanelRegistration registration)
        {
            if (registration.ObjectType != ObjectManager.ObjectType.Undefined)
                return registration.ObjectType;

            if (string.IsNullOrEmpty(registration.ObjectId))
                return ObjectManager.ObjectType.Undefined;

            try
            {
                return (ObjectManager.ObjectType)Enum.Parse(typeof(ObjectManager.ObjectType), registration.ObjectId, true);
            }
            catch
            {
                return ObjectManager.ObjectType.Undefined;
            }
        }

        private sealed class RegistrationLease : IDisposable
        {
            private string _registrationId;

            public RegistrationLease(string registrationId)
            {
                _registrationId = registrationId;
            }

            public void Dispose()
            {
                if (_registrationId == null)
                    return;

                Unregister(_registrationId);
                _registrationId = null;
            }
        }
    }

    internal sealed class RuntimeObjectPanelInteraction : Int_Base
    {
        private string _registrationId;

        internal void Configure(string registrationId)
        {
            _registrationId = registrationId;
        }

        public override void Awake()
        {
            obj = GetComponent<Obj_Base>();
        }

        public override string GetInstanceTypeName()
        {
            return "ShelteredAPI.RuntimeObjectPanelInteraction";
        }

        public override string GetInteractionType()
        {
            return !string.IsNullOrEmpty(_registrationId) ? _registrationId : "sheltered_runtime_ui_object_panel";
        }

        public override int GetInteractionPriority()
        {
            return 1;
        }

        public override bool IsPlayerSelectable()
        {
            Obj_Base target = obj != null ? obj : GetComponent<Obj_Base>();
            return target != null && RuntimeObjectPanelRegistry.CanOpen(_registrationId, target, GetSelectedMember());
        }

        public override bool IsPlayerSelectableWithoutValidMember()
        {
            return true;
        }

        public override bool OnInteractionSelected(FamilyMember member)
        {
            Obj_Base target = obj != null ? obj : GetComponent<Obj_Base>();
            return target != null && RuntimeObjectPanelRegistry.TryOpen(_registrationId, target, member);
        }

        private static FamilyMember GetSelectedMember()
        {
            try
            {
                return InteractionManager.Instance != null ? InteractionManager.Instance.GetSelectedFamilyMember() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
