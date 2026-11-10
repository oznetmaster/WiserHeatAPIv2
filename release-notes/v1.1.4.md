# WiserHeatAPIv2 1.1.4

This patch corrects the audited reliability issues while retaining the existing public API and supported frameworks.

- Hub refreshes serialize the complete multi-endpoint read and entity update, retaining existing entity identities. Disposal cancels queued refreshes and prevents late publication before disposing the transport. System updates replace their private dictionary rather than clearing caller-owned data. Automatic transient retries apply only to GET; writes are not replayed because a failure response does not prove that the hub rejected the operation. Entity collections remain mutable live views: applications must coordinate their own collection edits and enumeration and await refresh completion before consuming related values.

## Validation

- Offline workstation tests passed in Debug and Release on .NET 10 and .NET Framework 4.7.2: Wiser net10.0: 216; Wiser net472: 216. No failures or skips.
- 57 selected tests passed in the shared 374-case processor run on Mono 6.12.0.107 (32-bit), with no failures or skips. The merged host's resolved dependency graph was tested; these are not eight independent release-package tests.
- Fake transports and credentials exercised ordering, cancellation, shutdown and failure recovery. No new household-device or cloud control was performed. Pairing storage is unchanged.
- Strict XML, formatting and style checks passed. Package and companion-app preparation is verified separately by the shared release pipeline.

See [the changelog](https://github.com/oznetmaster/WiserHeatAPIv2/blob/v1.1.4/CHANGELOG.md) and [validation provenance](https://github.com/oznetmaster/WiserHeatAPIv2/blob/v1.1.4/release-validation/v1.1.4.json).
