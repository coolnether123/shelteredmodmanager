using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Networking;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    [PatchPolicy(PatchDomain.UI, "MainMenuMultiplayerEntry",
        TargetBehavior = "Main menu multiplayer connectivity test button injection",
        FailureMode = "The main menu does not expose the multiplayer connectivity test window.",
        RollbackStrategy = "Disable the UI patch domain or remove the multiplayer menu patch host.",
        StartupTiming = PatchStartupTiming.BootCritical)]
    [HarmonyPatch(typeof(MainMenu), "OnShow")]
    internal static class MainMenuMultiplayer_OnShow_Patch
    {
        private const string ButtonName = "Button_Multiplayer";
        private const string ButtonText = "Multiplayer";

        public static void Postfix(MainMenu __instance)
        {
            try
            {
                EnsureShortcutHandler(__instance);

                UITablePivot table = GetButtonTable(__instance);
                if (table == null)
                {
                    MMLog.WriteWarning("[MainMenuMultiplayer] MainMenu.m_table was not available.");
                    return;
                }

                if (HasMultiplayerButton(table))
                {
                    MMLog.WriteInfo("[MainMenuMultiplayer] Multiplayer button already exists.");
                    return;
                }

                UIButton templateButton = FindTemplateButton(table);
                if (templateButton == null)
                {
                    MMLog.WriteWarning("[MainMenuMultiplayer] No suitable menu button template found.");
                    return;
                }

                UIButton multiplayerButton = UIUtil.CloneButton(templateButton, table.transform, ButtonText);
                if (multiplayerButton == null)
                    return;

                multiplayerButton.gameObject.name = ButtonName;
                multiplayerButton.gameObject.layer = table.gameObject.layer;

                UILabel[] labels = multiplayerButton.GetComponentsInChildren<UILabel>(true);
                for (int i = 0; i < labels.Length; i++)
                {
                    UILabel label = labels[i];
                    if (label == null)
                        continue;

                    label.fontSize = 32;
                    label.overflowMethod = UILabel.Overflow.ShrinkContent;
                    label.text = ButtonText;
                }

                multiplayerButton.onClick.Clear();
                EventDelegate.Add(multiplayerButton.onClick, OpenMultiplayerWindow);
                multiplayerButton.gameObject.SetActive(true);

                table.Reposition();
                InvokeUpdateButtonTable(__instance);

                MMLog.WriteInfo("[MainMenuMultiplayer] Injected Multiplayer button.");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[MainMenuMultiplayer] Failed to inject button: " + ex.Message);
            }
        }

        private static UITablePivot GetButtonTable(MainMenu menu)
        {
            if (menu == null)
                return null;

            FieldInfo tableField = typeof(MainMenu).GetField("m_table", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tableField == null)
                return null;

            return tableField.GetValue(menu) as UITablePivot;
        }

        private static bool HasMultiplayerButton(UITablePivot table)
        {
            if (table == null)
                return true;

            foreach (Transform child in table.transform)
            {
                if (child != null && child.name == ButtonName)
                    return true;
            }

            return false;
        }

        private static UIButton FindTemplateButton(UITablePivot table)
        {
            if (table != null && table.children != null)
            {
                for (int i = 0; i < table.children.Count; i++)
                {
                    Transform child = table.children[i];
                    if (child == null)
                        continue;

                    string name = child.name ?? string.Empty;
                    if (name.IndexOf("Play", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("Options", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    UIButton button = child.GetComponent<UIButton>();
                    if (button != null)
                        return button;
                }
            }

            return UIUtil.FindAnyButtonTemplate();
        }

        private static void InvokeUpdateButtonTable(MainMenu menu)
        {
            if (menu == null)
                return;

            MethodInfo updateMethod = typeof(MainMenu).GetMethod("UpdateButtonTable", BindingFlags.NonPublic | BindingFlags.Instance);
            if (updateMethod != null)
                updateMethod.Invoke(menu, null);
        }

        private static void OpenMultiplayerWindow()
        {
            MultiplayerMenuController.ShowWindow();
        }

        private static void EnsureShortcutHandler(MainMenu menu)
        {
            if (menu == null || menu.gameObject == null)
                return;

            if (menu.gameObject.GetComponent<MainMenuMultiplayerShortcutHandler>() == null)
                menu.gameObject.AddComponent<MainMenuMultiplayerShortcutHandler>();
        }
    }

    internal sealed class MainMenuMultiplayerShortcutHandler : MonoBehaviour
    {
        private const KeyCode ShortcutKey = KeyCode.F4;
        private static readonly FieldInfo InputEnabledField =
            typeof(MainMenu).GetField("m_inputEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo UserSignedOutField =
            typeof(MainMenu).GetField("m_userSignedOut", BindingFlags.NonPublic | BindingFlags.Instance);

        private MainMenu _menu;

        private void Awake()
        {
            _menu = GetComponent<MainMenu>();
        }

        private void Update()
        {
            if (!UnityEngine.Input.GetKeyDown(ShortcutKey))
                return;

            if (!IsMainMenuInputAvailable())
                return;

            MultiplayerMenuController.ShowWindow();
        }

        private bool IsMainMenuInputAvailable()
        {
            if (_menu == null)
                _menu = GetComponent<MainMenu>();

            if (_menu == null)
                return false;

            bool inputEnabled = InputEnabledField == null || GetBooleanField(InputEnabledField, _menu, true);
            bool userSignedOut = UserSignedOutField != null && GetBooleanField(UserSignedOutField, _menu, false);
            return inputEnabled && !userSignedOut;
        }

        private static bool GetBooleanField(FieldInfo field, object instance, bool fallback)
        {
            try
            {
                object value = field.GetValue(instance);
                return value is bool ? (bool)value : fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
