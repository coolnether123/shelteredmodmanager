using System;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesApiVersion
    {
        public const string CurrentApiVersion = "1.0.0";
        public const string CurrentAdapterVersion = "1.0.0";
        public const string CurrentGameId = "paralives";
        public const string CurrentDisplayName = "Paralives";

        public static readonly ParalivesApiVersion Current = new ParalivesApiVersion(
            CurrentApiVersion,
            CurrentAdapterVersion,
            CurrentGameId,
            CurrentDisplayName);

        public ParalivesApiVersion(
            string apiVersion,
            string adapterVersion,
            string gameId,
            string displayName)
        {
            if (string.IsNullOrWhiteSpace(apiVersion))
                throw new ArgumentException("An API version is required.", "apiVersion");
            if (string.IsNullOrWhiteSpace(adapterVersion))
                throw new ArgumentException("An adapter version is required.", "adapterVersion");
            if (string.IsNullOrWhiteSpace(gameId))
                throw new ArgumentException("A game id is required.", "gameId");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", "displayName");

            ApiVersion = apiVersion.Trim();
            AdapterVersion = adapterVersion.Trim();
            GameId = gameId.Trim();
            DisplayName = displayName.Trim();
        }

        public string ApiVersion { get; private set; }

        public string AdapterVersion { get; private set; }

        public string GameId { get; private set; }

        public string DisplayName { get; private set; }
    }
}
