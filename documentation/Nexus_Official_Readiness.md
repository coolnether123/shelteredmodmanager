# Nexus Official Registration Readiness

This checklist tracks what SMM needs before requesting/using an official Nexus Mods application registration.

## Current Implementation

- SMM identifies API requests with `Application-Name`, `Application-Version`, `User-Agent`, and `APIKEY` headers.
- API keys are validated through the Nexus API key validation endpoint before the UI reports a connected account.
- Metadata browsing and update checks use Nexus v2/legacy read endpoints. Nexus v3 is reserved for the experimental publish/upload flow.
- Direct install selects active, non-archived files and lets Nexus's download-link endpoint or website-issued `nxm://` authorization make the authoritative access decision. Resolved mirror URLs are never stored.
- Non-premium downloads accept the short-lived authorization from Nexus `nxm://` links. SMM temporarily captures the link, restores the user's previous handler, validates domain/account/expiry, and DPAPI-encrypts any cross-process relay file.
- Direct install rejects unsafe packages, duplicate mod IDs, reserved folder names, non-ZIP archives, and unwritable `mods` folders before replacing an installed mod.
- Update rollback backups are created under the configured `mods` folder so replacing a mod remains an atomic same-volume operation even when SMM and Sheltered are installed on different drives.
- Publish/upload API actions are disabled in public 2.0 settings. The experimental code remains present for future review, but the stable manager only supports manual package handoff.

## Registration Checklist

1. Request/confirm the official Nexus application registration details for `Sheltered Mod Manager`.
2. Confirm the final Nexus application name/version/header expectations with Nexus before release.
3. Add Nexus SSO support when an app id is issued, rather than relying only on manual API-key copy/paste.
4. Test direct install with premium, supporter, and regular member accounts.
5. Test denied direct-download cases and confirm the UI opens the Nexus page/manual flow clearly.
6. Test rate-limit, timeout, offline, invalid API key, deleted file, hidden file, and file-not-found responses.
7. Keep upload/publish disabled for stable users until Nexus approves the app behavior and experimental v3 mod-file support expectations.

## Notes

- Nexus' public org describes v3 as the current actively developed API, but the official Node client still documents the download-link flow for resolving file mirrors.
- For non-premium users, mod-manager download links include short-lived authorization parameters from Nexus' website flow. SMM now consumes those parameters through a temporary `nxm://` capture without permanently replacing another manager's handler.
- Resolved download mirror URLs expire quickly and must not be cached.
- Nexus v3 is current, but its mod/file read and publish endpoints remain experimental and it does not currently expose download-link generation. SMM therefore keeps the official v1/v2 read/download paths where v3 has no stable replacement.
- The premium end-to-end review path was exercised against Sheltered mod 4/file 27: authorization, ZIP download, package validation, replacement, same-volume backup, Nexus metadata, and installed-version verification completed successfully.
