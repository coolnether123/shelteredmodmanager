using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Paging;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    internal static class AutoLoadFlow
    {
        public const int NewSaveSentinel = -1;

        private static bool _pendingNewSave;
        private static bool _modeChosen;
        private static bool _slotChosen;
        private static bool _mainMenuAdvanceIssued;
        private static bool _focusWaitLogged;

        public static bool NeedsMainMenuAdvance
        {
            get { return _pendingNewSave && !_mainMenuAdvanceIssued; }
        }

        public static void BeginNewSave()
        {
            _pendingNewSave = true;
            _modeChosen = false;
            _slotChosen = false;
            _mainMenuAdvanceIssued = false;
        }

        public static void Reset()
        {
            _pendingNewSave = false;
            _modeChosen = false;
            _slotChosen = false;
            _mainMenuAdvanceIssued = false;
            _focusWaitLogged = false;
        }

        public static void TryAdvanceMainMenu(MainMenu instance)
        {
            if (!NeedsMainMenuAdvance)
                return;

            if (!IsAutomationFocusReady("main menu"))
                return;

            if (!IsInputEnabled(instance))
                return;

            _mainMenuAdvanceIssued = true;
            MMLog.WriteDebug("[AutoLoad] Main menu ready. Triggering Play for auto-new-save.");
            instance.OnPlayButtonPressed();
        }

        public static void TryChooseMode(GameModeSelectionPanel instance)
        {
            if (!_pendingNewSave || _modeChosen)
                return;

            try
            {
                if (IsTweenReversing(instance))
                    return;

                if (!IsAutomationFocusReady("mode selection"))
                    return;

                if (!IsInputEnabled(instance))
                    return;

                MMLog.WriteDebug("[AutoLoad] Auto-selecting Survival mode for New Save.");
                instance.OnSurvivalModeChosen();
                _modeChosen = true;
            }
            catch (Exception ex)
            {
                Reset();
                MMLog.WriteError("[AutoLoad] Failed choosing mode: " + ex.Message);
            }
        }

        public static void TryChooseSlot(SlotSelectionPanel instance)
        {
            if (!_pendingNewSave || _slotChosen)
                return;

            if (instance == null || !instance.m_inputEnabled)
                return;

            try
            {
                if (IsTweenReversing(instance))
                    return;

                if (!IsAutomationFocusReady("slot selection"))
                    return;

                int lowestSlot = FindLowestAvailableSurvivalSlot();
                int targetPage;
                int targetIndex;
                ResolveSlotPage(lowestSlot, out targetPage, out targetIndex);

                if (!TryMoveToPage(instance, targetPage))
                    return;

                Traverse.Create(instance).Field("m_selectedSlot").SetValue(targetIndex);
                MMLog.Write("[AutoLoad] Starting New Save in slot " + lowestSlot
                    + " (page " + targetPage + ", index " + targetIndex + ").");

                _slotChosen = true;
                instance.OnSlotChosen();
                Reset();
            }
            catch (Exception ex)
            {
                Reset();
                MMLog.WriteError("[AutoLoad] Failed choosing New Save slot: " + ex.Message);
            }
        }

        private static void ResolveSlotPage(int slot, out int page, out int index)
        {
            if (slot <= 3)
            {
                page = 0;
                index = slot - 1;
                return;
            }

            int customOffset = slot - 4;
            page = (customOffset / 3) + 1;
            index = customOffset % 3;
        }

        private static bool TryMoveToPage(SlotSelectionPanel instance, int targetPage)
        {
            int currentPage = PagingManager.GetPage(instance);
            while (currentPage != targetPage)
            {
                int before = currentPage;
                int delta = currentPage < targetPage ? 1 : -1;
                if (before == 0 && delta > 0)
                    PagingManager.SuppressWelcomeDialogOnce();

                PagingManager.ChangePage(instance, delta);
                currentPage = PagingManager.GetPage(instance);
                if (currentPage == before)
                    return false;
            }

            return true;
        }

        private static bool IsInputEnabled(object instance)
        {
            if (instance == null)
                return false;

            Traverse traverse = Traverse.Create(instance);
            bool inputEnabled = traverse.Field("m_inputEnabled").GetValue<bool>();
            Traverse signedOutField = traverse.Field("m_userSignedOut");
            if (signedOutField.FieldExists() && signedOutField.GetValue<bool>())
                return false;

            return inputEnabled;
        }

        private static bool IsAutomationFocusReady(string stage)
        {
            if (IsGameForegroundWindow())
            {
                _focusWaitLogged = false;
                return true;
            }

            if (!_focusWaitLogged)
            {
                MMLog.WriteDebug("[AutoLoad] Waiting for game focus before continuing auto-new-save at " + stage + ".");
                _focusWaitLogged = true;
            }

            return false;
        }

        private static bool IsGameForegroundWindow()
        {
            try
            {
                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                    return true;

                System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
                process.Refresh();
                IntPtr mainWindow = process.MainWindowHandle;
                if (mainWindow == IntPtr.Zero)
                    return true;

                return foreground == mainWindow;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static bool IsTweenReversing(object instance)
        {
            if (instance == null)
                return false;

            FieldInfo tweenField = instance.GetType().GetField("m_tween", BindingFlags.NonPublic | BindingFlags.Instance);
            TweenAlpha tween = tweenField != null ? tweenField.GetValue(instance) as TweenAlpha : null;
            return tween != null && tween.direction == AnimationOrTween.Direction.Reverse;
        }

        private static int FindLowestAvailableSurvivalSlot()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                var info = SaveRegistryCore.ReadVanillaSaveInfo(slot);
                if (info == null)
                    return slot;
            }

            int customSlot = 4;
            while (ExpandedVanillaSaves.GetBySlot(customSlot) != null)
            {
                customSlot++;
            }

            return customSlot;
        }
    }
}
