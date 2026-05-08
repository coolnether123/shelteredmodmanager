using System;
using ModAPI.Core;

namespace ShelteredAPI.Networking.Travel
{
    internal sealed class ShelteredExpeditionTravelHookService
    {
        private const string LogSource = "ShelteredAPI.ExpeditionTravel";
        private static readonly ShelteredExpeditionTravelHookService _instance =
            new ShelteredExpeditionTravelHookService();
        private readonly ShelteredExpeditionTravelReader _reader = new ShelteredExpeditionTravelReader();
        private readonly ShelteredTravelStateRegistry _registry = new ShelteredTravelStateRegistry();
        private readonly ShelteredTravelReplayService _replay;

        private ShelteredExpeditionTravelHookService()
        {
            _replay = new ShelteredTravelReplayService(_registry);
        }

        public static ShelteredExpeditionTravelHookService Instance
        {
            get { return _instance; }
        }

        public IShelteredTravelStateRegistry Registry
        {
            get { return _registry; }
        }

        public void OnTravelStarted(ExplorationParty party)
        {
            ShelteredExpeditionTravelReadResult result = _reader.TryBuildTravelStarted(party);
            if (!result.Success)
            {
                ShelteredExpeditionTravelReader.WarnExtractionFailed("Begin_Traveling", result);
                return;
            }

            PublishStarted(result.Started);
        }

        public void OnPartyRecalled(ExplorationParty party)
        {
            if (!HasActiveTravelForParty(party))
                return;

            PublishCorrection(party, ShelteredTravelCorrectionReasons.Recall, "RecallToShelter");
        }

        public void OnPartyDisbanded(ExplorationParty party)
        {
            if (!HasActiveTravelForParty(party))
                return;

            ShelteredExpeditionTravelReadResult result = _reader.TryBuildTravelArrived(party, ShelteredTravelArrivalKinds.Cancelled);
            if (!result.Success)
            {
                ShelteredExpeditionTravelReader.WarnExtractionFailed("DisbandExplorationParty", result);
                return;
            }

            PublishArrived(result.Arrived);
        }

        public void OnPartyStatePushed(ExplorationParty party, ExplorationParty.ePartyState state)
        {
            if (ShelteredExpeditionTravelReader.IsInterruptionState(state))
            {
                if (!HasActiveTravelForParty(party))
                    return;

                PublishCorrection(party, ShelteredExpeditionTravelReader.ReasonForState(state), "PushState." + state);
                return;
            }

            if (ShelteredExpeditionTravelReader.IsArrivalState(state))
            {
                if (!HasActiveTravelForParty(party))
                    return;

                ShelteredExpeditionTravelReadResult result = _reader.TryBuildTravelArrived(party, ShelteredTravelArrivalKinds.ReturnedHome);
                if (!result.Success)
                {
                    ShelteredExpeditionTravelReader.WarnExtractionFailed("PushState." + state, result);
                    return;
                }

                PublishArrived(result.Arrived);
            }
        }

        private void PublishCorrection(ExplorationParty party, string reason, string source)
        {
            ShelteredExpeditionTravelReadResult result = _reader.TryBuildTravelCorrected(party, reason);
            if (!result.Success)
            {
                ShelteredExpeditionTravelReader.WarnExtractionFailed(source, result);
                return;
            }

            ShelteredTravelSyncService sync = ShelteredTravelSyncService.Active;
            if (sync != null && sync.PublishTravelCorrected(result.Corrected))
                return;

            _replay.ReplayCorrection(result.Corrected, CreateLocalEventId(result.Corrected.TravelId, source), CurrentWorldTick);
        }

        private void PublishStarted(ShelteredTravelStartedEvent started)
        {
            ShelteredTravelSyncService sync = ShelteredTravelSyncService.Active;
            if (sync != null && sync.PublishTravelStarted(started))
                return;

            _registry.ApplyTravelStarted(started, CreateLocalEventId(started.TravelId, "local-start"));
        }

        private void PublishArrived(ShelteredTravelArrivedEvent arrived)
        {
            ShelteredTravelSyncService sync = ShelteredTravelSyncService.Active;
            if (sync != null && sync.PublishTravelArrived(arrived))
                return;

            _registry.ApplyTravelArrived(arrived, CreateLocalEventId(arrived.TravelId, "local-arrival"));
        }

        private static long CurrentWorldTick
        {
            get
            {
                ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
                return context != null ? context.WorldTick : 0;
            }
        }

        private static string CreateLocalEventId(string travelId, string source)
        {
            return "local:" + (source ?? string.Empty) + ":" + (travelId ?? string.Empty) + ":" + CurrentWorldTick.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool HasActiveTravelForParty(ExplorationParty party)
        {
            if (party == null)
                return false;

            System.Collections.Generic.IList<ShelteredTravelState> active = _registry.GetActive();
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].PartyId == party.id)
                    return true;
            }

            return false;
        }

        internal static void WarnHookFailed(string hook, Exception ex)
        {
            MMLog.WarnOnce(
                LogSource + ".HookFailed." + (hook ?? string.Empty),
                "Expedition travel hook failed at " + (hook ?? string.Empty) + ": " + (ex != null ? ex.Message : "unknown error") + ".");
        }
    }
}
