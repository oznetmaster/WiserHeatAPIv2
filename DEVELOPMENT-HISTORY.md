# Development and validation history

See the [product changelog](CHANGELOG.md) for shipped changes. This document preserves test, CI and build history. Dated development entries describe work at that time, not a published product version or completed acceptance. Version headings identify the release alongside which development work was recorded.

## Where changes belong

- Product changelog and product release notes: shipped behavior, API, compatibility, fixes and runtime dependencies. Mention validation briefly when it helps explain a fix.
- This history: test coverage, CI, build tooling, work on pending versions. Split mixed entries so the product effect remains easy to find.
- Testing and workflow guides: current setup and operating instructions.
- Test-only or documentation-only changes do not require a product release.

<!-- development-history -->

## Offline release workflow option - 2026-09-15 (no package release)

- Allow an explicit manual release when local hardware or the self-hosted runner is unavailable, with the reason and exact source recorded in the workflow summary.
- Keep hosted source validation mandatory and preserve all build, test and packaging steps. No runtime, API or package-version changes.

## CI validation - 2026-09-15 (no package release)

- Revalidate the current default-branch source after successful release workflows, including version commits created by GitHub Actions.
- Allow maintainers to configure exact-source, App-specific checks that must pass before publishing through `RELEASE_REQUIRED_CHECKS`; missing, failed or unconfirmed checks block the release.

## Test and development tooling - 2026-09-15 (no library release)

- Remove NUnit `Explicit` from the room-control fixture to use the same live-settings opt-in as the other fixtures. The `Live`/`LiveControl` categories, configured-room requirement and state restoration remain unchanged.

- Document console and WPF settings-file lookup, local build copying, per-clone exclusions, and direct links to each sample; distinguish these inputs from the NUnit live-test JSON settings.

- Remove pre-existing console and WPF credential files from tracked source, add placeholder examples, and explicitly exclude private settings from application publishing. Local copies remain available. This removes the files from current source; older Git history and tags still require separate remediation.

## [1.1.0.6](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.6) — 2026-09-12

Patch release updating runtime dependencies and adding live-test coverage. Public library API signatures are unchanged.

- Seven opt-in, read-only Wiser hub live tests with private JSON settings, a local enable flag, and NUnit runner parameter overrides.

- Two explicitly selected room control tests with private room selection, initial-state capture, cleanup after failed commands or assertions, and verified restoration. The temperature test requires schedule-controlled Auto mode without existing overrides and uses a one-minute override before restoring schedule control.

- Offline configuration and restoration tests, bringing the offline suite to 138 tests per framework.

- README instructions covering offline, read-only live, and room control execution, private settings, and optional capability skips.

- CI and package publishing explicitly exclude the Live category.

### Validation

- All 138 offline tests passed on net472 and net10.0.

- Six read-only live tests passed on each framework; OpenTherm skipped because the hub returned no data for this optional capability.

- Both room control tests passed on each framework. Final hub readback confirmed the original scheduled setpoint, Auto mode, window-detection setting, and absence of an active override.

## [1.1.0.5](https://github.com/oznetmaster/WiserHeatAPIv2/releases/tag/v1.1.0.5) — 2026-09-12

- Offline NUnit 4 test project in the existing Visual Studio solution, targeting .NET Framework 4.7.2 and .NET 10 with the latest C# language version.

- Coverage of REST requests and failures, schedules, room and device commands, telemetry conversion, repeated hub refreshes, and resource disposal.

- GitHub CI execution on both targets with downloadable test results.

- Internal HTTP handler injection for deterministic tests; existing public constructor signatures are unchanged.

Public API signatures are unchanged. The offline suite passes all 112 tests on each supported target framework; no live hub is required.