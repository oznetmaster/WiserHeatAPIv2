# WiserHeatAPIv2 v1.1.2

This patch corrects Fahrenheit readings and writes when `WiserAPI.Units` is `Imperial`. Room, thermostat, valve, underfloor, heating-actuator, system, converted schedule and OpenTherm values now follow the selected units. For example, a 20 C room returns 68 F, and a requested 68 F target sends 20 C to the hub.

Conversion occurs before applying the existing Celsius limits. Boost differences use the appropriate scale without an absolute-temperature offset. Off and hot-water control values remain control values, and raw hub dictionaries and raw schedule JSON remain unchanged. Setting a manual temperature from Off sends the requested target without briefly restoring the old scheduled target first.

Public API signatures, target frameworks and runtime dependencies are unchanged. Existing standalone OpenTherm constructors retain Celsius behavior. Applications displaying Fahrenheit must use corresponding Fahrenheit ranges and must not convert values a second time.

The offline suite passes on .NET Framework 4.7.2 and .NET 10, including synthetic command-payload, dynamic-unit, schedule round-trip and Off-state regressions. The net472 test build retains its existing RuntimeHelpers compiler warning. These checks do not claim live equipment validation. See [temperature units](docs/temperature-units.md), [CHANGELOG.md](CHANGELOG.md) and [development history](DEVELOPMENT-HISTORY.md).
