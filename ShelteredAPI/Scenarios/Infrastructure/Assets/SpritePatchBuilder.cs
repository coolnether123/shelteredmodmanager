using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class SpritePatchBuilder
    {
        public SpritePatchDefinition Build(
            string patchId,
            string displayName,
            string baseSpriteId,
            string baseRelativePath,
            string baseRuntimeSpriteKey,
            Texture2D beforeTexture,
            Texture2D afterTexture)
        {
            if (beforeTexture == null || afterTexture == null)
                return null;

            int width = Mathf.Min(beforeTexture.width, afterTexture.width);
            int height = Mathf.Min(beforeTexture.height, afterTexture.height);
            SpritePatchDefinition patch = new SpritePatchDefinition
            {
                Id = patchId,
                DisplayName = displayName,
                BaseSpriteId = baseSpriteId,
                BaseRelativePath = baseRelativePath,
                BaseRuntimeSpriteKey = baseRuntimeSpriteKey,
                Width = width,
                Height = height
            };

            SpritePatchOperation operation = new SpritePatchOperation
            {
                Id = patchId + ".pixels",
                Order = 0,
                Kind = SpritePatchOperationKind.Pixels
            };

            for (int y = 0; y < height; y++)
            {
                int runStart = -1;
                string runColor = null;
                int runLength = 0;
                for (int x = 0; x < width; x++)
                {
                    Color before = beforeTexture.GetPixel(x, y);
                    Color after = afterTexture.GetPixel(x, y);
                    string afterHex = EncodeColor(after);
                    bool changed = before != after;
                    if (changed && runStart < 0)
                    {
                        runStart = x;
                        runColor = afterHex;
                        runLength = 1;
                        continue;
                    }

                    if (changed && runStart >= 0 && string.Equals(runColor, afterHex, System.StringComparison.OrdinalIgnoreCase))
                    {
                        runLength++;
                        continue;
                    }

                    if (runStart >= 0)
                    {
                        operation.Runs.Add(new SpritePatchDeltaRun
                        {
                            X = runStart,
                            Y = y,
                            Length = runLength,
                            ColorHex = runColor
                        });
                        runStart = changed ? x : -1;
                        runColor = changed ? afterHex : null;
                        runLength = changed ? 1 : 0;
                    }
                }

                if (runStart >= 0)
                {
                    operation.Runs.Add(new SpritePatchDeltaRun
                    {
                        X = runStart,
                        Y = y,
                        Length = runLength,
                        ColorHex = runColor
                    });
                }
            }

            if (operation.Runs.Count == 0)
                operation.Runs.Add(new SpritePatchDeltaRun { X = 0, Y = 0, Length = 1, ColorHex = EncodeColor(afterTexture.GetPixel(0, 0)) });

            patch.Operations.Add(operation);
            return patch;
        }

        private static string EncodeColor(Color color)
        {
            Color32 color32 = color;
            return color32.r.ToString("X2")
                + color32.g.ToString("X2")
                + color32.b.ToString("X2")
                + color32.a.ToString("X2");
        }
    }
}
