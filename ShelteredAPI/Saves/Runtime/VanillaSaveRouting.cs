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
        internal static bool TryGetRoute(SaveManager.SaveType saveType, out VanillaSaveRoute route)
        {
            switch (saveType)
            {
                case SaveManager.SaveType.Slot1:
                    route = Create(saveType, ScenarioSaveIdGuards.StandardStorageScenarioId, 1, "Slot1", 1);
                    return true;
                case SaveManager.SaveType.Slot2:
                    route = Create(saveType, ScenarioSaveIdGuards.StandardStorageScenarioId, 2, "Slot2", 2);
                    return true;
                case SaveManager.SaveType.Slot3:
                    route = Create(saveType, ScenarioSaveIdGuards.StandardStorageScenarioId, 3, "Slot3", 3);
                    return true;
                case SaveManager.SaveType.SlotSurrounded:
                    route = Create(
                        saveType,
                        ScenarioSaveIdGuards.VanillaSurroundedStorageScenarioId,
                        1,
                        ScenarioSaveIdGuards.VanillaSurroundedSaveId,
                        4);
                    return true;
                case SaveManager.SaveType.SlotStasis:
                    route = Create(
                        saveType,
                        ScenarioSaveIdGuards.VanillaStasisStorageScenarioId,
                        1,
                        ScenarioSaveIdGuards.VanillaStasisSaveId,
                        5);
                    return true;
                default:
                    route = new VanillaSaveRoute();
                    return false;
            }
        }

        internal static bool TryGetRouteBySaveId(string saveId, out VanillaSaveRoute route)
        {
            VanillaSaveRoute candidate;
            if (TryGetRoute(SaveManager.SaveType.SlotSurrounded, out candidate)
                && string.Equals(candidate.SaveId, saveId, System.StringComparison.OrdinalIgnoreCase))
            {
                route = candidate;
                return true;
            }

            if (TryGetRoute(SaveManager.SaveType.SlotStasis, out candidate)
                && string.Equals(candidate.SaveId, saveId, System.StringComparison.OrdinalIgnoreCase))
            {
                route = candidate;
                return true;
            }

            route = new VanillaSaveRoute();
            return false;
        }

        internal static bool TryGetRouteByStorageScenarioId(string storageScenarioId, out VanillaSaveRoute route)
        {
            VanillaSaveRoute candidate;
            if (TryGetRoute(SaveManager.SaveType.SlotSurrounded, out candidate)
                && string.Equals(candidate.StorageScenarioId, storageScenarioId, System.StringComparison.OrdinalIgnoreCase))
            {
                route = candidate;
                return true;
            }

            if (TryGetRoute(SaveManager.SaveType.SlotStasis, out candidate)
                && string.Equals(candidate.StorageScenarioId, storageScenarioId, System.StringComparison.OrdinalIgnoreCase))
            {
                route = candidate;
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
