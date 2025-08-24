// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
using System.Collections.Concurrent;
using System.Globalization;

using System.Threading;
using System.Threading.Tasks;

using log4net;

namespace WiserHeatApiV2
	{
	public static class Constants
		{
		// Temperature Constants
		public const double DefaultAwayModeTemp = 10.5;
		public const double DefaultDegradedTemp = 18;
		public const int MaxBoostIncrease = 5;
		public const int TempError = 2000;
		public const int TempMinimum = 5;
		public const int TempMaximum = 30;
		public const int TempHwOn = 110;
		public const int TempHwOff = -20;
		public const int TempOff = -20;

		// Battery Constants
		public const double RoomstatMinBatteryLevel = 1.7;
		public const double RoomstatFullBatteryLevel = 2.7;
		public const double TrvFullBatteryLevel = 3.0;
		public const double TrvMinBatteryLevel = 2.4;

		// Text Values
		public const string TextAuto = "Auto";
		public const string TextClose = "Close";
		public const string TextDegreesC = "DegreesC";
		public const string TextHeating = "Heating";
		public const string TextLevel = "Level";
#if LIGHT
		public const string TextLighting = "Lighting";
#endif
		public const string TextManual = "Manual";
		public const string TextNoChange = "NoChange";
		public const string TextNone = "None";
		public const string TextOff = "Off";
		public const string TextOn = "On";
		public const string TextOnOff = "OnOff";
		public const string TextOpen = "Open";
		public const string TextSetpoint = "Setpoint";
#if SHUTTER
		public const string TextShutters = "Shutters";
#endif
		public const string TextState = "State";
		public const string TextTemp = "Temp";
		public const string TextTime = "Time";
		public const string TextUnknown = "Unknown";
		public const string TextWeekdays = "Weekdays";
		public const string TextWeekends = "Weekends";

		// Day Value Lists
		public static readonly List<string> Weekdays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
		public static readonly List<string> Weekends = ["Saturday", "Sunday"];
		public static readonly List<string> SpecialDays = [TextWeekdays, TextWeekends];
		public static readonly Dictionary<string, int> SpecialTimes = new () { { "Sunrise", 3000 }, { "Sunset", 4000 } };

		// Battery Level Enum
		public static readonly Dictionary<double, int> TrvBatteryLevelMapping = new ()
			{
				{ 3.0, 100 }, { 2.9, 80 }, { 2.8, 60 }, { 2.7, 40 }, { 2.6, 30 }, { 2.5, 20 }, { 2.4, 10 }, { 2.3, 0 }
		  };

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

	public enum WiserUnits
		{
		Imperial,
		Metric
		}

	public enum WiserScheduleType
		{
		Heating,
		OnOff,
		Level,
		Lighting,
		Shutters
		}

	public enum WiserHeatingMode
		{
		Off,
		Auto,
		Manual
		}

	public enum WiserHotWaterMode
		{
		Auto,
		Manual
		}

	public enum WiserSmartPlugMode
		{
		Auto,
		Manual
		}

#if SHUTTER
	public enum WiserShutterMode
		{
		Auto,
		Manual
		}
#endif
#if LIGHT
	public enum WiserLightMode
		{
		Auto,
		Manual
		}
#endif

	public enum WiserAwayAction
		{
		Off,
		NoChange,
		Close
		}

	// Exception classes

	public class WiserHubNotImplementedException (string message) : Exception (message)
		{
		}

	// Helper classes
	public static class WiserTemperatureFunctions
		{
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

		public static double FromWiserTemp (object? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric) =>
			temp switch
				{
					null or DBNull => 0,
					int intTemp => FromWiserTemp (intTemp, type, units),
					long longTemp => FromWiserTemp ((int)longTemp, type, units),
					double doubleTemp => FromWiserTemp ((int)Math.Round (doubleTemp * 10), type, units),
					_ => throw new ArgumentException ("Invalid temperature value type. Expected int or double."),
				};

		public static double FromWiserTemp (int? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric)
			{
			if (!temp.HasValue)
				return 0;
			var realTemp = temp >= Constants.TempError ? Constants.TempMinimum : ValidateTemperature (Math.Round ((double)temp / 10, 1), type);

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
					"hotwater" when temp is Constants.TempHwOn or Constants.TempHwOff => temp,
					"delta" => temp > Constants.MaxBoostIncrease ? Constants.MaxBoostIncrease : temp,
					"current" => temp < Constants.TempOff ? Constants.TempMinimum : temp,
					"set_heating" when temp >= Constants.TempError => Constants.TempMinimum,
					"set_heating" when temp > Constants.TempMaximum => Constants.TempMaximum,
					"set_heating" when temp is < Constants.TempMinimum and not Constants.TempOff => Constants.TempMinimum,
					_ => temp
					};

		private static double ConvertFromF (double temp) => Math.Round ((temp - 32) * 5 / 9, 1);

		private static double ConvertToF (double temp) => Math.Round (temp * 9 / 5 + 32, 1);
		}

	public class WiserBattery (IDictionary<string, object> data)
		{
		public string Level => data.TryGetValue ("BatteryLevel", out var level) ? level.ToString () : "No Battery";

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
								  (Voltage - Constants.RoomstatMinBatteryLevel) /
								  (Constants.RoomstatFullBatteryLevel - Constants.RoomstatMinBatteryLevel) * 100
							 )
						);
						}
					else if (productType.ToString () == "iTRV" && Level != "No Battery")
						{
						return Constants.TrvBatteryLevelMapping.TryGetValue (Voltage, out var level) ? level : 0;
						}
					}

				return 0;
				}
			}

		public double Voltage => data.TryGetValue ("BatteryVoltage", out var voltage) ? ConvertInvariant.ToDouble (voltage) / 10 : 0;

		private static int PercentageClip (int value) => Math.Min (100, Math.Max (0, value));
		}

	public class WiserSignalStrength (IDictionary<string, object> data)
		{
		public string DisplayedSignalStrength => data.TryGetValue ("DisplayedSignalStrength", out var strength) ? strength.ToString () : Constants.TextUnknown;

		public int? ControllerReceptionLqi => data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict
					? receptionDict.TryGetValue ("Lqi", out var lqi) ? ConvertInvariant.ToInt32 (lqi) : (int?)null
					: null;

		public int? ControllerReceptionRssi => data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict
					? receptionDict.TryGetValue ("Rssi", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : (int?)null
					: null;

		public int ControllerSignalStrength => ControllerReceptionRssi.HasValue && ControllerReceptionRssi.Value != 0
					? Math.Min (100, (int)(2 * (ControllerReceptionRssi.Value + 100)))
					: 0;

		public int? DeviceReceptionLqi => data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict
					? receptionDict.TryGetValue ("Lqi", out var lqi) ? ConvertInvariant.ToInt32 (lqi) : (int?)null
					: null;

		public int? DeviceReceptionRssi => data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict
					? receptionDict.TryGetValue ("Rssi", out var rssi) ? ConvertInvariant.ToInt32 (rssi) : (int?)null
					: null;

		public int? DeviceSignalStrength => DeviceReceptionRssi.HasValue
					? DeviceReceptionRssi.Value != 0 ? Math.Min (100, (int)(2 * (DeviceReceptionRssi.Value + 100))) : 0
					: null;
		}

	public class WiserGPS (Dictionary<string, object> data)
		{
		public double? Latitude => data.TryGetValue ("Latitude", out var latitude) ? ConvertInvariant.ToDouble (latitude) : (double?)null;

		public double? Longitude => data.TryGetValue ("Longitude", out var longitude) ? ConvertInvariant.ToDouble (longitude) : (double?)null;
		}

	public class WiserCloud (string cloudStatus, Dictionary<string, object> data)
		{
		public string ApiHost => data.TryGetValue ("WiserApiHost", out var host) ? host.ToString () : Constants.TextUnknown;

		public string BootstrapApiHost => data.TryGetValue ("BootStrapApiHost", out var host) ? host.ToString () : Constants.TextUnknown;

		public bool ConnectedToCloud => ConnectionStatus == "Connected";

		public string ConnectionStatus { get; } = cloudStatus;

		public bool DetailedPublishingEnabled => data.TryGetValue ("DetailedPublishing", out var enabled) && ConvertInvariant.ToBoolean (enabled);

		public bool DiagnosticTelemetryEnabled => data.TryGetValue ("EnableDiagnosticTelemetry", out var enabled) && ConvertInvariant.ToBoolean (enabled);
		}

	public class WiserFirmwareUpgradeItem (Dictionary<string, object> data)
		{
		public int Id => data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

		public string Filename => data.TryGetValue ("FirmwareFilename", out var filename) ? filename.ToString () : Constants.TextUnknown;
		}

	public class WiserFirmwareUpgradeInfo
		{
		private readonly List<Dictionary<string, object>> _data;

		public WiserFirmwareUpgradeInfo (List<Dictionary<string, object>> data)
			{
			_data = data;
			foreach (Dictionary<string, object> item in _data)
				{
				All.Add (new WiserFirmwareUpgradeItem (item));
				}
			}

		public List<WiserFirmwareUpgradeItem> All { get; } = [];

		public WiserFirmwareUpgradeItem GetById (int id) => All.FirstOrDefault (item => item.Id == id);
		}

	public class WiserHubCapabilitiesInfo (Dictionary<string, object> data)
		{
		public Dictionary<string, object> All => new (data);

		public bool SmartPlug => data.TryGetValue ("SmartPlug", out var value) && ConvertInvariant.ToBoolean (value);

		public bool ITRV => data.TryGetValue ("ITRV", out var value) && ConvertInvariant.ToBoolean (value);

		public bool Roomstat => data.TryGetValue ("Roomstat", out var value) && ConvertInvariant.ToBoolean (value);

		public bool UFH => data.TryGetValue ("UFH", out var value) && ConvertInvariant.ToBoolean (value);

		public bool UFHFloorTempSensor => data.TryGetValue ("UFHFloorTempSensor", out var value) && ConvertInvariant.ToBoolean (value);

		public bool UFHDewSensor => data.TryGetValue ("UFHDewSensor", out var value) && ConvertInvariant.ToBoolean (value);

		public bool HACT => data.TryGetValue ("HACT", out var value) && ConvertInvariant.ToBoolean (value);

		public bool LACT => data.TryGetValue ("LACT", out var value) && ConvertInvariant.ToBoolean (value);

		public bool Light => data.TryGetValue ("Light", out var value) && ConvertInvariant.ToBoolean (value);

		public bool Shutter => data.TryGetValue ("Shutter", out var value) && ConvertInvariant.ToBoolean (value);

		public bool LoadController => data.TryGetValue ("LoadController", out var value) && ConvertInvariant.ToBoolean (value);
		}

	public class WiserZigbee (Dictionary<string, object> data)
		{
		public int Error72Reset => data.TryGetValue ("Error72Reset", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

		public int JPANCount => data.TryGetValue ("JPANCount", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

		public int NetworkChannel => data.TryGetValue ("NetworkChannel", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

		public int NoSignalReset => data.TryGetValue ("NoSignalReset", out var value) ? ConvertInvariant.ToInt32 (value) : 0;

		public string ModuleVersion => data.TryGetValue ("ZigbeeModuleVersion", out var value) ? value.ToString () : Constants.TextUnknown;

		public string EUI => data.TryGetValue ("ZigbeeEUI", out var value) ? value.ToString () : Constants.TextUnknown;
		}

	public static class SpecialTimes
		{
		public static Dictionary<string, string> FormatOutput (List<int> sunTimes)
			{
			var output = new Dictionary<string, string> ();
			var today = (int)DateTime.Today.DayOfWeek;
			var days = Constants.Weekdays.Concat (Constants.Weekends).ToList ();

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

		public static Dictionary<string, string> SunriseTimes (List<int> times) => FormatOutput (times);

		public static Dictionary<string, string> SunsetTimes (List<int> times) => FormatOutput (times);
		}

	public class WiserElectricalLevelDevice (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
		{

		// Lights and shutters currently have model identifier as Unknown
		public override string Model => Data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TextUnknown;
		}

	public class WiserScheduleNext (string scheduleType, Dictionary<string, object> data)
		{
		public string Day => data.TryGetValue ("Day", out var day) ? day.ToString () : "";

		public TimeSpan Time
			{
			get
				{
				var timeValue = data.TryGetValue ("Time", out var time) ? ConvertInvariant.ToInt32 (time) : 0;
				var timeStr = timeValue.ToStringInvariant ().PadLeft (4, '0');
				return TimeSpan.ParseExact ($"{timeStr[..2]}:{timeStr[2..]}", "hh\\:mm", null);
				}
			}

		public DateTime DateTime
			{
			get
				{
				try
					{
					var allDays = Constants.Weekdays.Concat (Constants.Weekends).ToList ();
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

		public object? Setting =>
			scheduleType switch
				{
					var t when t == Constants.TextHeating =>
						 WiserTemperatureFunctions.FromWiserTemp (data.TryGetValue ("DegreesC", out var temp) ? temp : 0),
					var t when t == Constants.TextOnOff =>
						 data.TryGetValue ("State", out var state) ? state : null,
					var t when t == Constants.TextLevel =>
						 data.TryGetValue ("Level", out var level) ? level : null,
					_ => null
					};
		}

	public class WiserDiscovery
		{
		private readonly List<WiserDiscoveredHub> _discoveredHubs = [];

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

	public static class WiserTimeExtensions
		{
		public static string ToWiserTime (this long time)
			{
			if (time is < 0 or > 2359)
				throw new ArgumentOutOfRangeException (nameof (time), "Time must be between 0 and 2359.");
			var timeStr = time.ToStringInvariant ().PadLeft (4, '0');
			return $"{timeStr[..2]}:{timeStr[2..]}";
			}
		public static TimeSpan FromWiserTime (this string timeStr)
			{
			if (string.IsNullOrWhiteSpace (timeStr) || timeStr.Length != 5)
				throw new ArgumentException ("Invalid time format. Expected 'HH:MM' format.");
			var hours = timeStr[..2].ParseIntInvariant ();
			var minutes = timeStr[3..].ParseIntInvariant ();
			return new TimeSpan (hours, minutes, 0);
			}

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

	public static class StringExtensions
		{
		public static string TitleCase (this string str) =>
			string.IsNullOrWhiteSpace (str) ? str : CultureInfo.CurrentCulture.TextInfo.ToTitleCase (str.ToLowerInvariant ());

		public static string Capitalize (this string str) =>
			string.IsNullOrWhiteSpace (str) ? str : $"{char.ToUpper (str[0], CultureInfo.InvariantCulture)}{str[1..].ToLower (CultureInfo.InvariantCulture)}";

		public static string FormatInvariant (this string str, params object[] args) =>
			string.Format (CultureInfo.InvariantCulture, str, args);
		}

	public static class NumericExtensions
		{
		public static string ToStringInvariant (this int value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this int value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this double value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this double value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this float value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this float value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this long value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this long value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this decimal value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this decimal value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this bool value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this DateTime value) => value.ToString (CultureInfo.InvariantCulture);
		public static string ToStringInvariant (this DateTime value, string fmt) => value.ToString (fmt, CultureInfo.InvariantCulture);

		public static int ParseIntInvariant (this string str) => int.Parse (str, CultureInfo.InvariantCulture);
		public static long ParseLongInvariant (this string str) => long.Parse (str, CultureInfo.InvariantCulture);
		public static double ParseDoubleInvariant (this string str) => double.Parse (str, CultureInfo.InvariantCulture);
		public static float ParseFloatInvariant (this string str) => float.Parse (str, CultureInfo.InvariantCulture);
		public static decimal ParseDecimalInvariant (this string str) => decimal.Parse (str, CultureInfo.InvariantCulture);
		}

	public static class ConvertInvariant
		{
		public static int ToInt32 (object? obj) => Convert.ToInt32 (obj, CultureInfo.InvariantCulture);
		public static long ToInt64 (object? obj) => Convert.ToInt64 (obj, CultureInfo.InvariantCulture);
		public static double ToDouble (object? obj) => Convert.ToDouble (obj, CultureInfo.InvariantCulture);
		public static float ToSingle (object? obj) => Convert.ToSingle (obj, CultureInfo.InvariantCulture);
		public static decimal ToDecimal (object? obj) => Convert.ToDecimal (obj, CultureInfo.InvariantCulture);
		public static bool ToBoolean (object? obj) => Convert.ToBoolean (obj, CultureInfo.InvariantCulture);
		}

	public static class LoggerExtensions
		{
		public static void DebugFormatInvariant (this ILog logger, string message, params object?[] args) => 
			logger?.DebugFormat (CultureInfo.InvariantCulture, message, args);
		public static void InfoFormatInvariant (this ILog logger, string message, params object?[] args) => 
			logger?.InfoFormat (CultureInfo.InvariantCulture, message, args);
		}
	}

// -----

