# Changelog

## [1.1.0.5](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.5) — 2026-09-12

### Added

- Offline NUnit 4 test project in the existing Visual Studio solution, targeting .NET Framework 4.7.2 and .NET 10 with the latest C# language version.
- Coverage of REST requests and failures, schedules, room and device commands, telemetry conversion, repeated hub refreshes, and resource disposal.
- GitHub CI execution on both targets with downloadable test results.
- Internal HTTP handler injection for deterministic tests; existing public constructor signatures are unchanged.

### Fixed

- Retry requests now own separate copies of their content, avoiding disposed-content failures after a transient response. Intermediate responses are disposed before backoff.
- REST reads and commands preserve authentication and endpoint exception types instead of wrapping them as connection errors.
- Caller cancellation at the REST boundary remains cancellation; transport timeouts still report connection errors.
- Changing a connection's secret takes effect on subsequent requests.
- UTF-8 response parsing preserves accented and non-Latin names while retaining legacy invalid-control-character cleanup and accepting a leading UTF-8 BOM.
- Redirected schedule requests retain the same HTTP/1.0 policy and content headers as the original request.

Public API signatures are unchanged. The offline suite passes all 112 tests on each supported target framework; no live hub is required.