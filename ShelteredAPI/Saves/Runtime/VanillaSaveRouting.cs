namespace ShelteredAPI.Saves.Runtime
{
    internal struct VanillaSaveRoute
    {
        public SaveManager.SaveType SaveType;
        public string StorageScenarioId;
        public int AbsoluteSlot;
        public string SaveId;
        public int VanillaSlotNumber;
    }

    internal static class VanillaSaveRouting
    {
        private static readonly VanillaSaveRoute[] Routes =
        {
            Create(SaveManager.SaveType.Slot1, ScenarioSaveIdGuards.StandardStorageScenarioId, 1, "Slot1", 1),
            Create(SaveManager.SaveType.Slot2, ScenarioSaveIdGuards.StandardStorageScenarioId, 2, "Slot2", 2),
            Create(SaveManager.SaveType.Slot3, ScenarioSaveIdGuards.StandardStorageScenarioId, 3, "Slot3", 3),
            Create(SaveManager.SaveType.SlotSurrounded, ScenarioSaveIdGuards.VanillaSurroundedStorageScenarioId, 1, ScenarioSaveIdGuards.VanillaSurroundedSaveId, 4),
            Create(SaveManager.SaveType.SlotStasis, ScenarioSaveIdGuards.VanillaStasisStorageScenarioId, 1, ScenarioSaveIdGuards.VanillaStasisSaveId, 5)
        };

        internal static bool TryGetRoute(SaveManager.SaveType saveType, out VanillaSaveRoute route)
        {
            for (int i = 0; i < Routes.Length; i++)
            {
                if (Routes[i].SaveType != saveType)
                    continue;

                route = Routes[i];
                return true;
            }

            route = new VanillaSaveRoute();
            return false;
        }

        internal static bool TryGetRouteBySaveId(string saveId, out VanillaSaveRoute route)
        {
            for (int i = 3; i < Routes.Length; i++)
            {
                if (!string.Equals(Routes[i].SaveId, saveId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                route = Routes[i];
                return true;
            }

            route = new VanillaSaveRoute();
            return false;
        }

        internal static bool TryGetRouteByVanillaSlotNumber(int vanillaSlotNumber, out VanillaSaveRoute route)
        {
            for (int i = 0; i < Routes.Length; i++)
            {
                if (Routes[i].VanillaSlotNumber != vanillaSlotNumber)
                    continue;

                route = Routes[i];
                return true;
            }

            route = new VanillaSaveRoute();
            return false;
        }

        internal static bool TryGetRouteByStorageLocation(string storageScenarioId, int absoluteSlot, out VanillaSaveRoute route)
        {
            string normalizedScenarioId = SaveStorageRouter.NormalizeScenarioId(storageScenarioId);
            for (int i = 0; i < Routes.Length; i++)
            {
                if (Routes[i].AbsoluteSlot != absoluteSlot
                    || !string.Equals(Routes[i].StorageScenarioId, normalizedScenarioId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                route = Routes[i];
                return true;
            }

            route = new VanillaSaveRoute();
            return false;
        }

        private static VanillaSaveRoute Create(
            SaveManager.SaveType saveType,
            string storageScenarioId,
            int absoluteSlot,
            string saveId,
            int vanillaSlotNumber)
        {
            return new VanillaSaveRoute
            {
                SaveType = saveType,
                StorageScenarioId = storageScenarioId,
                AbsoluteSlot = absoluteSlot,
                SaveId = saveId,
                VanillaSlotNumber = vanillaSlotNumber
            };
        }
    }
}
