using System;
using System.Collections.Generic;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    /// <summary>
    /// Registers custom Paralives notifications and provides a guarded way to dispatch them.
    /// </summary>
    public sealed class ParalivesNotificationRegistry
    {
        private readonly object _sync = new object();
        private readonly List<Notification> _notifications = new List<Notification>();

        public int RegisteredNotificationCount
        {
            get { lock (_sync) return _notifications.Count; }
        }

        public void Register(Notification notification)
        {
            if (notification == null)
                throw new ArgumentNullException("notification");
            if (notification.GUID == 0UL)
                throw new ArgumentException("Registered notifications must have a non-zero GUID.", "notification");

            lock (_sync)
                Upsert(_notifications, notification);
        }

        public bool EnsureRegistered()
        {
            return ApplyWhenReady();
        }

        public bool ApplyWhenReady()
        {
            try
            {
                return ApplyWhenReadyCore();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesNotificationRegistry.ApplyWhenReady", "Failed to apply Paralives notification registrations: " + ex.Message);
                return false;
            }
        }

        public NotificationData CreateNotificationData(ulong notificationGuid, params ulong[] characterGuids)
        {
            return new NotificationData
            {
                NotificationGUID = notificationGuid,
                CharacterGUIDs = characterGuids
            };
        }

        public bool Show(ulong notificationGuid, params ulong[] characterGuids)
        {
            return Show(CreateNotificationData(notificationGuid, characterGuids), true);
        }

        public bool Show(NotificationData notificationData)
        {
            return Show(notificationData, true);
        }

        public bool Show(NotificationData notificationData, bool ignoreNonHouseholdCharacters)
        {
            if (notificationData == null)
                throw new ArgumentNullException("notificationData");
            if (notificationData.NotificationGUID == 0UL)
                throw new ArgumentException("Notifications must have a non-zero NotificationGUID.", "notificationData");

            try
            {
                return ShowCore(notificationData, ignoreNonHouseholdCharacters);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesNotificationRegistry.Show", "Failed to show Paralives notification: " + ex.Message);
                return false;
            }
        }

        private bool ApplyWhenReadyCore()
        {
            if (Settings.Instance == null)
                return false;

            Notifications notifications = Settings.Get<Notifications>();
            if (notifications == null)
                return false;

            Notification[] pending;
            lock (_sync)
                pending = _notifications.ToArray();

            bool changed = false;
            for (int i = 0; i < pending.Length; i++)
                changed |= EnsureNotification(notifications, pending[i]);

            return changed;
        }

        private bool ShowCore(NotificationData notificationData, bool ignoreNonHouseholdCharacters)
        {
            if (Settings.Instance == null)
                return false;

            Notifications notifications = Settings.Get<Notifications>();
            if (notifications == null)
                return false;

            if (notifications.GetNotificationByGUID(notificationData.NotificationGUID) == null)
            {
                ApplyWhenReady();
                notifications = Settings.Get<Notifications>();
                if (notifications == null || notifications.GetNotificationByGUID(notificationData.NotificationGUID) == null)
                    return false;
            }

            if (notificationData.CharacterGUIDs == null)
                notificationData.CharacterGUIDs = new ulong[0];

            NotificationManager.Instance.ShowNotification(notificationData, ignoreNonHouseholdCharacters);
            return true;
        }

        private static bool EnsureNotification(Notifications notifications, Notification notification)
        {
            if (notification == null || notification.GUID == 0UL)
                return false;

            if (ContainsNotification(notifications.AllNotifications, notification.GUID))
                return false;

            notifications.AllNotifications = Append(notifications.AllNotifications, notification);
            return true;
        }

        private static bool ContainsNotification(Notification[] notifications, ulong guid)
        {
            if (notifications == null)
                return false;

            for (int i = 0; i < notifications.Length; i++)
            {
                if (notifications[i] != null && notifications[i].GUID == guid)
                    return true;
            }

            return false;
        }

        private static void Upsert(List<Notification> notifications, Notification notification)
        {
            for (int i = 0; i < notifications.Count; i++)
            {
                if (notifications[i] != null && notifications[i].GUID == notification.GUID)
                {
                    notifications[i] = notification;
                    return;
                }
            }

            notifications.Add(notification);
        }

        private static T[] Append<T>(T[] source, T item)
        {
            int length = source != null ? source.Length : 0;
            T[] result = new T[length + 1];
            if (length > 0)
                Array.Copy(source, result, length);

            result[length] = item;
            return result;
        }
    }
}
