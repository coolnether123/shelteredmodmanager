namespace ModAPI.Core
{
    /// <summary>
    /// High-level helpers for commonly accessed game state (v1.0.1).
    /// Addresses the "Unified Home Storage" and "Game Logic" feedback.
    /// </summary>
    public interface IGameHelper
    {
        /// <summary>
        /// Get the total count of an item across all shelter storage managers
        /// (Inventory, Food, Water, Entertainment).
        /// </summary>
        int GetTotalOwned(string itemId);

        /// <summary>
        /// Get the total count of an item in the primary item inventory.
        /// </summary>
        int GetInventoryCount(string itemId);

        /// <summary>
        /// Try to find a player by their character ID.
        /// </summary>
        FamilyMember FindMember(string characterId);
    }
}
