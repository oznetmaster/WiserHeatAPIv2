# WiserHeatAPIv2 v1.1.1

This patch fixes false success reports from schedule JSON import and editor updates after a rejected request or connection failure. Schedule forwarding methods now preserve their underlying command results consistently.

Public API signatures, target frameworks and runtime dependencies are unchanged. Callers should check the returned Boolean and refresh hub state when they need confirmation that a requested change has actually taken effect.

The offline suite passed on both .NET Framework 4.7.2 and .NET 10, including regression cases for accepted, rejected and disconnected schedule commands. See [development and validation history](DEVELOPMENT-HISTORY.md) for verification details, [CHANGELOG.md](CHANGELOG.md) for shipped fixes, and the [README](README.md) for usage.
