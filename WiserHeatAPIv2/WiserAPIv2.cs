// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
using System.Collections.Concurrent;
using System.Globalization;

using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.Constants;

using log4net;

namespace WiserHeatApiV2;

/// <summary>
/// Library-wide constants used by the Wiser API v2 surface.
/// </summary>
public static class Constants
	{
	// Temperature Constants
	/// <summary>Default away mode temperature in degrees.</summary>
	public const double DEFAULT_AWAY_MODE_TEMP = 10.5;
	/// <summary>Default degraded temperature in degrees.</summary>
	public const double DEFAULT_DEGRADED_TEMP = 18;
	/// <summary>Maximum temperature increase for Boost.</summary>
	public const int MAX_BOOST_INCREASE = 5;
	/// <summary>Hub error sentinel for temperature (>= indicates error).</summary>
	public const int TEMP_ERROR = 2000;
	/// <summary>Minimum allowed temperature in degrees.</summary>
	public const int TEMP_MINIMUM = 5;
	/// <summary>Maximum allowed temperature in degrees.</summary>
	public const int TEMP_MAXIMUM = 30;
	/// <summary>Hot water "on" special temperature sentinel.</summary>
	public const int TEMP_HW_ON = 110;
	/// <summary>Hot water "off" special temperature sentinel.</summary>
	public const int TEMP_HW_OFF = -20;
	/// <summary>Heating off sentinel temperature.</summary>
	public const int TEMP_OFF = -20;

	// Battery Constants
	/// <summary>Minimum room stat battery voltage (V).</summary>
	public const double ROOMSTAT_MIN_BATTERY_LEVEL = 1.7;
	/// <summary>Full room stat battery voltage (V).</summary>
	public const double ROOMSTAT_FULL_BATTERY_LEVEL = 2.7;
	/// <summary>Full TRV battery voltage (V).</summary>
	public const double TRV_FULL_BATTERY_LEVEL = 3.0;
	/// <summary>Minimum TRV battery voltage (V).</summary>
	public const double TRV_MIN_BATTERY_LEVEL = 2.4;

	// Text Values
	/// <summary>Auto mode text value.</summary>
	public const string TEXT_AUTO = "Auto";
	/// <summary>Close action text value.</summary>
	public const string TEXT_CLOSE = "Close";
	/// <summary>Temperature key for DegreesC.</summary>
	public const string TEXT_DEGREES_C = "DegreesC";
	/// <summary>Heating schedule type text.</summary>
	public const string TEXT_HEATING = "Heating";
	/// <summary>Level schedule type text.</summary>
	public const string TEXT_LEVEL = "Level";
#if LIGHT
	/// <summary>Lighting schedule type text.</summary>
	public const string TEXT_LIGHTING = "Lighting";
#endif
	/// <summary>Manual mode text.</summary>
	public const string TEXT_MANUAL = "Manual";
	/// <summary>NoChange action text.</summary>
	public const string TEXT_NO_CHANGE = "NoChange";
	/// <summary>None text value.</summary>
	public const string TEXT_NONE = "None";
	/// <summary>Off text value.</summary>
	public const string TEXT_OFF = "Off";
	/// <summary>On text value.</summary>
	public const string TEXT_ON = "On";
	/// <summary>OnOff schedule type text.</summary>
	public const string TEXT_ON_OFF = "OnOff";
	/// <summary>Open action text value.</summary>
	public const string TEXT_OPEN = "Open";
	/// <summary>Setpoint key text.</summary>
	public const string TEXT_SETPOINT = "Setpoint";
#if SHUTTER
	/// <summary>Shutters schedule type text.</summary>
	public const string TEXT_SHUTTERS = "Shutters";
#endif
	/// <summary>State key text.</summary>
	public const string TEXT_STATE = "State";
	/// <summary>Temp key text.</summary>
	public const string TEXT_TEMP = "Temp";
	/// <summary>Time key text.</summary>
	public const string TEXT_TIME = "Time";
	/// <summary>Unknown text value.</summary>
	public const string TEXT_UNKNOWN = "Unknown";
	/// <summary>Weekdays schedule special day.</summary>
	public const string TEXT_WEEKDAYS = "Weekdays";
	/// <summary>Weekends schedule special day.</summary>
	public const string TEXT_WEEKENDS = "Weekends";

	// Day Value Lists
	/// <summary>Weekday names in order Monday..Friday.</summary>
	public static readonly List<string> Weekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
	/// <summary>Weekend names in order Saturday..Sunday.</summary>
	public static readonly List<string> Weekends = ["Saturday", "Sunday"];
	/// <summary>Special day labels allowed in schedules.</summary>
	public static readonly List<string> SpecialDays = [TEXT_WEEKDAYS, TEXT_WEEKENDS];
	/// <summary>Special time placeholders for sunrise and sunset.</summary>
	public static readonly Dictionary<string, int> SpecialTimes = new () { { "Sunrise", 3000 }, { "Sunset", 4000 } };

	// Battery Level Enum
	/// <summary>Mapping of TRV battery voltage to percent.
	/// Keys are voltage values; values are percentage.</summary>
	public static readonly Dictionary<double, int> TrvBatteryLevelMapping = new ()
		{
			{ 3.0, 100 }, { 2.9, 80 }, { 2.8, 60 }, { 2.7, 40 }, { 2.6, 30 }, { 2.5, 20 }, { 2.4, 10 }, { 2.3, 0 }
	  };

	/// <summary>Default empty level schedule structure keyed by day name.</summary>
	public static readonly Dictionary<string, Dictionary<string, object>> DefaultLevelSchedule = new ()
		{
			{ "Monday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Tuesday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Wednesday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Thursday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Friday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Saturday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } },
			{ "Sunday", new Dictionary<string, object> { { "Time", new List<string>() }, { "Level", new List<int>() } } }
	  };
	}

/// <summary>Indicates the preferred unit system for temperatures.</summary>
public enum WiserUnits
	{
	/// <summary>Imperial units (Fahrenheit).</summary>
	Imperial,
	/// <summary>Metric units (Celsius).</summary>
	Metric
	}

/// <summary>Schedule categories supported by the Wiser hub.</summary>
public enum WiserScheduleType
	{
	/// <summary>Heating schedules (temperature).</summary>
	Heating,
	/// <summary>On/off schedules.</summary>
	OnOff,
	/// <summary>Level schedules (e.g., percentage).</summary>
	Level,
	/// <summary>Lighting schedules.</summary>
	Lighting,
	/// <summary>Shutter schedules.</summary>
	Shutters
	}

/// <summary>Heating operating modes.</summary>
public enum WiserHeatingMode
	{
	/// <summary>Heating off.</summary>
	Off,
	/// <summary>Follow schedule.</summary>
	Auto,
	/// <summary>Manual setpoint control.</summary>
	Manual
	}

/// <summary>Hot water operating modes.</summary>
public enum WiserHotWaterMode
	{
	/// <summary>Follow schedule.</summary>
	Auto,
	/// <summary>Manual control.</summary>
	Manual
	}

/// <summary>Smart plug operating modes.</summary>
public enum WiserSmartPlugMode
	{
	/// <summary>Follow schedule.</summary>
	Auto,
	/// <summary>Manual control.</summary>
	Manual
	}

#if SHUTTER
/// <summary>Shutter operating modes.</summary>
public enum WiserShutterMode
	{
	/// <summary>Follow schedule.</summary>
	Auto,
	/// <summary>Manual control.</summary>
	Manual
	}
#endif
#if LIGHT
/// <summary>Light operating modes.</summary>
public enum WiserLightMode
	{
	/// <summary>Follow schedule.</summary>
	Auto,
	/// <summary>Manual control.</summary>
	Manual
	}
#endif

/// <summary>Actions applied when the system enters Away mode.</summary>
public enum WiserAwayAction
	{
	/// <summary>Turn off.</summary>
	Off,
	/// <summary>Leave current state unchanged.</summary>
	NoChange,
	/// <summary>Close (for shutters, valves, etc.).</summary>
	Close
	}

/// <summary>
/// Exception indicating a not-implemented operation in the Wiser hub API.
/// </summary>
/// <param name="message">Human-readable description of the missing or unsupported operation.</param>
public class WiserHubNotImplementedException (string message) : Exception (message)
	{
	}

/// <summary>
/// Temperature conversion helpers between hub values and real-world temperatures.
/// </summary>
public static class WiserTemperatureFunctions
	{
	/// <summary>
	/// Converts a real-world temperature to a Wiser hub scaled integer value (tenths of a degree),
	/// validating the range for the specified context and converting units if required.
	/// </summary>
	/// <param name="temp">Temperature value to convert.</param>
	/// <param name="type">Context for validation: "set_heating", "delta", "current", or "hotwater".</param>
	/// <param name="units">Preferred unit system for conversion.</param>
	/// <returns>The Wiser hub temperature as an integer in tenths of a degree.</returns>
	public static int ToWiserTemp (double temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric)
		{
		temp = (int)(ValidateTemperature (temp, type) * 10);

		// Convert to metric if imperial units set
		if (units == WiserUnits.Imperial)
			{
			temp = ConvertFromF (temp);
			}

		return (int)temp;
		}

	/// <summary>
	/// Converts an object containing a hub temperature to a real-world temperature value.
	/// Supports int, long and double inputs; <see langword="null"/> yields 0.
	/// </summary>
	/// <param name="temp">Hub temperature value as int, long or double; <see langword="null"/> returns 0.</param>
	/// <param name="type">Context for validation: "set_heating", "delta", "current", or "hotwater".</param>
	/// <param name="units">Preferred unit system for conversion.</param>
	/// <returns>Temperature value as a double.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="temp"/> is not int, long or double.</exception>
	public static double FromWiserTemp (object? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric) =>
		temp switch
			{
				null or DBNull => 0,
				int intTemp => FromWiserTemp (intTemp, type, units),
				long longTemp => FromWiserTemp ((int)longTemp, type, units),
				double doubleTemp => FromWiserTemp ((int)Math.Round (doubleTemp * 10), type, units),
				_ => throw new ArgumentException ("Invalid temperature value type. Expected int or double."),
				};

	/// <summary>
	/// Converts a hub temperature integer (tenths of a degree) to a real-world temperature value.
	/// </summary>
	/// <param name="temp">Hub temperature in tenths of degrees (nullable).</param>
	/// <param name="type">Context for validation: "set_heating", "delta", "current", or "hotwater".</param>
	/// <param name="units">Preferred unit system for conversion.</param>
	/// <returns>Temperature value as a double.</returns>
	public static double FromWiserTemp (int? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric)
		{
		if (!temp.HasValue)
			return 0;
		var realTemp = temp >= TEMP_ERROR ? TEMP_MINIMUM : ValidateTemperature (Math.Round ((double)temp / 10, 1), type);

		// Convert to imperial if imperial units set
		if (units == WiserUnits.Imperial)
			{
			realTemp = ConvertToF (realTemp);
			}

		return realTemp;
		}

	private static double ValidateTemperature (double temp, string type = "set_heating") =>
		type switch
			{
				"hotwater" when temp is TEMP_HW_ON or TEMP_HW_OFF => temp,
				"delta" => temp > MAX_BOOST_INCREASE ? MAX_BOOST_INCREASE : temp,
				"current" => temp < TEMP_OFF ? TEMP_MINIMUM : temp,
				"set_heating" when temp >= TEMP_ERROR => TEMP_MINIMUM,
				"set_heating" when temp > TEMP_MAXIMUM => TEMP_MAXIMUM,
				"set_heating" when temp is < TEMP_MINIMUM and not TEMP_OFF => TEMP_MINIMUM,
				_ => temp
				};

	private static double ConvertFromF (double temp) => Math.Round ((temp - 32) * 5 / 9, 1);

	private static double ConvertToF (double temp) => Math.Round (temp * 9 / 5 + 32, 1);
	}

/// <summary>
/// Represents device battery information and derived percentage.
/// </summary>
/// <param name="data">Raw device dictionary from the hub payload for this device.</param>
/// <remarks>
/// Percent derivation depends on device type: RoomStat uses linear interpolation between
/// <see cref="Constants.ROOMSTAT_MIN_BATTERY_LEVEL"/> and <see cref="Constants.ROOMSTAT_FULL_BATTERY_LEVEL"/>,
/// while iTRV uses <see cref="Constants.TrvBatteryLevelMapping"/>.
/// </remarks>
public class WiserBattery (IDictionary<string, object> data)
	{
	/// <summary>Gets the reported battery level as a string, or "No Battery".</summary>
	/// <value>The hub-reported battery level string; "No Battery" when not applicable.</value>
	public string Level => data.TryGetValue ("BatteryLevel", out var level) ? level.ToString () : "No Battery";

	/// <summary>Gets the derived battery percentage for the device type.</summary>
	/// <value>
	/// A percentage in the range 0..100 derived from voltage and device type; 0 when unavailable or not applicable.
	/// </value>
	public int Percent
		{
		get
			{
			if (data.TryGetValue ("ProductType", out var productType) && Level != "No Battery")
				{
				if (productType.ToString () == "RoomStat")
					{
					return PercentageClip (
							 (int)Math.Round (
								  (Voltage - ROOMSTAT_MIN_BATTERY_LEVEL) /
								  (ROOMSTAT_FULL_BATTERY_LEVEL - ROOMSTAT_MIN_BATTERY_LEVEL) * 100
							 )
					);
					}
				else if (productType.ToString () == "iTRV" && Level != "No Battery")
					{
					return TrvBatteryLevelMapping.TryGetValue (Voltage, out var level) ? level : 0;
					}
				}

			return 0;
			}
		}

	/// <summary>Gets the battery voltage in volts.</summary>
	/// <value>The measured battery voltage (V); 0 when not available.</value>
	public double Voltage => data.TryGetValue ("BatteryVoltage", out var voltage) ? ConvertInvariant.ToDouble (voltage) / 10 : 0;

	private static int PercentageClip (int value) => Math.Min (100, Math.Max (0, value));
	}

/// <summary>
/// Represents radio signal strength and derived metrics.
/// </summary>
/// <param name="data">Raw device dictionary containing signal fields (ReceptionOfController/Device).</param>
/// <remarks>
/// Derived strengths map RSSI (dBm) to a 0..100 scale. <see langword="null"/> indicates unavailable values in the payload.
/// </remarks>
public class WiserSignalStrength (IDictionary<string, object> data)
	{
	/// <summary>Gets the hub-provided textual signal strength.</summary>
	public string DisplayedSignalStrength => data.TryGetValue ("DisplayedSignalStrength", out var strength) ? strength.ToString () : TEXT_UNKNOWN;

	/// <summary>Gets the LQI value for controller reception, if available.</summary>
	public int? ControllerReceptionLqi => data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict
				? receptionDict.TryGetValue ("Lqi", out var lqi) ? ConvertInvariant.ToInt32 (lqi) : null
				: null;

	/// <summary>Gets the RSSI value for controller reception, if available.</summary>
	public int? ControllerReceptionRssi => data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict
				? receptionDict.TryGetValue ("Rssi", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : null
				: null;

	/// <summary>Gets a 0..100 derived signal strength for messages to the controller.</summary>
	/// <value>A derived signal strength percentage (0..100); 0 when RSSI is unavailable.</value>
	public int ControllerSignalStrength => ControllerReceptionRssi.HasValue && ControllerReceptionRssi.Value != 0
			 ? Math.Min (100, (int)(2 * (ControllerReceptionRssi.Value + 100)))
			 : 0;

	/// <summary>Gets the LQI value for device reception, if available.</summary>
	public int? DeviceReceptionLqi => data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict
				? receptionDict.TryGetValue ("Lqi", out var lqi) ? ConvertInvariant.ToInt32 (lqi) : null
				: null;

	/// <summary>Gets the RSSI value for device reception, if available.</summary>
	public int? DeviceReceptionRssi => data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict
				? receptionDict.TryGetValue ("Rssi", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : null
				: null;

	/// <summary>Gets a 0..100 derived signal strength for messages to the device, or null if unknown.</summary>
	/// <value>
	/// A derived signal strength percentage (0..100) when RSSI is reported; 0 when reported RSSI is 0;
	/// otherwise <see langword="null"/> when unavailable.
	/// </value>
	public int? DeviceSignalStrength => DeviceReceptionRssi.HasValue
			 ? DeviceReceptionRssi.Value != 0 ? Math.Min (100, (int)(2 * (DeviceReceptionRssi.Value + 100))) : 0
			 : null;
	}

/// <summary>
/// Represents GPS coordinates used by the hub.
/// </summary>
/// <param name="data">Raw dictionary containing Latitude and Longitude values.</param>
public class WiserGPS (Dictionary<string, object> data)
	{
	/// <summary>Gets the latitude in decimal degrees, if provided by the hub.</summary>
	/// <value>Latitude in decimal degrees; <see langword="null"/> if not provided.</value>
	public double? Latitude => data.TryGetValue ("Latitude", out var latitude) ? ConvertInvariant.ToDouble (latitude) : (double?)null;

	/// <summary>Gets the longitude in decimal degrees, if provided by the hub.</summary>
	/// <value>Longitude in decimal degrees; <see langword="null"/> if not provided.</value>
	public double? Longitude => data.TryGetValue ("Longitude", out var longitude) ? ConvertInvariant.ToDouble (longitude) : (double?)null;
	}

/// <summary>
/// Cloud connection and platform configuration details.
/// </summary>
/// <param name="cloudStatus">The current cloud connection status string.</param>
/// <param name="data">Raw dictionary with cloud configuration fields returned by the hub.</param>
/// <remarks>
/// Boolean properties are computed from hub-provided values. <see cref="ConnectedToCloud"/> is derived
/// from <see cref="ConnectionStatus"/>.
/// </remarks>
public class WiserCloud (string cloudStatus, Dictionary<string, object> data)
	{
	/// <summary>Gets the Wiser API host name.</summary>
	/// <value>The Wiser API hostname reported by the hub; <c>TextUnknown</c> if unavailable.</value>
	public string ApiHost => data.TryGetValue ("WiserApiHost", out var host) ? host.ToString () : TEXT_UNKNOWN;

	/// <summary>Gets the bootstrap API host name.</summary>
	/// <value>The bootstrap API hostname reported by the hub; <c>TextUnknown</c> if unavailable.</value>
	public string BootstrapApiHost => data.TryGetValue ("BootStrapApiHost", out var host) ? host.ToString () : TEXT_UNKNOWN;

	/// <summary>Gets the current connection status string.</summary>
	/// <value>The cloud connection status reported by the hub (e.g., "Connected").</value>
	public string ConnectionStatus { get; } = cloudStatus;

	/// <summary>Gets a value indicating whether the hub is connected to the cloud platform.</summary>
	/// <value><see langword="true"/> when <see cref="ConnectionStatus"/> equals "Connected"; otherwise <see langword="false"/>.</value>
	public bool ConnectedToCloud => ConnectionStatus == "Connected";

	/// <summary>Gets a value indicating whether detailed publishing is enabled.</summary>
	/// <value><see langword="true"/> if the hub reports DetailedPublishing enabled; otherwise <see langword="false"/>.</value>
	public bool DetailedPublishingEnabled => data.TryGetValue ("DetailedPublishing", out var enabled) && ConvertInvariant.ToBoolean (enabled);

	/// <summary>Gets a value indicating whether diagnostic telemetry is enabled.</summary>
	/// <value><see langword="true"/> if the hub reports diagnostic telemetry enabled; otherwise <see langword="false"/>.</value>
	public bool DiagnosticTelemetryEnabled => data.TryGetValue ("EnableDiagnosticTelemetry", out var enabled) && ConvertInvariant.ToBoolean (enabled);
	}

/// <summary>
/// Represents a single firmware upgrade item available on the hub.
/// </summary>
/// <param name="data">Raw firmware item payload from the hub.</param>
public class WiserFirmwareUpgradeItem (Dictionary<string, object> data)
	{
	/// <summary>Gets the numeric identifier for the firmware item.</summary>
	public int Id => data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the firmware filename string.</summary>
	public string Filename => data.TryGetValue ("FirmwareFilename", out var filename) ? filename.ToString () : TEXT_UNKNOWN;
	}

/// <summary>Collection of available firmware upgrade items.</summary>
public class WiserFirmwareUpgradeInfo
	{
	private readonly List<Dictionary<string, object>> _data;

	/// <summary>Initializes the collection from a hub payload list.</summary>
	/// <param name="data">List of firmware item payloads.</param>
	public WiserFirmwareUpgradeInfo (List<Dictionary<string, object>> data)
		{
		_data = data;
		foreach (Dictionary<string, object> item in _data)
			{
			All.Add (new WiserFirmwareUpgradeItem (item));
			}
		}

	/// <summary>Gets all firmware items.</summary>
	public List<WiserFirmwareUpgradeItem> All { get; } = [];

	/// <summary>Finds an item by its identifier.</summary>
	/// <param name="id">Firmware item id.</param>
	/// <returns>The matching item, or <see langword="null"/>.</returns>
	public WiserFirmwareUpgradeItem GetById (int id) => All.FirstOrDefault (item => item.Id == id);
	}

/// <summary>
/// Represents hub capability flags reported by the Wiser controller.
/// </summary>
/// <param name="data">Raw capabilities dictionary from the hub.</param>
/// <remarks>Boolean properties default to <see langword="false"/> when a key is missing.</remarks>
public class WiserHubCapabilitiesInfo (Dictionary<string, object> data)
	{
	/// <summary>Gets all capability flags as a flat dictionary.</summary>
	public Dictionary<string, object> All => new (data);

	/// <summary>Gets a value indicating whether Smart Plug is supported.</summary>
	public bool SmartPlug => data.TryGetValue ("SmartPlug", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether TRVs (iTRV) are supported.</summary>
	public bool ITRV => data.TryGetValue ("ITRV", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether room stats are supported.</summary>
	public bool Roomstat => data.TryGetValue ("Roomstat", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether underfloor heating is supported.</summary>
	public bool UFH => data.TryGetValue ("UFH", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether UFH floor temperature sensor is supported.</summary>
	public bool UFHFloorTempSensor => data.TryGetValue ("UFHFloorTempSensor", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether UFH dew sensor is supported.</summary>
	public bool UFHDewSensor => data.TryGetValue ("UFHDewSensor", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether Heating Actuator is supported.</summary>
	public bool HACT => data.TryGetValue ("HACT", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether Lighting Actuator is supported.</summary>
	public bool LACT => data.TryGetValue ("LACT", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether Light devices are supported.</summary>
	public bool Light => data.TryGetValue ("Light", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether Shutter devices are supported.</summary>
	public bool Shutter => data.TryGetValue ("Shutter", out var value) && ConvertInvariant.ToBoolean (value);

	/// <summary>Gets a value indicating whether Load Controller is supported.</summary>
	public bool LoadController => data.TryGetValue ("LoadController", out var value) && ConvertInvariant.ToBoolean (value);
	}

/// <summary>
/// Aggregated Zigbee radio statistics from the hub.
/// </summary>
/// <param name="data">Raw Zigbee statistics dictionary from the hub.</param>
public class WiserZigbee (Dictionary<string, object> data)
	{
	/// <summary>Gets the Error72 reset count.</summary>
	/// <value>The number of “Error72” resets reported by the hub; 0 if unavailable.</value>
	public int Error72Reset => data.TryGetValue ("Error72Reset", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

	/// <summary>Gets the number of JPAN resets.</summary>
	/// <value>The number of JPAN (Join PAN) resets reported by the hub; 0 if unavailable.</value>
	public int JPANCount => data.TryGetValue ("JPANCount", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

	/// <summary>Gets the Zigbee network channel.</summary>
	/// <value>The active Zigbee network channel number; 0 if unavailable.</value>
	public int NetworkChannel => data.TryGetValue ("NetworkChannel", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

	/// <summary>Gets the no-signal reset count.</summary>
	/// <value>The number of resets due to no signal reported by the hub; 0 if unavailable.</value>
	public int NoSignalReset => data.TryGetValue ("NoSignalReset", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

	/// <summary>Gets the Zigbee module firmware version.</summary>
	/// <value>The module firmware version string; <c>TextUnknown</c> if unavailable.</value>
	public string ModuleVersion => data.TryGetValue ("ZigbeeModuleVersion", out var value) ? value.ToString () : TEXT_UNKNOWN;

	/// <summary>Gets the EUI of the Zigbee module.</summary>
	/// <value>The module EUI (extended unique identifier) string; <c>TextUnknown</c> if unavailable.</value>
	public string EUI => data.TryGetValue ("ZigbeeEUI", out var value) ? value.ToString () : TEXT_UNKNOWN;
	}

/// <summary>Utilities for formatting sunrise/sunset special time values.</summary>
public static class SpecialTimes
	{
	/// <summary>
	/// Formats a list of HHmm sunrise/sunset values into a dictionary keyed by day name, starting from today.
	/// </summary>
	/// <param name="sunTimes">List of HHmm integer times for up to 7 days.</param>
	/// <returns>Dictionary of day names to formatted times in "HH:MM" format.</returns>
	public static Dictionary<string, string> FormatOutput (List<int> sunTimes)
		{
		var output = new Dictionary<string, string> ();
		var today = (int)DateTime.Today.DayOfWeek;
		var days = Weekdays.Concat (Weekends).ToList ();

		for (var i = 0; i < 7; i++)
			{
			var index = (6 - today + i) % 7;
			if (index < sunTimes.Count)
				{
				output[days[i % 7]] = FormatTime (sunTimes[index]);
				}
			}

		return output;
		}

	private static string FormatTime (int time)
		{
		var timeStr = time.ToStringInvariant ().PadLeft (4, '0');
		return $"{timeStr[..2]}:{timeStr[2..]}";
		}

	/// <summary>Formats sunrise times for the week starting today.</summary>
	/// <param name="times">List of HHmm integer times.</param>
	/// <returns>Dictionary of day names to "HH:MM" strings.</returns>
	public static Dictionary<string, string> SunriseTimes (List<int> times) => FormatOutput (times);

	/// <summary>Formats sunset times for the week starting today.</summary>
	/// <param name="times">List of HHmm integer times.</param>
	/// <returns>Dictionary of day names to "HH:MM" strings.</returns>
	public static Dictionary<string, string> SunsetTimes (List<int> times) => FormatOutput (times);
	}

/// <summary>
/// Base type for level-based electrical devices (lights/shutters).
/// </summary>
/// <param name="wiserRestController">REST controller used to send commands.</param>
/// <param name="data">Raw device data payload.</param>
/// <param name="deviceTypeData">Device-type specific payload for this device.</param>
/// <remarks>Provides a default <see cref="WiserDevice.Model"/> mapping for level-capable devices.</remarks>
public class WiserElectricalLevelDevice (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
	{

	// Lights and shutters currently have model identifier as Unknown
	/// <inheritdoc />
	public override string Model => Data.TryGetValue ("ProductType", out var type) ? type.ToString () : TEXT_UNKNOWN;
	}

/// <summary>
/// Represents the next scheduled change for a schedule.
/// </summary>
/// <param name="scheduleType">High-level schedule type (e.g., Heating, OnOff, Level).</param>
/// <param name="data">Raw next-event payload from the hub.</param>
/// <remarks>
/// <see cref="Time"/> is parsed from an HHmm integer; <see cref="DateTime"/> is computed relative to today.
/// <see cref="Setting"/> maps to DegreesC/State/Level depending on <paramref name="scheduleType"/>.
/// </remarks>
public class WiserScheduleNext (string scheduleType, Dictionary<string, object> data)
	{
	/// <summary>Gets the day name for the next scheduled event.</summary>
	public string Day => data.TryGetValue ("Day", out var day) ? day.ToString () : "";

	/// <summary>Gets the time of the next event as a TimeSpan.</summary>
	public TimeSpan Time
		{
		get
			{
			var timeValue = data.TryGetValue ("Time", out var time) ? ConvertInvariant.ToInt32 (time) : 0;
			var timeStr = timeValue.ToStringInvariant ().PadLeft (4, '0');
			return TimeSpan.ParseExact ($"{timeStr[..2]}:{timeStr[2..]}", "hh\\:mm", null);
			}
		}

	/// <summary>Gets the DateTime representing the next event.</summary>
	/// <value>
	/// The computed local date and time of the next scheduled change; returns
	/// <see cref="DateTime.MinValue"/> if the value cannot be determined.
	/// </value>
	/// <remarks>
	/// Computed from the next event's day and time relative to today.
	/// </remarks>
	public DateTime DateTime
		{
		get
			{
			try
				{
				var allDays = Weekdays.Concat (Weekends).ToList ();
				var nextScheduleDay = (allDays.IndexOf (Day) + 1) % 7;
				TimeSpan nextScheduleTime = Time;
				var currentDay = (int)DateTime.Today.DayOfWeek;
				TimeSpan currentTime = DateTime.Now.TimeOfDay;

				// If next day or time on earlier weekday, add week to date
				var daysDiff = nextScheduleDay - currentDay;
				daysDiff = daysDiff > 0 || (daysDiff == 0 && nextScheduleTime >= currentTime) ? daysDiff : daysDiff + 7;
				DateTime nextDate = DateTime.Today.AddDays (daysDiff);
				return nextDate.Date + nextScheduleTime;
				}
			catch
				{
				return DateTime.MinValue;
				}
			}
		}

	/// <summary>Gets the next setting (temperature, on/off state, or level) for the schedule.</summary>
	public object? Setting =>
		scheduleType switch
			{
				var t when t == TEXT_HEATING =>
					 WiserTemperatureFunctions.FromWiserTemp (data.TryGetValue ("DegreesC", out var temp) ? temp : 0),
				var t when t == TEXT_ON_OFF =>
					 data.TryGetValue ("State", out var state) ? state : null,
				var t when t == TEXT_LEVEL =>
					 data.TryGetValue ("Level", out var level) ? level : null,
				_ => null
				};
	}

/// <summary>Discovery helpers to locate Wiser hubs on the local network.</summary>
public class WiserDiscovery
	{
	/// <summary>
	/// Discover hubs within a maximum search time.
	/// </summary>
	/// <param name="maxSearchTime">Maximum scan time in seconds.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of discovered hubs.</returns>
	/// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
	public static async Task<List<WiserDiscoveredHub>> DiscoverHubAsync (int maxSearchTime = 30, CancellationToken cancellationToken = default)
		{
		WiserDiscoveryOptions options = new ()
			{
			ShowDebug = false,
			ShowProgress = true,
			MaxConcurrency = 10,
			TimeoutSeconds = maxSearchTime,
			HttpTimeout = 5000,
			PingTimeout = 1000   // Also increase ping timeout slightly
			};
		ConcurrentBag<WiserDiscoveredHub> hubs = await WiserHubDiscovery.DiscoverHubsAsync (options, cancellationToken).ConfigureAwait (false);
		return [.. hubs];
		}

	/// <summary>
	/// Discover hubs with a maximum result count.
	/// </summary>
	/// <param name="maxSearchTime">Maximum scan time in seconds.</param>
	/// <param name="maxResults">Maximum number of hubs to return (0 for unlimited).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of discovered hubs.</returns>
	/// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
	public static async Task<List<WiserDiscoveredHub>> DiscoverHubAsync (int maxSearchTime = 30, int maxResults = 0, CancellationToken cancellationToken = default)
		{
		WiserDiscoveryOptions options = new ()
			{
			ShowDebug = false,
			ShowProgress = true,
			MaxConcurrency = 10,
			TimeoutSeconds = maxSearchTime,
			HttpTimeout = 5000,
			PingTimeout = 1000,
			MaxResults = maxResults
			};
		ConcurrentBag<WiserDiscoveredHub> hubs = await WiserHubDiscovery.DiscoverHubsAsync (options, cancellationToken).ConfigureAwait (false);
		return [.. hubs];
		}
	}

/// <summary>Time formatting helpers for hub HHmm values and TimeSpan.</summary>
public static class WiserTimeExtensions
	{
	/// <summary>
	/// Formats a numeric HHmm value into an "HH:MM" string.
	/// </summary>
	/// <param name="time">Time in HHmm (0..2359).</param>
	/// <returns>Time formatted as "HH:MM".</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not between 0 and 2359.</exception>
	public static string ToWiserTime (this long time)
		{
		if (time is < 0 or > 2359)
			throw new ArgumentOutOfRangeException (nameof (time), "Time must be between 0 and 2359.");
		var timeStr = time.ToStringInvariant ().PadLeft (4, '0');
		return $"{timeStr[..2]}:{timeStr[2..]}";
		}

	/// <summary>
	/// Parses an "HH:MM" string into a TimeSpan value.
	/// </summary>
	/// <param name="timeStr">String in "HH:MM" format.</param>
	/// <returns>Parsed TimeSpan.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="timeStr"/> is <see langword="null"/>/whitespace or not in "HH:MM" format.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the parsed hours/minutes are outside valid ranges.</exception>
	public static TimeSpan FromWiserTime (this string timeStr)
		{
		if (string.IsNullOrWhiteSpace (timeStr) || timeStr.Length != 5)
			throw new ArgumentException ("Invalid time format. Expected 'HH:MM' format.");
		var hours = timeStr[..2].ParseIntInvariant ();
		var minutes = timeStr[3..].ParseIntInvariant ();
		return new TimeSpan (hours, minutes, 0);
		}

	/// <summary>
	/// Converts supported inputs (long HHmm, TimeSpan, or string) into an "HH:MM" string.
	/// </summary>
	/// <param name="timeObj">Input value: long, TimeSpan, or string.</param>
	/// <returns>Time formatted as "HH:MM".</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="timeObj"/> is not a supported type or is not in a valid format.
	/// </exception>
	public static string ToWiserTime (this object timeObj)
		{
		if (timeObj is long timeLong)
			{
			return ToWiserTime (timeLong);
			}

		if (timeObj is TimeSpan timeSpan)
			{
			return ToWiserTime (timeSpan);
			}

		if (timeObj is string timeStr)
			{
			if (timeStr.Length == 5 && timeStr[2] == ':') // Check for 'HH:MM' format
				{
				return timeStr;
				}
			else if (timeStr.Length == 4 && int.TryParse (timeStr, out var timeInt) && timeInt >= 0 && timeInt <= 2359)
				{
				return ToWiserTime (timeInt);
				}
			}

		throw new ArgumentException ("Invalid time object type. Expected long, TimeSpan, or string in 'HH:MM' format.");
		}
	}

/// <summary>String helpers for casing and invariant formatting.</summary>
public static class StringExtensions
	{
	/// <summary>
	/// Converts a string to title case using the current culture.
	/// </summary>
	/// <param name="str">Input string.</param>
	/// <returns>Title-cased string.</returns>
	public static string TitleCase (this string str) =>
		string.IsNullOrWhiteSpace (str) ? str : CultureInfo.CurrentCulture.TextInfo.ToTitleCase (str.ToLowerInvariant ());

	/// <summary>
	/// Capitalizes the first character and lower-cases the remainder using invariant culture.
	/// </summary>
	/// <param name="str">Input string.</param>
	/// <returns>Capitalized string.</returns>
	public static string Capitalize (this string str) =>
		string.IsNullOrWhiteSpace (str) ? str : $"{char.ToUpper (str[0], CultureInfo.InvariantCulture)}{str[1..].ToLower (CultureInfo.InvariantCulture)}";

	/// <summary>
	/// Formats a composite string using CultureInfo.InvariantCulture.
	/// </summary>
	/// <param name="str">Composite format string.</param>
	/// <param name="args">Format arguments.</param>
	/// <returns>Formatted string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="str"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">Thrown when <paramref name="str"/> is not a valid composite format string.</exception>
	public static string FormatInvariant (this string str, params object[] args) =>
		string.Format (CultureInfo.InvariantCulture, str, args);
	}

/// <summary>Numeric and DateTime helpers using CultureInfo.InvariantCulture.</summary>
public static class NumericExtensions
	{
	/// <summary>
	/// Converts an integer to a string using invariant culture.
	/// </summary>
	/// <param name="value">The integer value to convert.</param>
	/// <returns>The string representation of <paramref name="value"/> using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this int value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts an integer to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The integer value to convert.</param>
	/// <param name="fmt">A standard or custom numeric format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this int value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a double to a string using invariant culture.
	/// </summary>
	/// <param name="value">The double value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this double value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a double to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The double value to convert.</param>
	/// <param name="fmt">A standard or custom numeric format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this double value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a float to a string using invariant culture.
	/// </summary>
	/// <param name="value">The float value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this float value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a float to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The float value to convert.</param>
	/// <param name="fmt">A standard or custom numeric format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this float value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a long to a string using invariant culture.
	/// </summary>
	/// <param name="value">The long value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this long value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a long to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The long value to convert.</param>
	/// <param name="fmt">A standard or custom numeric format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this long value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a decimal to a string using invariant culture.
	/// </summary>
	/// <param name="value">The decimal value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this decimal value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a decimal to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The decimal value to convert.</param>
	/// <param name="fmt">A standard or custom numeric format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this decimal value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a bool to a string using invariant culture.
	/// </summary>
	/// <param name="value">The Boolean value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this bool value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a DateTime to a string using invariant culture.
	/// </summary>
	/// <param name="value">The DateTime value to convert.</param>
	/// <returns>The string representation using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this DateTime value) => value.ToString (CultureInfo.InvariantCulture);

	/// <summary>
	/// Converts a DateTime to a formatted string using invariant culture.
	/// </summary>
	/// <param name="value">The DateTime value to convert.</param>
	/// <param name="fmt">A standard or custom date and time format string.</param>
	/// <returns>The formatted string using CultureInfo.InvariantCulture.</returns>
	public static string ToStringInvariant (this DateTime value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses an integer using invariant culture.
	/// </summary>
	/// <param name="str">The string to parse.</param>
	/// <returns>The parsed 32-bit integer.</returns>
	/// <exception cref="FormatException">The input string is not in a valid format.</exception>
	/// <exception cref="OverflowException">The number represented is less than Int32.MinValue or greater than Int32.MaxValue.</exception>
	public static int ParseIntInvariant (this string str) => int.Parse (str, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses a long using invariant culture.
	/// </summary>
	/// <param name="str">The string to parse.</param>
	/// <returns>The parsed 64-bit integer.</returns>
	/// <exception cref="FormatException">The input string is not in a valid format.</exception>
	/// <exception cref="OverflowException">The number represented is less than Int64.MinValue or greater than Int64.MaxValue.</exception>
	public static long ParseLongInvariant (this string str) => long.Parse (str, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses a double using invariant culture.
	/// </summary>
	/// <param name="str">The string to parse.</param>
	/// <returns>The parsed double-precision floating-point number.</returns>
	/// <exception cref="FormatException">The input string is not in a valid format.</exception>
	/// <exception cref="OverflowException">The number is too large or too small for a Double.</exception>
	public static double ParseDoubleInvariant (this string str) => double.Parse (str, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses a float using invariant culture.
	/// </summary>
	/// <param name="str">The string to parse.</param>
	/// <returns>The parsed single-precision floating-point number.</returns>
	/// <exception cref="FormatException">The input string is not in a valid format.</exception>
	/// <exception cref="OverflowException">The number is too large or too small for a Single.</exception>
	public static float ParseFloatInvariant (this string str) => float.Parse (str, CultureInfo.InvariantCulture);

	/// <summary>
	/// Parses a decimal using invariant culture.
	/// </summary>
	/// <param name="str">The string to parse.</param>
	/// <returns>The parsed decimal number.</returns>
	/// <exception cref="FormatException">The input string is not in a valid format.</exception>
	/// <exception cref="OverflowException">The number is less than Decimal.MinValue or greater than Decimal.MaxValue.</exception>
	public static decimal ParseDecimalInvariant (this string str) => decimal.Parse (str, CultureInfo.InvariantCulture);
	}

/// <summary>Convert helpers using CultureInfo.InvariantCulture.</summary>
public static class ConvertInvariant
	{
	/// <summary>Converts an object to Int32 using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields 0).</param>
	/// <returns>The 32-bit signed integer equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	/// <exception cref="OverflowException">The value represents a number outside the range of Int32.</exception>
	public static int ToInt32 (object? obj) => Convert.ToInt32 (obj, CultureInfo.InvariantCulture);

	/// <summary>Converts an object to Int64 using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields 0).</param>
	/// <returns>The 64-bit signed integer equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	/// <exception cref="OverflowException">The value represents a number outside the range of Int64.</exception>
	public static long ToInt64 (object? obj) => Convert.ToInt64 (obj, CultureInfo.InvariantCulture);

	/// <summary>Converts an object to Double using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields 0.0).</param>
	/// <returns>The double-precision floating-point equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	/// <exception cref="OverflowException">The value represents a number outside the range of Double.</exception>
	public static double ToDouble (object? obj) => Convert.ToDouble (obj, CultureInfo.InvariantCulture);

	/// <summary>Converts an object to Single using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields 0.0F).</param>
	/// <returns>The single-precision floating-point equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	/// <exception cref="OverflowException">The value represents a number outside the range of Single.</exception>
	public static float ToSingle (object? obj) => Convert.ToSingle (obj, CultureInfo.InvariantCulture);

	/// <summary>Converts an object to Decimal using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields 0M).</param>
	/// <returns>The decimal number equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	/// <exception cref="OverflowException">The value represents a number outside the range of Decimal.</exception>
	public static decimal ToDecimal (object? obj) => Convert.ToDecimal (obj, CultureInfo.InvariantCulture);

	/// <summary>Converts an object to Boolean using invariant culture.</summary>
	/// <param name="obj">The value to convert (<see langword="null"/> yields <see langword="false"/>).</param>
	/// <returns>The Boolean equivalent of <paramref name="obj"/>.</returns>
	/// <exception cref="InvalidCastException">The value does not implement IConvertible.</exception>
	/// <exception cref="FormatException">The value's format is invalid for conversion.</exception>
	public static bool ToBoolean (object? obj) => Convert.ToBoolean (obj, CultureInfo.InvariantCulture);
	}

/// <summary>Invariant-culture log4net helpers to avoid implicit culture-sensitive formatting.</summary>
public static class LoggerExtensions
	{
	/// <summary>Writes a debug message formatted with CultureInfo.InvariantCulture.</summary>
	/// <param name="logger">Logger instance (nullable).</param>
	/// <param name="message">Composite format string.</param>
	/// <param name="args">Format arguments (nullable elements permitted).</param>
	public static void DebugFormatInvariant (this ILog logger, string message, params object?[] args) =>
		logger?.DebugFormat (CultureInfo.InvariantCulture, message, args);

	/// <summary>Writes an info message formatted with CultureInfo.InvariantCulture.</summary>
	/// <param name="logger">Logger instance (nullable).</param>
	/// <param name="message">Composite format string.</param>
	/// <param name="args">Format arguments (nullable elements permitted).</param>
	public static void InfoFormatInvariant (this ILog logger, string message, params object?[] args) =>
		logger?.InfoFormat (CultureInfo.InvariantCulture, message, args);
	}

// -----

