# Nexus OAuth Implementation

Sheltered Mod Manager is a public Windows desktop OAuth client. It uses the authorization-code flow with PKCE and does not have a client secret.

## Registration values

- Application: `Sheltered Mod Manager`
- Callback URI: `http://127.0.0.1:52147/callback`
- Authorization endpoint: `https://users.nexusmods.com/oauth/authorize`
- Token endpoint: `https://users.nexusmods.com/oauth/token`
- PKCE method: `S256`

`NexusOAuthConfiguration.ClientId` remains empty in review builds until Nexus issues the application registration. The Manager disables the sign-in button while that value is empty, while still exposing the completed callback and storage implementation for review.

## Sign-in sequence

1. Generate a 64-byte random PKCE verifier and 32-byte random state.
2. Derive the SHA-256 base64url PKCE challenge.
3. Bind a temporary `TcpListener` exclusively to `IPAddress.Loopback` on port `52147`.
4. Open the Nexus authorization URL in the user's default browser.
5. Accept a bounded HTTP GET request for `/callback`.
6. Reject non-loopback clients, unexpected paths, malformed parameters, provider errors, and state mismatches.
7. Exchange the authorization code and original verifier for tokens.
8. Protect the access and refresh tokens independently with Windows DPAPI CurrentUser.
9. Close the listener and show a browser response that contains no authorization code or token.

The listener is active only during sign-in and stops after success, an error, or five minutes. Using a raw loopback TCP listener avoids Windows `HttpListener` URL ACL requirements and does not require elevation.

## API authentication

All Nexus clients share one credential provider. It:

- returns a usable OAuth bearer token when available;
- refreshes shortly before expiry and persists the rotated tokens;
- clears the session and requires reconnection when refresh fails or Nexus rejects authorization;
- returns no credential for public metadata requests when OAuth is not configured.

Request headers always include the application name, version, and user agent. Authenticated requests use `Authorization: Bearer`. Production request code has no personal-key header, constructor, override, or fallback branch.

The v1, v2 GraphQL, and v3 clients share credential-scoped rate-limit state inside the active Nexus service. They consume the hourly and daily remaining-request response headers, reserve known capacity across concurrent requests, and stop locally when Nexus reports an exhausted quota. OAuth token-endpoint throttling is handled separately and preserves `Retry-After` guidance rather than mixing user-service limits into API quota state.

## Security boundaries

- No client secret is present because installed desktop applications cannot keep one confidential.
- OAuth state comparison processes the complete values before deciding equality.
- Tokens, authorization codes, PKCE verifiers, state, download mirrors, and `nxm://` authorization values are never logged.
- Access and refresh tokens use distinct DPAPI entropy and are never written to plaintext settings keys.
- Legacy personal-key settings from older releases are deleted during load/save without entering the in-memory settings model or an HTTP request.
- Signing out clears both tokens and their expiry from persisted settings.
- Publishing remains disabled independently of authentication.

## Verification

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\Test-NexusOAuthContracts.ps1
```

The contract tests exercise PKCE construction, callback acceptance/rejection, DPAPI round-tripping, loopback binding, protected storage, bearer headers, successful and failed refresh behavior, logout, legacy-state scrubbing, public-client constraints, and UI wiring without requiring a live client ID.

The Nexus Manager contract suite also runs a local HTTP behavior harness that verifies exact application-header values, OAuth-session isolation, bearer-only requests, `401` invalidation, concurrent quota reservations, stale-response handling, UTC resets, HTTP 429 behavior, and presigned-request isolation.
