using System;
using System.Collections.Generic;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class NexusUploadOwnershipService
    {
        public NexusOwnershipVerification Verify(ModItem localMod, NexusUploadDraft draft, NexusAccountStatus account, IList<NexusRemoteMod> ownedMods)
        {
            var result = new NexusOwnershipVerification();
            if (localMod == null || draft == null)
            {
                result.Summary = "Select a mod to verify ownership.";
                return result;
            }

            NexusRemoteMod match = FindBestMatch(localMod, draft, ownedMods);
            if (match == null)
            {
                result.Summary = "No matching Nexus mod was found for this local mod.";
                return result;
            }

            result.RemoteMod = match;
            if (account != null && account.UserId > 0 && match.UploaderId == account.UserId)
            {
                result.IsVerified = true;
                result.Kind = NexusOwnershipVerificationKind.UploaderId;
                result.Summary = "Verified by Nexus uploader id: " + account.UserId + ".";
                return result;
            }

            if (account != null && !string.IsNullOrEmpty(account.UserName) &&
                string.Equals(match.UploaderName, account.UserName, StringComparison.OrdinalIgnoreCase))
            {
                result.IsVerified = true;
                result.Kind = NexusOwnershipVerificationKind.UploaderName;
                result.Summary = "Verified by Nexus uploader name: " + account.UserName + ".";
                return result;
            }

            if (HasAuthor(localMod, match.Author) || ContainsAuthorText(draft.AuthorsText, match.Author))
            {
                result.IsVerified = true;
                result.Kind = NexusOwnershipVerificationKind.AuthorName;
                result.Summary = "Matched by author name. Confirm the Nexus account before publishing.";
                return result;
            }

            result.Summary = "A possible Nexus mod was found, but ownership was not verified.";
            return result;
        }

        private static NexusRemoteMod FindBestMatch(ModItem localMod, NexusUploadDraft draft, IList<NexusRemoteMod> ownedMods)
        {
            if (ownedMods == null || ownedMods.Count == 0)
                return null;

            for (int i = 0; i < ownedMods.Count; i++)
            {
                NexusRemoteMod mod = ownedMods[i];
                if (mod == null)
                    continue;

                if (draft.NexusModId > 0 && mod.ModId == draft.NexusModId)
                    return mod;
                if (localMod.NexusModId > 0 && mod.ModId == localMod.NexusModId)
                    return mod;
            }

            for (int i = 0; i < ownedMods.Count; i++)
            {
                NexusRemoteMod mod = ownedMods[i];
                if (mod == null)
                    continue;

                if (SameName(mod.Name, draft.Name) || SameName(mod.Name, localMod.DisplayName))
                    return mod;
            }

            return null;
        }

        private static bool SameName(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasAuthor(ModItem localMod, string author)
        {
            if (localMod == null || localMod.Authors == null || string.IsNullOrEmpty(author))
                return false;

            for (int i = 0; i < localMod.Authors.Length; i++)
            {
                if (string.Equals(localMod.Authors[i], author, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsAuthorText(string authorsText, string author)
        {
            return !string.IsNullOrEmpty(authorsText)
                && !string.IsNullOrEmpty(author)
                && authorsText.IndexOf(author, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
