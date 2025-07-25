// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
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
		public static readonly List<string> Weekdays = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
		public static readonly List<string> Weekends = new List<string> { "Saturday", "Sunday" };
		public static readonly List<string> SpecialDays = new List<string> { TextWeekdays, TextWeekends };
		public static readonly Dictionary<string, int> SpecialTimes = new Dictionary<string, int> { { "Sunrise", 3000 }, { "Sunset", 4000 } };

		// Battery Level Enum
		public static readonly Dictionary<double, int> TrvBatteryLevelMapping = new Dictionary<double, int>
		  {
				{ 3.0, 100 }, { 2.9, 80 }, { 2.8, 60 }, { 2.7, 40 }, { 2.6, 30 }, { 2.5, 20 }, { 2.4, 10 }, { 2.3, 0 }
		  };

		public static readonly Dictionary<string, Dictionary<string, object>> DefaultLevelSchedule = new Dictionary<string, Dictionary<string, object>>
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

	public class WiserHubNotImplementedException : Exception
		{
		public WiserHubNotImplementedException (string message) : base (message) { }
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

		public static double FromWiserTemp (object? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric)
			{
			if (temp == null || temp is DBNull)
				return 0;
			if (temp is int intTemp)
				return FromWiserTemp (intTemp, type, units);
			if (temp is long longTemp)
				return FromWiserTemp ((int)longTemp, type, units);
			if (temp is double doubleTemp)
				return FromWiserTemp ((int)Math.Round (doubleTemp * 10), type, units);

			throw new ArgumentException ("Invalid temperature value type. Expected int or double.");
			}

		public static double FromWiserTemp (int? temp, string type = "set_heating", WiserUnits units = WiserUnits.Metric)
			{
			if (!temp.HasValue)
				return 0;
			double realTemp;

			if (temp >= Constants.TempError)  // Fix high value from hub when lost sight of iTRV
				{
				realTemp = Constants.TempMinimum;
				}
			else
				{
				realTemp = ValidateTemperature (Math.Round ((double)temp / 10, 1), type);
				}

			// Convert to imperial if imperial units set
			if (units == WiserUnits.Imperial)
				{
				realTemp = ConvertToF (realTemp);
				}

			return realTemp;
			}



		private static double ValidateTemperature (double temp, string type = "set_heating")
			{
			// Accommodate hw temps
			if (type == "hotwater" && (temp == Constants.TempHwOn || temp == Constants.TempHwOff))
				{
				return temp;
				}

			// Accommodate temp deltas
			if (type == "delta")
				{
				if (temp > Constants.MaxBoostIncrease)
					return Constants.MaxBoostIncrease;
				return temp;
				}

			// Accommodate reported current temps
			if (type == "current")
				{
				if (temp < Constants.TempOff)
					return Constants.TempMinimum;
				return temp;
				}

			// Accommodate heating temps
			if (type == "set_heating")
				{
				if (temp >= Constants.TempError)
					return Constants.TempMinimum;
				else if (temp > Constants.TempMaximum)
					return Constants.TempMaximum;
				else if (temp < Constants.TempMinimum && temp != Constants.TempOff)
					return Constants.TempMinimum;
				else
					return temp;
				}

			return temp;
			}

		private static double ConvertFromF (double temp)
			{
			return Math.Round ((temp - 32) * 5 / 9, 1);
			}

		private static double ConvertToF (double temp)
			{
			return Math.Round ((temp * 9 / 5) + 32, 1);
			}
		}

	public class WiserBattery
		{
		private readonly IDictionary<string, object> _data;

		public WiserBattery (IDictionary<string, object> data)
			{
			_data = data;
			}

		public string Level => _data.TryGetValue ("BatteryLevel", out var level) ? level.ToString () : "No Battery";

		public int Percent
			{
			get
				{
				if (_data.TryGetValue ("ProductType", out var productType) && Level != "No Battery")
					{
					if (productType.ToString () == "RoomStat")
						{
						return PercentageClip (
							 (int)Math.Round (
								  ((Voltage - Constants.RoomstatMinBatteryLevel) /
								  (Constants.RoomstatFullBatteryLevel - Constants.RoomstatMinBatteryLevel)) * 100
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

		public double Voltage => _data.TryGetValue ("BatteryVoltage", out var voltage) ? Convert.ToDouble (voltage, CultureInfo.InvariantCulture) / 10 : 0;

		private static int PercentageClip (int value)
			{
			return Math.Min (100, Math.Max (0, value));
			}
		}

	public class WiserSignalStrength
		{
		private readonly IDictionary<string, object> _data;

		public WiserSignalStrength (IDictionary<string, object> data)
			{
			_data = data;
			}

		public string DisplayedSignalStrength => _data.TryGetValue ("DisplayedSignalStrength", out var strength) ? strength.ToString () : Constants.TextUnknown;

		public int? ControllerReceptionLqi
			{
			get
				{
				if (_data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict)
					{
					return receptionDict.TryGetValue ("Lqi", out var lqi) ? Convert.ToInt32 (lqi, CultureInfo.InvariantCulture) : (int?)null;
					}
				return null;
				}
			}

		public int? ControllerReceptionRssi
			{
			get
				{
				if (_data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict)
					{
					return receptionDict.TryGetValue ("Rssi", out var rssi) ? Convert.ToInt32 (rssi, CultureInfo.InvariantCulture) : (int?)null;
					}
				return null;
				}
			}

		public int ControllerSignalStrength
			{
			get
				{
				if (ControllerReceptionRssi.HasValue && ControllerReceptionRssi.Value != 0)
					{
					return Math.Min (100, (int)(2 * (ControllerReceptionRssi.Value + 100)));
					}
				return 0;
				}
			}

		public int? DeviceReceptionLqi
			{
			get
				{
				if (_data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict)
					{
					return receptionDict.TryGetValue ("Lqi", out var lqi) ? Convert.ToInt32 (lqi, CultureInfo.InvariantCulture) : (int?)null;
					}
				return null;
				}
			}

		public int? DeviceReceptionRssi
			{
			get
				{
				if (_data.TryGetValue ("ReceptionOfDevice", out var reception) && reception is Dictionary<string, object> receptionDict)
					{
					return receptionDict.TryGetValue ("Rssi", out var rssi) ? Convert.ToInt32 (rssi, CultureInfo.InvariantCulture) : (int?)null;
					}
				return null;
				}
			}

		public int? DeviceSignalStrength
			{
			get
				{
				if (DeviceReceptionRssi.HasValue)
					{
					return DeviceReceptionRssi.Value != 0 ? Math.Min (100, (int)(2 * (DeviceReceptionRssi.Value + 100))) : 0;
					}
				return null;
				}
			}
		}

	public class WiserGPS
		{
		private readonly Dictionary<string, object> _data;

		public WiserGPS (Dictionary<string, object> data)
			{
			_data = data;
			}

		public double? Latitude => _data.TryGetValue ("Latitude", out var latitude) ? Convert.ToDouble (latitude, CultureInfo.InvariantCulture) : (double?)null;

		public double? Longitude => _data.TryGetValue ("Longitude", out var longitude) ? Convert.ToDouble (longitude, CultureInfo.InvariantCulture) : (double?)null;
		}

	public class WiserCloud
		{
		private readonly string _cloudStatus;
		private readonly Dictionary<string, object> _data;

		public WiserCloud (string cloudStatus, Dictionary<string, object> data)
			{
			_cloudStatus = cloudStatus;
			_data = data;
			}

		public string ApiHost => _data.TryGetValue ("WiserApiHost", out var host) ? host.ToString () : Constants.TextUnknown;

		public string BootstrapApiHost => _data.TryGetValue ("BootStrapApiHost", out var host) ? host.ToString () : Constants.TextUnknown;

		public bool ConnectedToCloud => _cloudStatus == "Connected";

		public string ConnectionStatus => _cloudStatus;

		public bool DetailedPublishingEnabled => _data.TryGetValue ("DetailedPublishing", out var enabled) && Convert.ToBoolean (enabled, CultureInfo.InvariantCulture);

		public bool DiagnosticTelemetryEnabled => _data.TryGetValue ("EnableDiagnosticTelemetry", out var enabled) && Convert.ToBoolean (enabled, CultureInfo.InvariantCulture);
		}

	public class WiserFirmwareUpgradeItem
		{
		private readonly Dictionary<string, object> _data;

		public WiserFirmwareUpgradeItem (Dictionary<string, object> data)
			{
			_data = data;
			}

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string Filename => _data.TryGetValue ("FirmwareFilename", out var filename) ? filename.ToString () : Constants.TextUnknown;
		}

	public class WiserFirmwareUpgradeInfo
		{
		private readonly List<Dictionary<string, object>> _data;
		private readonly List<WiserFirmwareUpgradeItem> _items = new List<WiserFirmwareUpgradeItem> ();

		public WiserFirmwareUpgradeInfo (List<Dictionary<string, object>> data)
			{
			_data = data;
			foreach (var item in _data)
				{
				_items.Add (new WiserFirmwareUpgradeItem (item));
				}
			}

		public List<WiserFirmwareUpgradeItem> All => _items;

		public WiserFirmwareUpgradeItem GetById (int id)
			{
			return _items.FirstOrDefault (item => item.Id == id);
			}
		}

	public class WiserHubCapabilitiesInfo
		{
		private readonly Dictionary<string, object> _data;

		public WiserHubCapabilitiesInfo (Dictionary<string, object> data)
			{
			_data = data;
			}

		public Dictionary<string, object> All => new Dictionary<string, object> (_data);

		public bool SmartPlug => _data.TryGetValue ("SmartPlug", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool ITRV => _data.TryGetValue ("ITRV", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool Roomstat => _data.TryGetValue ("Roomstat", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool UFH => _data.TryGetValue ("UFH", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool UFHFloorTempSensor => _data.TryGetValue ("UFHFloorTempSensor", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool UFHDewSensor => _data.TryGetValue ("UFHDewSensor", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool HACT => _data.TryGetValue ("HACT", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool LACT => _data.TryGetValue ("LACT", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool Light => _data.TryGetValue ("Light", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool Shutter => _data.TryGetValue ("Shutter", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool LoadController => _data.TryGetValue ("LoadController", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);
		}


	public class WiserZigbee
		{
		private readonly Dictionary<string, object> _data;

		public WiserZigbee (Dictionary<string, object> data)
			{
			_data = data;
			}

		public int Error72Reset => _data.TryGetValue ("Error72Reset", out var value) ? Convert.ToInt32 (value, CultureInfo.InvariantCulture) : 0;

		public int JPANCount => _data.TryGetValue ("JPANCount", out var value) ? Convert.ToInt32 (value, CultureInfo.InvariantCulture) : 0;

		public int NetworkChannel => _data.TryGetValue ("NetworkChannel", out var value) ? Convert.ToInt32 (value, CultureInfo.InvariantCulture) : 0;

		public int NoSignalReset => _data.TryGetValue ("NoSignalReset", out var value) ? Convert.ToInt32 (value, CultureInfo.InvariantCulture) : 0;

		public string ModuleVersion => _data.TryGetValue ("ZigbeeModuleVersion", out var value) ? value.ToString () : Constants.TextUnknown;

		public string EUI => _data.TryGetValue ("ZigbeeEUI", out var value) ? value.ToString () : Constants.TextUnknown;
		}

	public static class SpecialTimes
		{
		public static Dictionary<string, string> FormatOutput (List<int> sunTimes)
			{
			var output = new Dictionary<string, string> ();
			var today = (int)DateTime.Today.DayOfWeek;
			var days = Constants.Weekdays.Concat(Constants.Weekends).ToList ();

			for (int i = 0; i < 7; i++)
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
			var timeStr = time.ToString (CultureInfo.InvariantCulture).PadLeft (4, '0');
			return $"{timeStr.Substring (0, 2)}:{timeStr.Substring (2, 2)}";
			}

		public static Dictionary<string, string> SunriseTimes (List<int> times)
			{
			return FormatOutput (times);
			}

		public static Dictionary<string, string> SunsetTimes (List<int> times)
			{
			return FormatOutput (times);
			}
		}


	public class WiserElectricalLevelDevice : WiserDevice
		{
		public WiserElectricalLevelDevice (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			}

		// Lights and shutters currently have model identifier as Unknown
		public override string Model => Data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TextUnknown;
		}


	public class WiserScheduleNext
		{
		private readonly string _scheduleType;
		private readonly Dictionary<string, object> _data;

		public WiserScheduleNext (string scheduleType, Dictionary<string, object> data)
			{
			_scheduleType = scheduleType;
			_data = data;
			}

		public string Day => _data.TryGetValue ("Day", out var day) ? day.ToString () : "";

		public TimeSpan Time
			{
			get
				{
				int timeValue = _data.TryGetValue ("Time", out var time) ? Convert.ToInt32 (time, CultureInfo.InvariantCulture) : 0;
				string timeStr = timeValue.ToString ("D4", CultureInfo.InvariantCulture);
				return TimeSpan.ParseExact ($"{timeStr.Substring (0, 2)}:{timeStr.Substring (2, 2)}", "hh\\:mm", null);
				}
			}

		public DateTime DateTime
			{
			get
				{
				try
					{
					var allDays = Constants.Weekdays.Concat(Constants.Weekends).ToList ();
					int nextScheduleDay = (allDays.IndexOf (Day) + 1) % 7;
					TimeSpan nextScheduleTime = Time;
					int currentDay = (int)DateTime.Today.DayOfWeek;
					TimeSpan currentTime = DateTime.Now.TimeOfDay;

					// If next day or time on earlier weekday, add week to date
					int daysDiff = nextScheduleDay - currentDay;
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

		public object? Setting
			{
			get
				{
				if (_scheduleType == Constants.TextHeating)
					{
					return WiserTemperatureFunctions.FromWiserTemp (_data.TryGetValue ("DegreesC", out var temp) ? temp : 0);
					}
				if (_scheduleType == Constants.TextOnOff)
					{
					return _data.TryGetValue ("State", out var state) ? state : null;
					}
				if (_scheduleType == Constants.TextLevel)
					{
					return _data.TryGetValue ("Level", out var level) ? level : null;
					}
				return null;
				}
			}
		}

	public class WiserDiscoveredHub
		{
		private readonly string _ip;
		private readonly string _hostname;
		private readonly string _name;

		public WiserDiscoveredHub (string ip, string hostname, string name)
			{
			_ip = ip;
			_hostname = hostname;
			_name = name;
			}

		public string Ip => _ip;
		public string Hostname => _hostname;
		public string Name => _name;
		}

	public class WiserDiscovery
		{
		private readonly List<WiserDiscoveredHub> _discoveredHubs = new List<WiserDiscoveredHub> ();

		// Note: This is a simplified implementation as C# doesn't have a direct equivalent to Python's zeroconf
		// In a real implementation, you would use a library like Makaretu.Dns.Multicast or similar
		public List<WiserDiscoveredHub> DiscoverHub (int minSearchTime = 2, int maxSearchTime = 10)
			{
			// This is a placeholder. In a real implementation, you would use mDNS discovery
			Console.WriteLine ("mDNS discovery is not implemented in this C# version.");
			Console.WriteLine ("You would need to use a library like Makaretu.Dns.Multicast for actual discovery.");

			// Return empty list as this is just a placeholder
			return _discoveredHubs;
			}
		}

	public static class WiserTimeExtensions
		{
		public static string ToWiserTime (this long time)
			{
			if (time < 0 || time > 2359)
				throw new ArgumentOutOfRangeException (nameof (time), "Time must be between 0 and 2359.");
			string timeStr = time.ToString ("D4", CultureInfo.InvariantCulture);
			return $"{timeStr.Substring (0, 2)}:{timeStr.Substring (2, 2)}";
			}
		public static TimeSpan FromWiserTime (this string timeStr)
			{
			if (string.IsNullOrWhiteSpace (timeStr) || timeStr.Length != 5)
				throw new ArgumentException ("Invalid time format. Expected 'HH:MM' format.");
			int hours = int.Parse (timeStr.Substring (0, 2), CultureInfo.InvariantCulture);
			int minutes = int.Parse (timeStr.Substring (3, 2), CultureInfo.InvariantCulture);
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
				if (timeStr.Length == 5 && timeStr[2] == ':') // Check for 'HH:MM' format
					{
					return timeStr;
					}
				else if (timeStr.Length == 4 && int.TryParse (timeStr, out int timeInt) && timeInt >= 0 && timeInt <= 2359)
					{
					return ToWiserTime (timeInt);
					}

			throw new ArgumentException ("Invalid time object type. Expected long, TimeSpan, or string in 'HH:MM' format.");
			}
		}

	public static class StringExtensions
		{
		public static string Title (this string str)
			{
			if (string.IsNullOrWhiteSpace (str))
				return str;
			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase (str.ToLowerInvariant ());
			}

		public static string Capitalize (this string str)
			{
			if (string.IsNullOrWhiteSpace (str))
				return str;
			return $"{char.ToUpper (str[0], CultureInfo.InvariantCulture)}{str.Substring (1).ToLower (CultureInfo.InvariantCulture)}";
			}
		}
	}

// -----

