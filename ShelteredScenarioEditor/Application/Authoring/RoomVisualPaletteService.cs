using ShelteredAPI.Scenarios.Public;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class RoomVisualPaletteService
    {
        internal sealed class Entry
        {
            public int NativeIndex;
            public string RuntimeSpriteKey;
            public Sprite Sprite;
            public string SourceLabel;
        }

        private static readonly FieldInfo WiresSpritesField = typeof(ShelterRoomGrid).GetField("wiresSprites", BindingFlags.NonPublic | BindingFlags.Instance);

        public List<Entry> BuildWallPalette(ShelterRoom selectedRoom)
        {
            List<Entry> entries = new List<Entry>();
            Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            AddPaletteSprites(entries, byKey, selectedRoom != null ? selectedRoom.wallSprites : null, selectedRoom != null ? selectedRoom.wallSprites : null, "Selected room");

            ShelterRoom[] rooms = Resources.FindObjectsOfTypeAll<ShelterRoom>();
            for (int i = 0; rooms != null && i < rooms.Length; i++)
            {
                ShelterRoom room = rooms[i];
                if (room == null || room == selectedRoom)
                    continue;

                AddPaletteSprites(entries, byKey, selectedRoom != null ? selectedRoom.wallSprites : null, room.wallSprites, ResolveRoomPaletteSource(room));
            }

            return entries;
        }

        public List<Entry> BuildWirePalette(List<Sprite> selectedWires)
        {
            List<Entry> entries = new List<Entry>();
            Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

            AddPaletteSprites(entries, byKey, selectedWires, selectedWires, "Selected shelter");

            ShelterRoomGrid[] grids = Resources.FindObjectsOfTypeAll<ShelterRoomGrid>();
            for (int i = 0; grids != null && i < grids.Length; i++)
            {
                ShelterRoomGrid grid = grids[i];
                List<Sprite> wires = GetWireSprites(grid);
                if (wires == null || object.ReferenceEquals(wires, selectedWires))
                    continue;

                AddPaletteSprites(entries, byKey, selectedWires, wires, ResolveGridPaletteSource(grid));
            }

            return entries;
        }

        public Entry ResolveWallEntry(ShelterRoom room, int nativeIndex, string runtimeSpriteKey)
        {
            if (room == null)
                return null;

            if (nativeIndex >= 0 && room.wallSprites != null && nativeIndex < room.wallSprites.Count)
            {
                Sprite sprite = room.wallSprites[nativeIndex];
                return CreateNativeEntry(nativeIndex, sprite, "Selected room");
            }

            return FindEntry(BuildWallPalette(room), runtimeSpriteKey);
        }

        public Entry ResolveWireEntry(List<Sprite> nativeWires, int nativeIndex, string runtimeSpriteKey)
        {
            if (nativeIndex >= 0 && nativeWires != null && nativeIndex < nativeWires.Count)
            {
                Sprite sprite = nativeWires[nativeIndex];
                return CreateNativeEntry(nativeIndex, sprite, "Selected shelter");
            }

            return FindEntry(BuildWirePalette(nativeWires), runtimeSpriteKey);
        }

        public List<Sprite> GetWireSprites(ShelterRoomGrid grid)
        {
            return grid != null && WiresSpritesField != null ? WiresSpritesField.GetValue(grid) as List<Sprite> : null;
        }

        public int EnsureSprite(List<Sprite> sprites, Sprite sprite)
        {
            if (sprites == null || sprite == null)
                return -1;

            int existing = FindSpriteIndex(sprites, sprite);
            if (existing >= 0)
                return existing;

            sprites.Add(sprite);
            return sprites.Count - 1;
        }

        private static Entry CreateNativeEntry(int nativeIndex, Sprite sprite, string sourceLabel)
        {
            return new Entry
            {
                NativeIndex = nativeIndex,
                RuntimeSpriteKey = ShelteredScenarioRuntime.CreateRuntimeSpriteKey(sprite),
                Sprite = sprite,
                SourceLabel = sourceLabel
            };
        }

        private static void AddPaletteSprites(
            List<Entry> entries,
            Dictionary<string, Entry> byKey,
            List<Sprite> nativeSprites,
            List<Sprite> sprites,
            string sourceLabel)
        {
            if (entries == null || byKey == null || sprites == null)
                return;

            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite sprite = sprites[i];
                string runtimeSpriteKey = ShelteredScenarioRuntime.CreateRuntimeSpriteKey(sprite);
                if (sprite == null || string.IsNullOrEmpty(runtimeSpriteKey) || byKey.ContainsKey(runtimeSpriteKey))
                    continue;

                Entry entry = new Entry
                {
                    NativeIndex = FindSpriteIndex(nativeSprites, sprite),
                    RuntimeSpriteKey = runtimeSpriteKey,
                    Sprite = sprite,
                    SourceLabel = sourceLabel
                };
                byKey[runtimeSpriteKey] = entry;
                entries.Add(entry);
            }
        }

        private static Entry FindEntry(List<Entry> entries, string runtimeSpriteKey)
        {
            if (string.IsNullOrEmpty(runtimeSpriteKey))
                return null;

            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry != null && string.Equals(entry.RuntimeSpriteKey, runtimeSpriteKey, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private static int FindSpriteIndex(List<Sprite> sprites, Sprite sprite)
        {
            if (sprites == null || sprite == null)
                return -1;

            for (int i = 0; i < sprites.Count; i++)
            {
                if ((UnityEngine.Object)sprites[i] == (UnityEngine.Object)sprite)
                    return i;
            }

            return -1;
        }

        private static string ResolveRoomPaletteSource(ShelterRoom room)
        {
            if (room == null || room.gameObject == null)
                return "Loaded scenario room";

            string sceneName = room.gameObject.scene.IsValid() ? room.gameObject.scene.name : null;
            return !string.IsNullOrEmpty(sceneName) ? sceneName + " room" : "Loaded scenario room";
        }

        private static string ResolveGridPaletteSource(ShelterRoomGrid grid)
        {
            if (grid == null || grid.gameObject == null)
                return "Loaded scenario shelter";

            string sceneName = grid.gameObject.scene.IsValid() ? grid.gameObject.scene.name : null;
            return !string.IsNullOrEmpty(sceneName) ? sceneName + " shelter" : "Loaded scenario shelter";
        }
    }
}
