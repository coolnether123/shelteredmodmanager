using System;
using System.Collections;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    internal sealed class FieldManualPageTurnController : MonoBehaviour
    {
        private FieldManualPageTurnProfile _profile;
        private IFieldManualTransition _hideTransition;
        private IFieldManualTransition _contentRevealTransition;
        private IFieldManualTransition _labelRevealTransition;
        private FieldManualPageTurnAudio _audio;
        private FieldManualPageFlipOverlay _flipOverlay;
        private bool _isTurning;
        private float _lockoutUntil;

        public bool IsLocked
        {
            get { return _isTurning || Time.realtimeSinceStartup < _lockoutUntil; }
        }

        public void Configure(
            FieldManualPageTurnProfile profile,
            IFieldManualTransition hideTransition,
            IFieldManualTransition contentRevealTransition,
            IFieldManualTransition labelRevealTransition,
            FieldManualPageTurnAudio audio,
            FieldManualPageFlipOverlay flipOverlay)
        {
            _profile = profile ?? FieldManualPageTurnProfile.VanillaClipboard;
            _hideTransition = hideTransition;
            _contentRevealTransition = contentRevealTransition;
            _labelRevealTransition = labelRevealTransition;
            _audio = audio;
            _flipOverlay = flipOverlay;
        }

        public bool TryTurn(
            int delta,
            GameObject contentRoot,
            GameObject flipParent,
            GameObject labelRoot,
            Func<int, bool> canTurn,
            Action<int> commitPage,
            Action rebuildPage)
        {
            if (IsLocked || canTurn == null || commitPage == null || rebuildPage == null)
                return false;

            if (!canTurn(delta))
                return false;

            StartCoroutine(TurnRoutine(delta, contentRoot, flipParent, labelRoot, commitPage, rebuildPage));
            return true;
        }

        private IEnumerator TurnRoutine(
            int delta,
            GameObject contentRoot,
            GameObject flipParent,
            GameObject labelRoot,
            Action<int> commitPage,
            Action rebuildPage)
        {
            FieldManualPageTurnProfile profile = _profile ?? FieldManualPageTurnProfile.VanillaClipboard;
            _isTurning = true;
            _lockoutUntil = Time.realtimeSinceStartup + profile.LockoutDuration;

            if (_audio != null)
                _audio.Play();

            if (_hideTransition != null && contentRoot != null)
                _hideTransition.Play(contentRoot);

            if (_flipOverlay != null)
                _flipOverlay.Play(flipParent != null ? flipParent : contentRoot, profile.FlipDuration, delta);

            if (profile.RebuildDelay > 0f)
                yield return StartCoroutine(WaitRealtime(profile.RebuildDelay));

            commitPage(delta);
            rebuildPage();

            if (_contentRevealTransition != null && contentRoot != null)
                _contentRevealTransition.Play(contentRoot);
            if (_labelRevealTransition != null && labelRoot != null)
                _labelRevealTransition.Play(labelRoot);

            float remaining = profile.LockoutDuration - (Time.realtimeSinceStartup - (_lockoutUntil - profile.LockoutDuration));
            if (remaining > 0f)
                yield return StartCoroutine(WaitRealtime(remaining));

            _isTurning = false;
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float endAt = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < endAt)
                yield return null;
        }
    }
}
