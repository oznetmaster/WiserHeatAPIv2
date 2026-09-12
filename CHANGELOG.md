# Changelog

## Unreleased

- Document console and WPF settings-file lookup, local build copying, per-clone exclusions, and direct links to each sample; distinguish these inputs from the NUnit live-test JSON settings.

- Remove pre-existing console and WPF credential files from tracked source, add placeholder examples, and explicitly exclude private settings from application publishing. Local copies remain available. This removes the files from current source; older Git history and tags still require separate remediation.


## [1.1.0.6](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.6) — 2026-09-12

Patch release updating runtime dependencies and adding live-test coverage. Public library API signatures are unchanged.

### Added

- Seven opt-in, read-only Wiser hub live tests with private JSON settings, a local enable flag, and NUnit runner parameter overrides.
- Two explicitly selected room control tests with private room selection, initial-state capture, cleanup after failed commands or assertions, and verified restoration. The temperature test requires schedule-controlled Auto mode without existing overrides and uses a one-minute override before restoring schedule control.
- Offline configuration and restoration tests, bringing the offline suite to 138 tests per framework.
- README instructions covering offline, read-only live, and room control execution, private settings, and optional capability skips.

### Changed

- CI and package publishing explicitly exclude the Live category.
- Update log4net from 3.3.1 to 3.4.0 in the library and console, and YamlDotNet from 18.0.0 to 18.1.0 in the library. The NuGet package now requires these updated dependency versions.

### Validation

- All 138 offline tests passed on net472 and net10.0.
- Six read-only live tests passed on each framework; OpenTherm skipped because the hub returned no data for this optional capability.
- Both room control tests passed on each framework. Final hub readback confirmed the original scheduled setpoint, Auto mode, window-detection setting, and absence of an active override.

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