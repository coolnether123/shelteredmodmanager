using System;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Core
{
    internal static class LoadingTransitionRuntime
    {
        public static string GetActiveSceneName()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                return string.IsNullOrEmpty(scene.name) ? "<empty>" : scene.name;
            }
            catch
            {
                return "<scene-error>";
            }
        }

        public static bool CanUseNgui()
        {
            return UnityEngine.Object.FindObjectOfType<UIRoot>() != null;
        }

        public static string DescribeSaveManagerState()
        {
            try
            {
                SaveManager manager = SaveManager.instance;
                if (manager == null)
                    return "<missing>";

                FieldInfo stateField = typeof(SaveManager).GetField("m_state", BindingFlags.Instance | BindingFlags.NonPublic);
                object state = stateField != null ? stateField.GetValue(manager) : null;
                return (state != null ? state.ToString() : "<unknown>") +
                    " saving=" + manager.isSaving +
                    " loading=" + manager.isLoading;
            }
            catch (Exception ex)
            {
                return "<error:" + ex.GetType().Name + ">";
            }
        }

        public static void ResetAfterFailedTransition()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ResetLoadingScreen();
            ResetSaveManager();
        }

        private static void ResetLoadingScreen()
        {
            try
            {
                LoadingScreen.ClearNextLevel();
                LoadingScreen screen = LoadingScreen.Instance;
                if (screen == null)
                    return;

                SetPrivateField(screen, "m_showCount", 0);
                SetPrivateField(screen, "m_showScreen", false);

                GameObject loadingImage = GetPrivateField(screen, "m_loadingImage") as GameObject;
                if (loadingImage != null)
                    loadingImage.SetActive(false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[LoadingTransitionRecovery] Failed to reset LoadingScreen state: " + ex.Message);
            }
        }

        private static void ResetSaveManager()
        {
            try
            {
                SaveManager manager = SaveManager.instance;
                if (manager == null)
                    return;

                manager.SceneLoadAsyncOp = null;
                SetPrivateField(manager, "m_pendingLoad", false);
                SetPrivateField(manager, "m_framesUntilLoad", 0);
                SetSaveManagerIdle(manager);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[LoadingTransitionRecovery] Failed to reset SaveManager transition state: " + ex.Message);
            }
        }

        private static void SetSaveManagerIdle(SaveManager manager)
        {
            MethodInfo setState = typeof(SaveManager).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setState != null)
            {
                ParameterInfo[] parameters = setState.GetParameters();
                if (parameters != null && parameters.Length == 1)
                {
                    object idle = Enum.Parse(parameters[0].ParameterType, "Idle");
                    setState.Invoke(manager, new[] { idle });
                    return;
                }
            }

            FieldInfo stateField = typeof(SaveManager).GetField("m_state", BindingFlags.Instance | BindingFlags.NonPublic);
            if (stateField != null)
                stateField.SetValue(manager, Enum.Parse(stateField.FieldType, "Idle"));
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(instance, value);
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(instance) : null;
        }
    }
}
