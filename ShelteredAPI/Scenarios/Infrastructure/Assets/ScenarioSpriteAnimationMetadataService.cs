using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal sealed class ScenarioSpriteAnimationMetadataService
    {
        internal sealed class AnimationFrame
        {
            public int Index;
            public Sprite Sprite;
            public string RuntimeSpriteKey;
            public float DurationSeconds;
        }

        internal sealed class AnimationMetadata
        {
            public string ClipName;
            public float ClipLengthSeconds;
            public float SampleRate;
            public List<AnimationFrame> Frames;
        }

        public AnimationMetadata Resolve(ScenarioSpriteRuntimeResolver.ResolvedTarget target)
        {
            if (target == null || target.Kind != Definitions.ScenarioSpriteTargetComponentKind.SpriteRenderer || target.SpriteRenderer == null)
                return null;

            Animator animator = target.SpriteRenderer.GetComponent<Animator>();
            if (animator == null)
                animator = target.SpriteRenderer.GetComponentInParent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                return null;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0)
                return null;

            AnimationMetadata best = null;
            string currentKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(target.CurrentSprite);
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationMetadata metadata = SampleClip(target.SpriteRenderer, clips[i], currentKey);
                if (metadata == null || metadata.Frames == null || metadata.Frames.Count <= 1)
                    continue;

                if (ContainsFrame(metadata, currentKey))
                    return metadata;
                if (best == null)
                    best = metadata;
            }

            return best;
        }

        private static bool ContainsFrame(AnimationMetadata metadata, string runtimeSpriteKey)
        {
            if (metadata == null || metadata.Frames == null || string.IsNullOrEmpty(runtimeSpriteKey))
                return false;

            for (int i = 0; i < metadata.Frames.Count; i++)
            {
                AnimationFrame frame = metadata.Frames[i];
                if (frame != null && string.Equals(frame.RuntimeSpriteKey, runtimeSpriteKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static AnimationMetadata SampleClip(SpriteRenderer sourceRenderer, AnimationClip clip, string currentKey)
        {
            if (sourceRenderer == null || clip == null || clip.length <= 0f)
                return null;

            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(sourceRenderer.gameObject) as GameObject;
                if (clone == null)
                    return null;
                clone.name = "ScenarioAnimationSample_" + sourceRenderer.gameObject.name;
                clone.hideFlags = HideFlags.HideAndDontSave;
                clone.SetActive(false);

                Animator[] animators = clone.GetComponentsInChildren<Animator>(true);
                for (int i = 0; animators != null && i < animators.Length; i++)
                    animators[i].enabled = false;

                SpriteRenderer cloneRenderer = clone.GetComponent<SpriteRenderer>();
                if (cloneRenderer == null)
                    cloneRenderer = clone.GetComponentInChildren<SpriteRenderer>(true);
                if (cloneRenderer == null)
                    return null;

                float sampleRate = Mathf.Max(1f, clip.frameRate);
                float step = 1f / sampleRate;
                List<AnimationFrame> frames = new List<AnimationFrame>();
                string previousKey = null;
                float previousTime = 0f;

                for (float time = 0f; time <= clip.length + (step * 0.5f); time += step)
                {
                    float sampleTime = Mathf.Min(time, clip.length);
                    clip.SampleAnimation(clone, sampleTime);
                    Sprite sampled = cloneRenderer.sprite;
                    string key = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(sampled);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (frames.Count > 0)
                            frames[frames.Count - 1].DurationSeconds = Mathf.Max(step, sampleTime - previousTime);

                        frames.Add(new AnimationFrame
                        {
                            Index = frames.Count,
                            Sprite = sampled,
                            RuntimeSpriteKey = key,
                            DurationSeconds = step
                        });
                        previousKey = key;
                        previousTime = sampleTime;
                    }
                }

                if (frames.Count > 0)
                    frames[frames.Count - 1].DurationSeconds = Mathf.Max(step, clip.length - previousTime);

                if (frames.Count <= 1)
                    return null;

                return new AnimationMetadata
                {
                    ClipName = !string.IsNullOrEmpty(clip.name) ? clip.name : "Animation",
                    ClipLengthSeconds = clip.length,
                    SampleRate = sampleRate,
                    Frames = frames
                };
            }
            catch
            {
                return null;
            }
            finally
            {
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);
            }
        }
    }
}
