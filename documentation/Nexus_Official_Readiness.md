# Nexus Official Registration Readiness

This checklist tracks what SMM needs before requesting/using an official Nexus Mods application registration.

## Current Implementation

- SMM identifies API requests with `Application-Name`, `Application-Version`, `User-Agent`, and `APIKEY` headers.
- API keys are validated through the Nexus API key validation endpoint before the UI reports a connected account.
- Metadata browsing uses Nexus API v3 where SMM has adopted v3 endpoints.
- Direct install selects files that Nexus marks as manager-downloadable, resolves short-lived mirror URLs through the Nexus download-link endpoint, and never stores resolved mirror URLs.
- Direct install rejects unsafe packages, duplicate mod IDs, reserved folder names, non-ZIP archives, and unwritable `mods` folders before replacing an installed mod.
- Publish/upload API actions remain experimental and hidden unless Nexus integration and the experimental publish setting are enabled. Package creation streams file content instead of loading the full package into memory.

## Registration Checklist

1. Request/confirm the official Nexus application registration details for `Sheltered Mod Manager`.
2. Confirm the final Nexus application name/version/header expectations with Nexus before release.
3. Add Nexus SSO support when an app id is issued, rather than relying only on manual API-key copy/paste.
4. Add `nxm://` protocol handling before advertising non-premium one-click mod-manager downloads.
5. Test direct install with premium/supporter and regular member accounts.
6. Test denied direct-download cases and confirm the UI opens the Nexus page/manual flow clearly.
7. Test rate-limit, timeout, offline, invalid API key, deleted file, hidden file, and file-not-found responses.
8. Keep upload/publish disabled for stable users until Nexus approves the app behavior and upload support expectations.

## Notes

- Nexus' public org describes v3 as the current actively developed API, but the official Node client still documents the download-link flow for resolving file mirrors.
- For non-premium users, mod-manager download links can include short-lived authorization parameters from Nexus' website flow. SMM has a service method ready for those parameters, but full `nxm://` handler registration is still a release task.
- Resolved download mirror URLs expire quickly and must not be cached.
