using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    internal sealed class FieldManualPageTurnAudio
    {
        private readonly VanillaPageTurnAssets _assets;

        public FieldManualPageTurnAudio(VanillaPageTurnAssets assets)
        {
            _assets = assets;
        }

        public void Play()
        {
            AudioClip clip = _assets != null ? _assets.FindPageTurnSound() : null;
            if (clip != null && UISound.instance != null)
            {
                UISound.instance.Play(clip);
                return;
            }

            if (UISound.instance != null)
                UISound.instance.PlayPreset(UISound.PresetSound.Navigate);
        }
    }
}
