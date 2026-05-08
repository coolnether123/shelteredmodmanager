using System;

namespace ShelteredAPI.Networking.Settlements
{
    internal sealed class ShelteredSettlementProductionService
    {
        public ShelteredSettlementProductionResult BuildProduction(ShelteredSettlementState settlement, long worldTick)
        {
            ShelteredSettlementProductionResult result = new ShelteredSettlementProductionResult();
            if (settlement == null || string.IsNullOrEmpty(settlement.SettlementId))
                return result;

            result.SettlementId = settlement.SettlementId;
            result.ProductionTick = worldTick;
            result.ProductionScore = Math.Max(0, settlement.Population) + Math.Max(0, settlement.Defense / 10);
            for (int i = 0; settlement.ProductionTags != null && i < settlement.ProductionTags.Count; i++)
            {
                string tag = settlement.ProductionTags[i] ?? string.Empty;
                if (tag.Length > 0)
                    result.ProducedTags.Add(tag);
            }

            return result;
        }
    }
}
