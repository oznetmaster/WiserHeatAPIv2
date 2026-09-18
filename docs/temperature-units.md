# Temperature units

These corrections are source work after version 1.1.1 and are not yet published in a package.

`WiserAPI.Units` selects `WiserUnits.Metric` (Celsius) or `WiserUnits.Imperial` (Fahrenheit). The hub protocol always carries Celsius values, usually in tenths of a degree. Room, radiator valve, thermostat, underfloor controller, heating actuator, OpenTherm, system target and converted heating-schedule values follow the configured units. Raw hub dictionaries and raw schedule JSON remain in the hub's format.

For example, a hub temperature of `200` represents 20°C and is returned as 68°F in Imperial mode. Sending a 68°F room target sends `200` to the hub. Changing `Units` changes subsequent model readings without altering the hub settings.

Heating commands retain the library's existing 5–30°C limits, equivalent to 41–86°F. Conversion happens before applying those limits. Boost increases are temperature differences: a 3.6°F increase is a 2°C increase, without a 32-degree offset. The existing maximum increase remains 5°C (9°F).

The heating Off value (`Constants.TEMP_OFF`, -20) and hot-water On/Off constants are control sentinels rather than temperatures. They retain their meaning in either unit system. Missing override readings retain the documented zero value. YAML/editor schedule conversion uses the selected units; raw `DegreesC` arrays do not change their protocol representation.

Regression coverage uses a synthetic transport and checks readings, commands, limits, differences, Off values, unit changes, schedule import/export and preservation of raw values. No live heating commands are needed for these checks. The normal Release build includes the heating-actuator and OpenTherm models. Their readings are covered, including retained OpenTherm operational views after a units change. Standalone OpenTherm constructors retain Celsius compatibility. Downstream applications must still use ranges and display values appropriate to their selected units.
