# WiserHeatAPIv2 v1.1.3

This patch fixes cancellation of HTTP response-body reads. Previously, a hub could return its response headers and then stop sending data, leaving a request waiting beyond the caller's cancellation deadline. Error responses had the same gap.

The .NET Framework build now reads the response stream with cancellation, including on Mono. Modern .NET retains its native cancellable content read. The response is released when cancelled, and the client remains available for subsequent requests. Public API signatures, target frameworks and runtime dependencies are unchanged.

Validation covers stalled headers, partially transmitted success and error bodies, caller cancellation and client reuse. The complete offline suite passes on .NET Framework 4.7.2, .NET 10 and the tested Mono runtime, including real loopback HTTP connections. These tests do not operate household devices.

See [CHANGELOG.md](CHANGELOG.md) for release history.
