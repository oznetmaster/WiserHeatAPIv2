# Changelog

This changelog records shipped features, fixes, compatibility and runtime dependency changes. See [development and validation history](DEVELOPMENT-HISTORY.md) for tests, CI, build tooling and work not yet released.

## [1.1.1](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.1) - 2026-09-18

### Fixed

- Schedule JSON import and editor updates now return failure when the underlying update fails, instead of incorrectly returning success.
- Schedule copy, delete, update, assignment and import methods preserve the underlying command result consistently.

Public API signatures and runtime dependencies are unchanged.

## [1.1.0.6](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.6) — 2026-09-12

Patch release updating runtime dependencies. Public library API signatures are unchanged.

### Changed

- Update log4net from 3.3.1 to 3.4.0 in the library and console, and YamlDotNet from 18.0.0 to 18.1.0 in the library. The NuGet package now requires these updated dependency versions.

## [1.1.0.5](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.5) — 2026-09-12

### Fixed

- Retry requests now own separate copies of their content, avoiding disposed-content failures after a transient response. Intermediate responses are disposed before backoff.

- REST reads and commands preserve authentication and endpoint exception types instead of wrapping them as connection errors.

- Caller cancellation at the REST boundary remains cancellation; transport timeouts still report connection errors.

- Changing a connection's secret takes effect on subsequent requests.

- UTF-8 response parsing preserves accented and non-Latin names while retaining legacy invalid-control-character cleanup and accepting a leading UTF-8 BOM.

- Redirected schedule requests retain the same HTTP/1.0 policy and content headers as the original request.

Public API signatures are unchanged.