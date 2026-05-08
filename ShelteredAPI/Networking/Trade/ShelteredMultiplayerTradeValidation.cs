using System;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Trade
{
    internal sealed class ShelteredTradeCargoValidationResult
    {
        private ShelteredTradeCargoValidationResult(
            bool success,
            string errorMessage,
            int totalCargoLines,
            int totalItemCount)
        {
            Success = success;
            ErrorMessage = errorMessage ?? string.Empty;
            TotalCargoLines = totalCargoLines;
            TotalItemCount = totalItemCount;
        }

        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly int TotalCargoLines;
        public readonly int TotalItemCount;

        public static ShelteredTradeCargoValidationResult Ok(int totalCargoLines, int totalItemCount)
        {
            return new ShelteredTradeCargoValidationResult(true, string.Empty, totalCargoLines, totalItemCount);
        }

        public static ShelteredTradeCargoValidationResult Failed(string errorMessage, int totalCargoLines, int totalItemCount)
        {
            return new ShelteredTradeCargoValidationResult(false, errorMessage, totalCargoLines, totalItemCount);
        }
    }

    internal static class ShelteredMultiplayerTradeValidation
    {
        public static ShelteredTradeCargoValidationResult ValidateOfferIntent(
            ShelteredMultiplayerTradeEvent tradeEvent,
            Func<string, IItemStore> resolveOwnerStore)
        {
            if (resolveOwnerStore == null)
                return ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            return ShelteredMultiplayerTradeCargoResolver.ValidateCargoAvailable(tradeEvent, resolveOwnerStore);
        }
    }
}
