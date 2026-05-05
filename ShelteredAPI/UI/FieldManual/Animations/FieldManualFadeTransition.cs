using UnityEngine;


using ShelteredAPI.Hooks;
namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Fades all NGUI widgets under a target while preserving each widget's own alpha.
    /// </summary>
    internal sealed class FieldManualFadeTransition : IFieldManualTransition
    {
        private readonly FieldManualTransitionProfile _profile;

        public FieldManualFadeTransition(FieldManualTransitionProfile profile)
        {
            _profile = profile ?? FieldManualTransitionProfile.VanillaPageInfoFade;
        }

        public void Play(GameObject target)
        {
            UIWidgetAlphaGroup alphaGroup = GetAlphaGroup(target);
            if (alphaGroup == null)
                return;

            alphaGroup.Play(_profile);
        }

        public void Complete(GameObject target)
        {
            UIWidgetAlphaGroup alphaGroup = GetAlphaGroup(target);
            if (alphaGroup == null)
                return;

            alphaGroup.Complete(_profile.ToAlpha);
        }

        private static UIWidgetAlphaGroup GetAlphaGroup(GameObject target)
        {
            if (target == null)
                return null;

            UIWidgetAlphaGroup alphaGroup = target.GetComponent<UIWidgetAlphaGroup>();
            if (alphaGroup == null)
                alphaGroup = target.AddComponent<UIWidgetAlphaGroup>();

            return alphaGroup;
        }
    }
}
