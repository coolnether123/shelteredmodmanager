# Nexus Official Registration Readiness

This checklist tracks what SMM needs before requesting/using an official Nexus Mods application registration.

## Current Implementation

- SMM identifies every API request with `Application-Name`, `Application-Version`, and `User-Agent`. Authenticated requests prefer `Authorization: Bearer` and retain `APIKEY` only as a legacy fallback.
- OAuth authorization code + PKCE (`S256`) is implemented for the public Windows desktop Manager.
- The registered callback is `http://127.0.0.1:52147/callback`. Its temporary listener binds only to `127.0.0.1`, validates callback path and state, limits input, times out after five minutes, and does not require an administrator HTTP URL reservation.
- OAuth access and refresh tokens are encrypted at rest with Windows DPAPI CurrentUser using separate purpose-specific entropy. No client secret exists in the application.
- Nexus credentials are validated through the v1 user-validation endpoint before the UI reports a connected account.
- Metadata browsing and update checks use Nexus v2/legacy read endpoints. Nexus v3 is reserved for the experimental publish/upload flow.
- Direct install selects active, non-archived files and lets Nexus's download-link endpoint or website-issued `nxm://` authorization make the authoritative access decision. Resolved mirror URLs are never stored.
- Non-premium downloads accept the short-lived authorization from Nexus `nxm://` links. SMM temporarily captures the link, restores the user's previous handler, validates domain/account/expiry, and DPAPI-encrypts any cross-process relay file.
- Direct install rejects unsafe packages, duplicate mod IDs, reserved folder names, non-ZIP archives, and unwritable `mods` folders before replacing an installed mod.
- Update rollback backups are created under the configured `mods` folder so replacing a mod remains an atomic same-volume operation even when SMM and Sheltered are installed on different drives.
- Publish/upload API actions are disabled in public 2.0 settings. The experimental code remains present for future review, but the stable manager only supports manual package handoff.

## Registration Checklist

1. Send Nexus the current GitHub review branch/prerelease and the callback URI `http://127.0.0.1:52147/callback`.
2. Receive the official public-client ID and place it in `NexusOAuthConfiguration.ClientId`.
3. Run live OAuth approval, denial, refresh, revocation, and sign-out tests.
4. Test direct install with premium, supporter, and regular member accounts.
5. Test denied direct-download cases and confirm the UI opens the Nexus page/manual flow clearly.
6. Test rate-limit, timeout, offline, invalid/revoked credential, deleted file, hidden file, file-not-found, and occupied callback-port responses.
7. Keep upload/publish disabled for stable users until Nexus approves the app behavior and experimental v3 mod-file support expectations.

## Notes

- Nexus' public org describes v3 as the current actively developed API, but the official Node client still documents the download-link flow for resolving file mirrors.
- Nexus's desktop OAuth guidance recommends a loopback listener. SMM uses a fixed registration URI on an unprivileged high port and binds only to IPv4 loopback.
- For non-premium users, mod-manager download links include short-lived authorization parameters from Nexus' website flow. SMM now consumes those parameters through a temporary `nxm://` capture without permanently replacing another manager's handler.
- Resolved download mirror URLs expire quickly and must not be cached.
- Nexus v3 is current, but its mod/file read and publish endpoints remain experimental and it does not currently expose download-link generation. SMM therefore keeps the official v1/v2 read/download paths where v3 has no stable replacement.
- The premium end-to-end review path was exercised against Sheltered mod 4/file 27: authorization, ZIP download, package validation, replacement, same-volume backup, Nexus metadata, and installed-version verification completed successfully.
