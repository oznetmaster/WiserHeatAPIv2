# WiserHeatAPIv2 v1.1.0.6

This patch updates the library's runtime dependencies to **log4net 3.4.0** and **YamlDotNet 18.1.0**. Public library API signatures are unchanged.

The repository now includes seven opt-in read-only hub tests and two explicitly selected room control tests. Control tests capture the starting state, restore it even after a failed command or assertion, and verify the result by reading the hub. They use a privately configured room, skip temperature changes when an existing override prevents reliable restoration, and require explicit selection.

Private settings and credentials remain outside the published package. Only an empty settings example is included in source. CI and release publishing run the offline suite and exclude all live tests.

Validation on **both net472 and net10.0**:

- **138 offline tests passed** per framework.
- **Six read-only live tests passed**; the optional OpenTherm check skipped because the hub returned no data.
- **Both room control tests passed**, with the original scheduled setpoint, Auto mode, window-detection setting, and absence of an override confirmed afterward.

Only the library is published to NuGet. The test suites are available in the repository and are not NuGet packages.

See [CHANGELOG.md](https://github.com/oznetmaster/WiserHeatAPIv2/blob/v1.1.0.6/CHANGELOG.md) and the [README](https://github.com/oznetmaster/WiserHeatAPIv2/blob/v1.1.0.6/README.md) for details and test setup instructions.