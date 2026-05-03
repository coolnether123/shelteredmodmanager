using System;
using System.Collections.Generic;
using ShelteredAPI.Interactions;

namespace ShelteredAPI.UI.Internal
{
    internal static class RuntimeObjectPanelRegistry
    {
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

            InteractionRegistry.For(objectType)
                .Add(registration.InteractionText, typeof(RuntimeObjectPanelInteraction))
                .WithPriority(registration.Priority > 0 ? registration.Priority : 1)
                .When(delegate(Obj_Base obj) { return CanOpen(registrationId, obj, null); })
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

            return registration.CanOpen(new ObjectPanelContext(registration.ObjectId, target, member));
        }

        public static bool TryOpen(string registrationId, Obj_Base target, FamilyMember member)
        {
            ObjectPanelRegistration registration;
            if (!TryGet(registrationId, out registration))
                return false;

            if (!CanOpen(registrationId, target, member))
                return false;

            registration.Open(new ObjectPanelContext(registration.ObjectId, target, member));
            return true;
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
            if (!string.IsNullOrEmpty(registration.ObjectId))
                return registration.ObjectId + "::" + registration.InteractionText;

            return objectType + "::" + registration.InteractionText;
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
            return target != null && RuntimeObjectPanelRegistry.CanOpen(_registrationId, target, null);
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
    }
}
