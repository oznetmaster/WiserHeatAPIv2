# WiserHeatAPIv2 v1.1.0.5

This patch fixes HTTP failures found while adding the library's offline NUnit suite.

- Retry requests retain their original payload after a transient response; intermediate responses are disposed before retrying.
- REST reads and commands preserve authentication and endpoint exception types.
- Caller cancellation remains cancellation rather than being reported as a timeout; actual transport timeouts still report connection errors.
- Updated connection secrets take effect on subsequent requests.
- UTF-8 room and device names preserve accented and non-Latin characters.
- Redirected schedule requests retain HTTP/1.0 and their content headers.

The new NUnit suite is included in the existing Visual Studio solution and covers requests, error handling, models, commands, schedules, refreshes, and disposal. CI and package publishing run it on both supported frameworks.

Validation: **112 tests passed on .NET Framework 4.7.2 and 112 passed on .NET 10**. The complete Release solution build passed with zero warnings or errors. Tests use simulated responses and do not require or operate a physical hub.

Public API signatures are unchanged. Callers of the REST controller can now catch its documented authentication and endpoint exceptions directly; code relying on every failure being wrapped as a connection error should account for these more specific exceptions.

Only the library is published to NuGet. The test project is not packable.

See [CHANGELOG.md](https://github.com/oznetmaster/WiserHeatAPIv2/blob/v1.1.0.5/CHANGELOG.md) for the detailed changelog.
