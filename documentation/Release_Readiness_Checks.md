# Check release readiness without opening the game

Keep the Manager version unchanged. These checks prepare a candidate, but do not authorize publication, deployment or account sign-in.

## Inspect stored Nexus state

Run `tools/Read-NexusAuthenticationState.ps1` with an explicit settings path:

```powershell
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File tools/Read-NexusAuthenticationState.ps1 -SettingsPath '<game folder>\SMM\bin\mod_manager.ini'
```

The probe reports protected-field presence, whether Nexus is enabled and the expiry category. It does not decrypt, refresh or remove credentials. `SessionValidity` remains `not-checked`: stored fields do not prove the account is connected. An absent file does not prove another Manager installation has no session.

Do not use `SettingsService.Load` for this inspection. That method can sanitize invalid settings and write the file.

## Check capture and menu contracts

Run the offline contracts through hidden PowerShell:

```powershell
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File tools/Test-NexusAuthenticationState.ps1
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File tools/performance/Test-NativeMenuCaptureContracts.ps1
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File tools/performance/Test-ShelteredBenchmarkContracts.ps1
```

The capture tests use a synthetic native boundary. They do not capture the desktop, open a window or prove that Windows can capture a live game.

Native capture validates both PrintWindow modes. Blank frames and blank references cannot count as matches. A configured reference that is missing stops session preparation before the install snapshot or game launch. An intentionally unconfigured reference retains the labeled `window-delay-fallback`, which is not exact menu evidence.

The menu gate still requires three consecutive matches at the configured RMSE threshold. Do not raise that threshold or replace the reference merely to clear a failed run. Keep the failure receipt and diagnose the game state, capture provider and reference independently. A synchronous PrintWindow call can still block inside Windows; these offline checks do not prove bounded live capture latency.

## Preserve the live acceptance gate

Run the documented build and API boundary checks, then verify the exact package hashes, provenance and packaged Manager OAuth-only contracts. Keep account acceptance separate from those checks.

A real acceptance session needs the user's participation for OAuth and a separately authorized, snapshot-backed storefront test. Public listing access proves neither account authentication nor download authorization. A stable release needs current account, storefront and save evidence against the exact candidate. Publication remains a separate decision.
