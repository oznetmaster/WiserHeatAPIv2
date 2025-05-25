// WiserHeatApiV2.cs
using log4net;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public static class Constants
		{
		// Temperature Constants
		public const double DEFAULT_AWAY_MODE_TEMP = 10.5;
		public const double DEFAULT_DEGRADED_TEMP = 18;
		public const int MAX_BOOST_INCREASE = 5;
		public const int TEMP_ERROR = 2000;
		public const int TEMP_MINIMUM = 5;
		public const int TEMP_MAXIMUM = 30;
		public const int TEMP_HW_ON = 110;
		public const int TEMP_HW_OFF = -20;
		public const int TEMP_OFF = -20;

		// Battery Constants
		public const double ROOMSTAT_MIN_BATTERY_LEVEL = 1.7;
		public const double ROOMSTAT_FULL_BATTERY_LEVEL = 2.7;
		public const double TRV_FULL_BATTERY_LEVEL = 3.0;
		public const double TRV_MIN_BATTERY_LEVEL = 2.4;

		// Text Values
		public const string TEXT_AUTO = "Auto";
		public const string TEXT_CLOSE = "Close";
		public const string TEXT_DEGREESC = "DegreesC";
		public const string TEXT_HEATING = "Heating";
		public const string TEXT_LEVEL = "Level";
#if LIGHT
		public const string TEXT_LIGHTING = "Lighting";
#endif
		public const string TEXT_MANUAL = "Manual";
		public const string TEXT_NO_CHANGE = "NoChange";
		public const string TEXT_NONE = "None";
		public const string TEXT_OFF = "Off";
		public const string TEXT_ON = "On";
		public const string TEXT_ONOFF = "OnOff";
		public const string TEXT_OPEN = "Open";
		public const string TEXT_SETPOINT = "Setpoint";
#if SHUTTER
		public const string TEXT_SHUTTERS = "Shutters";
#endif
		public const string TEXT_STATE = "State";
		public const string TEXT_TEMP = "Temp";
		public const string TEXT_TIME = "Time";
		public const string TEXT_UNKNOWN = "Unknown";
		public const string TEXT_WEEKDAYS = "Weekdays";
		public const string TEXT_WEEKENDS = "Weekends";

		// Day Value Lists
		public static readonly List<string> WEEKDAYS = new List<string> { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
		public static readonly List<string> WEEKENDS = new List<string> { "Saturday", "Sunday" };
		public static readonly List<string> SPECIAL_DAYS = new List<string> { TEXT_WEEKDAYS, TEXT_WEEKENDS };
		public static readonly Dictionary<string, int> SPECIAL_TIMES = new Dictionary<string, int> { { "Sunrise", 3000 }, { "Sunset", 4000 } };

		// Battery Level Enum
		public static readonly Dictionary<double, int> TRV_BATTERY_LEVEL_MAPPING = new Dictionary<double, int>
		  {
				{ 3.0, 100 }, { 2.9, 80 }, { 2.8, 60 }, { 2.7, 40 }, { 2.6, 30 }, { 2.5, 20 }, { 2.4, 10 }, { 2.3, 0 }
		  };


		public static readonly Dictionary<string, Dictionary<string, object>> DEFAULT_LEVEL_SCHEDULE = new Dictionary<string, Dictionary<string, object>>
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

	public enum WiserUnitsEnum
		{
		Imperial,
		Metric
		}

	public enum WiserScheduleTypeEnum
		{
		Heating,
		OnOff,
		Level,
		Lighting,
		Shutters
		}

	public enum WiserHeatingModeEnum
		{
		Off,
		Auto,
		Manual
		}

	public enum WiserHotWaterModeEnum
		{
		Auto,
		Manual
		}

	public enum WiserSmartPlugModeEnum
		{
		Auto,
		Manual
		}

#if SHUTTER
	public enum WiserShutterModeEnum
		{
		Auto,
		Manual
		}
#endif
#if LIGHT
	public enum WiserLightModeEnum
		{
		Auto,
		Manual
		}
#endif

	public enum WiserAwayActionEnum
		{
		Off,
		NoChange,
		Close
		}

	// Exception classes

	public class WiserHubNotImplementedError : Exception
		{
		public WiserHubNotImplementedError (string message) : base (message) { }
		}

	// Helper classes
	public static class WiserTemperatureFunctions
		{
		public static int ToWiserTemp (double temp, string type = "set_heating", WiserUnitsEnum units = WiserUnitsEnum.Metric)
			{
			temp = (int)(ValidateTemperature (temp, type) * 10);

			// Convert to metric if imperial units set
			if (units == WiserUnitsEnum.Imperial)
				{
				temp = ConvertFromF (temp);
				}

			return (int)temp;
			}

		public static double FromWiserTemp (int? temp, string type = "set_heating", WiserUnitsEnum units = WiserUnitsEnum.Metric)
			{
			if (!temp.HasValue)
				return 0;
			double realTemp;

			if (temp >= Constants.TEMP_ERROR)  // Fix high value from hub when lost sight of iTRV
				{
				realTemp = Constants.TEMP_MINIMUM;
				}
			else
				{
				realTemp = ValidateTemperature (Math.Round ((double)temp / 10, 1), type);
				}

			// Convert to imperial if imperial units set
			if (units == WiserUnitsEnum.Imperial)
				{
				realTemp = ConvertToF (realTemp);
				}

			return realTemp;
			}

		private static double ValidateTemperature (double temp, string type = "set_heating")
			{
			// Accommodate hw temps
			if (type == "hotwater" && (temp == Constants.TEMP_HW_ON || temp == Constants.TEMP_HW_OFF))
				{
				return temp;
				}

			// Accommodate temp deltas
			if (type == "delta")
				{
				if (temp > Constants.MAX_BOOST_INCREASE)
					return Constants.MAX_BOOST_INCREASE;
				return temp;
				}

			// Accommodate reported current temps
			if (type == "current")
				{
				if (temp < Constants.TEMP_OFF)
					return Constants.TEMP_MINIMUM;
				return temp;
				}

			// Accommodate heating temps
			if (type == "set_heating")
				{
				if (temp >= Constants.TEMP_ERROR)
					return Constants.TEMP_MINIMUM;
				else if (temp > Constants.TEMP_MAXIMUM)
					return Constants.TEMP_MAXIMUM;
				else if (temp < Constants.TEMP_MINIMUM && temp != Constants.TEMP_OFF)
					return Constants.TEMP_MINIMUM;
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
		private readonly Dictionary<string, object> _data;

		public WiserBattery (Dictionary<string, object> data)
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
								  ((Voltage - Constants.ROOMSTAT_MIN_BATTERY_LEVEL) /
								  (Constants.ROOMSTAT_FULL_BATTERY_LEVEL - Constants.ROOMSTAT_MIN_BATTERY_LEVEL)) * 100
							 )
						);
						}
					else if (productType.ToString () == "iTRV" && Level != "No Battery")
						{
						return Constants.TRV_BATTERY_LEVEL_MAPPING.TryGetValue (Voltage, out var level) ? level : 0;
						}
					}
				return 0;
				}
			}

		public double Voltage => _data.TryGetValue ("BatteryVoltage", out var voltage) ? Convert.ToDouble (voltage) / 10 : 0;

		private int PercentageClip (int value)
			{
			return Math.Min (100, Math.Max (0, value));
			}
		}

	public class WiserSignalStrength
		{
		private readonly Dictionary<string, object> _data;

		public WiserSignalStrength (Dictionary<string, object> data)
			{
			_data = data;
			}

		public string DisplayedSignalStrength => _data.TryGetValue ("DisplayedSignalStrength", out var strength) ? strength.ToString () : Constants.TEXT_UNKNOWN;

		public int? ControllerReceptionLqi
			{
			get
				{
				if (_data.TryGetValue ("ReceptionOfController", out var reception) && reception is Dictionary<string, object> receptionDict)
					{
					return receptionDict.TryGetValue ("Lqi", out var lqi) ? Convert.ToInt32 (lqi) : (int?)null;
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
					return receptionDict.TryGetValue ("Rssi", out var rssi) ? Convert.ToInt32 (rssi) : (int?)null;
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
					return receptionDict.TryGetValue ("Lqi", out var lqi) ? Convert.ToInt32 (lqi) : (int?)null;
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
					return receptionDict.TryGetValue ("Rssi", out var rssi) ? Convert.ToInt32 (rssi) : (int?)null;
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

		public double? Latitude => _data.TryGetValue ("Latitude", out var latitude) ? Convert.ToDouble (latitude) : (double?)null;

		public double? Longitude => _data.TryGetValue ("Longitude", out var longitude) ? Convert.ToDouble (longitude) : (double?)null;
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

		public string ApiHost => _data.TryGetValue ("WiserApiHost", out var host) ? host.ToString () : Constants.TEXT_UNKNOWN;

		public string BootstrapApiHost => _data.TryGetValue ("BootStrapApiHost", out var host) ? host.ToString () : Constants.TEXT_UNKNOWN;

		public bool ConnectedToCloud => _cloudStatus == "Connected";

		public string ConnectionStatus => _cloudStatus;

		public bool DetailedPublishingEnabled => _data.TryGetValue ("DetailedPublishing", out var enabled) && Convert.ToBoolean (enabled);

		public bool DiagnosticTelemetryEnabled => _data.TryGetValue ("EnableDiagnosticTelemetry", out var enabled) && Convert.ToBoolean (enabled);
		}

	public class WiserFirmwareUpgradeItem
		{
		private readonly Dictionary<string, object> _data;

		public WiserFirmwareUpgradeItem (Dictionary<string, object> data)
			{
			_data = data;
			}

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Filename => _data.TryGetValue ("FirmwareFilename", out var filename) ? filename.ToString () : Constants.TEXT_UNKNOWN;
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

		public bool SmartPlug => _data.TryGetValue ("SmartPlug", out var value) && Convert.ToBoolean (value);

		public bool ITRV => _data.TryGetValue ("ITRV", out var value) && Convert.ToBoolean (value);

		public bool Roomstat => _data.TryGetValue ("Roomstat", out var value) && Convert.ToBoolean (value);

		public bool UFH => _data.TryGetValue ("UFH", out var value) && Convert.ToBoolean (value);

		public bool UFHFloorTempSensor => _data.TryGetValue ("UFHFloorTempSensor", out var value) && Convert.ToBoolean (value);

		public bool UFHDewSensor => _data.TryGetValue ("UFHDewSensor", out var value) && Convert.ToBoolean (value);

		public bool HACT => _data.TryGetValue ("HACT", out var value) && Convert.ToBoolean (value);

		public bool LACT => _data.TryGetValue ("LACT", out var value) && Convert.ToBoolean (value);

		public bool Light => _data.TryGetValue ("Light", out var value) && Convert.ToBoolean (value);

		public bool Shutter => _data.TryGetValue ("Shutter", out var value) && Convert.ToBoolean (value);

		public bool LoadController => _data.TryGetValue ("LoadController", out var value) && Convert.ToBoolean (value);
		}

	public class WiserDetectedNetwork
		{
		private readonly Dictionary<string, object> _data;

		public WiserDetectedNetwork (Dictionary<string, object> data)
			{
			_data = data;
			}

		public string SSID => _data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : null;

		public int? Channel => _data.TryGetValue ("Channel", out var channel) ? Convert.ToInt32 (channel) : (int?)null;

		public string SecurityMode => _data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : null;

		public int? RSSI => _data.TryGetValue ("RSSI", out var rssi) ? Convert.ToInt32 (rssi) : (int?)null;
		}

	public class WiserNetwork
		{
		private readonly Dictionary<string, object> _data;
		private readonly Dictionary<string, object> _dhcpStatus;
		private readonly Dictionary<string, object> _networkInterface;
		private readonly List<WiserDetectedNetwork> _detectedAccessPoints = new List<WiserDetectedNetwork> ();

		public WiserNetwork (Dictionary<string, object> data)
			{
			_data = data;
			_dhcpStatus = _data.TryGetValue ("DhcpStatus", out var dhcpStatus) && dhcpStatus is Dictionary<string, object> dhcpDict
				 ? dhcpDict : new Dictionary<string, object> ();
			_networkInterface = _data.TryGetValue ("NetworkInterface", out var networkInterface) && networkInterface is Dictionary<string, object> networkDict
				 ? networkDict : new Dictionary<string, object> ();

			if (_data.TryGetValue ("DetectedAccessPoints", out var accessPoints) && accessPoints is List<object> accessPointsList)
				{
				foreach (var ap in accessPointsList)
					{
					if (ap is Dictionary<string, object> apDict)
						{
						_detectedAccessPoints.Add (new WiserDetectedNetwork (apDict));
						}
					}
				}
			}

		public List<WiserDetectedNetwork> DetectedAccessPoints => _detectedAccessPoints;

		public string DhcpMode => _networkInterface.TryGetValue ("DhcpMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

		public string Hostname => _networkInterface.TryGetValue ("HostName", out var hostname) ? hostname.ToString () : Constants.TEXT_UNKNOWN;

		public string IpAddress
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4Address", out var address) ? address.ToString () : Constants.TEXT_UNKNOWN;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4HostAddress", out var address) ? address.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpSubnetMask
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TEXT_UNKNOWN;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4SubnetMask", out var mask) ? mask.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpGateway
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TEXT_UNKNOWN;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4DefaultGateway", out var gateway) ? gateway.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpPrimaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4PrimaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string IpSecondaryDNS
			{
			get
				{
				if (DhcpMode == "Client")
					{
					return _dhcpStatus.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				else
					{
					return _networkInterface.TryGetValue ("IPv4SecondaryDNS", out var dns) ? dns.ToString () : Constants.TEXT_UNKNOWN;
					}
				}
			}

		public string MacAddress => _data.TryGetValue ("MacAddress", out var mac) ? mac.ToString () : Constants.TEXT_UNKNOWN;

		public int SignalPercent
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current))
					{
					return Math.Min (100, (int)(2 * (Convert.ToInt32 (current) + 100)));
					}
				return 0;
				}
			}

		public int SignalRssi
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Current", out var current))
					{
					return Convert.ToInt32 (current);
					}
				return 0;
				}
			}

		public int SignalRssiMin
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Min", out var min))
					{
					return Convert.ToInt32 (min);
					}
				return 0;
				}
			}

		public int SignalRssiMax
			{
			get
				{
				if (_data.TryGetValue ("RSSI", out var rssi) && rssi is Dictionary<string, object> rssiDict && rssiDict.TryGetValue ("Max", out var max))
					{
					return Convert.ToInt32 (max);
					}
				return 0;
				}
			}

		public string SecurityMode => _data.TryGetValue ("SecurityMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

		public string SSID => _data.TryGetValue ("SSID", out var ssid) ? ssid.ToString () : Constants.TEXT_UNKNOWN;
		}

	public class WiserOpenThermBoilerParameters
		{
		private readonly Dictionary<string, object> _data;

		public WiserOpenThermBoilerParameters (Dictionary<string, object> data)
			{
			_data = data;
			}

		public bool? HwSetpointTransferEnable => _data.TryGetValue ("dhwSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? ChSetpointTransferEnable => _data.TryGetValue ("maxChSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? HwSetpointReadWrite => _data.TryGetValue ("dhwSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? ChSetpointReadWrite => _data.TryGetValue ("maxChSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value) : null;
		}

	public class WiserOpenThermOperationalData
		{
		private readonly Dictionary<string, object> _data;

		public WiserOpenThermOperationalData (Dictionary<string, object> data)
			{
			_data = data;
			}

		public double ChPressureBar => _data.TryGetValue ("ChPressureBar", out var value) ? Convert.ToDouble (value) / 10 : 0;

		public double ChFlowTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("Ch1FlowTemperature", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public double ChReturnTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ChReturnTemperature", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public double HwTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("Dhw1Temperature", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public int? RelativeModulationLevel => _data.TryGetValue ("RelativeModulationLevel", out var value) ? (int?)Convert.ToInt32 (value) : null;

		public int? SlaveStatus => _data.TryGetValue ("SlaveStatus", out var value) ? (int?)Convert.ToInt32 (value) : null;
		}

	public class WiserOpentherm
		{
		internal readonly Dictionary<string, object> _data;
		private readonly string _enabledStatus;

		public WiserOpentherm (Dictionary<string, object> data, string enabledStatus)
			{
			_data = data;
			_enabledStatus = enabledStatus;
			}

		public double ChFlowActiveLowerSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveLowerSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public double ChFlowActiveUpperSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveUpperSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public bool Ch1FlowEnabled => _data.TryGetValue ("ch1FlowEnable", out var value) && Convert.ToBoolean (value);

		public double Ch1FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch1FlowSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public bool Ch2FlowEnabled => _data.TryGetValue ("ch2FlowEnable", out var value) && Convert.ToBoolean (value);

		public double Ch2FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch2FlowSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public string ConnectionStatus => _enabledStatus;

		public bool Enabled => _data.TryGetValue ("Enabled", out var value) && Convert.ToBoolean (value);

		public bool HwEnabled => _data.TryGetValue ("dhwEnable", out var value) && Convert.ToBoolean (value);

		public double HwFlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("dhwFlowSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public string OperatingMode => _data.TryGetValue ("operatingMode", out var value) ? value.ToString () : null;

		public WiserOpenThermOperationalData OperationalData
			{
			get
				{
				if (_data.TryGetValue ("operationalData", out var data) && data is Dictionary<string, object> dataDict)
					{
					return new WiserOpenThermOperationalData (dataDict);
					}
				return new WiserOpenThermOperationalData (new Dictionary<string, object> ());
				}
			}

		public WiserOpenThermBoilerParameters BoilerParameters
			{
			get
				{
				if (_data.TryGetValue ("preDefinedRemoteBoilerParameters", out var data) && data is Dictionary<string, object> dataDict)
					{
					return new WiserOpenThermBoilerParameters (dataDict);
					}
				return new WiserOpenThermBoilerParameters (new Dictionary<string, object> ());
				}
			}

		public double RoomSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomSetpoint", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public double RoomTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomTemperature", out var value) ? Convert.ToInt32 (value) : (int?)null, "current");

		public int? TrackedRoomId => _data.TryGetValue ("TrackedRoomId", out var value) ? (int?)Convert.ToInt32 (value) : null;
		}

	public class WiserZigbee
		{
		private readonly Dictionary<string, object> _data;

		public WiserZigbee (Dictionary<string, object> data)
			{
			_data = data;
			}

		public int Error72Reset => _data.TryGetValue ("Error72Reset", out var value) ? Convert.ToInt32 (value) : 0;

		public int JPANCount => _data.TryGetValue ("JPANCount", out var value) ? Convert.ToInt32 (value) : 0;

		public int NetworkChannel => _data.TryGetValue ("NetworkChannel", out var value) ? Convert.ToInt32 (value) : 0;

		public int NoSignalReset => _data.TryGetValue ("NoSignalReset", out var value) ? Convert.ToInt32 (value) : 0;

		public string ModuleVersion => _data.TryGetValue ("ZigbeeModuleVersion", out var value) ? value.ToString () : Constants.TEXT_UNKNOWN;

		public string EUI => _data.TryGetValue ("ZigbeeEUI", out var value) ? value.ToString () : Constants.TEXT_UNKNOWN;
		}

	public static class SpecialTimes
		{
		public static Dictionary<string, string> FormatOutput (List<int> sunTimes)
			{
			var output = new Dictionary<string, string> ();
			var today = (int)DateTime.Today.DayOfWeek;
			var days = Constants.WEEKDAYS.Concat (Constants.WEEKENDS).ToList ();

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
			var timeStr = time.ToString ().PadLeft (4, '0');
			return timeStr.Substring (0, 2) + ":" + timeStr.Substring (2, 2);
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

	public class WiserDevice
		{
		protected readonly Dictionary<string, object> _data;
		protected readonly WiserSignalStrength _signal;

		public WiserDevice (Dictionary<string, object> data)
			{
			_data = data;
			_signal = new WiserSignalStrength (data);
			}

		public virtual int DeviceTypeId => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string FirmwareVersion => _data.TryGetValue ("ActiveFirmwareVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Model => _data.TryGetValue ("ModelIdentifier", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

		public string Name => $"{ProductType}-{Id}";

		public int NodeId => _data.TryGetValue ("NodeId", out var nodeId) ? Convert.ToInt32 (nodeId) : 0;

		public string ProductIdentifier => _data.TryGetValue ("ProductIdentifier", out var id) ? id.ToString () : Constants.TEXT_UNKNOWN;

		public string ProductModel => _data.TryGetValue ("ProductModel", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

		public int ParentNodeId => _data.TryGetValue ("ParentNodeId", out var nodeId) ? Convert.ToInt32 (nodeId) : 0;

		public string ProductType => _data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public string SerialNumber => _data.TryGetValue ("SerialNumber", out var serial) ? serial.ToString () : Constants.TEXT_UNKNOWN;

		public WiserSignalStrength Signal => _signal;
		}

	public class WiserElectricalLevelDevice : WiserDevice
		{
		protected readonly Dictionary<string, object> _deviceTypeData;

		public WiserElectricalLevelDevice (Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_deviceTypeData = deviceTypeData;
			}

		public override int DeviceTypeId => _deviceTypeData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		// Lights and shutters currently have model identifier as Unknown
		public new string Model => _data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;
		}

	public class WiserSmartValve : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserSmartValve (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSMARTVALVE, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : 0, "current");

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public string MountingOrientation => _deviceTypeData.TryGetValue ("MountingOrientation", out var orientation) ? orientation.ToString () : null;

		public int PercentageDemand => _deviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserSmartValveCollection
		{
		private readonly List<WiserSmartValve> _smartValves = new List<WiserSmartValve> ();

		public List<WiserSmartValve> All => _smartValves;

		public int Count => _smartValves.Count;

		public WiserSmartValve GetById (int id)
			{
			return _smartValves.FirstOrDefault (valve => valve.Id == id);
			}
		}

	public class WiserRoomStat : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserRoomStat (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERROOMSTAT, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public int CurrentHumidity => _deviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? Convert.ToInt32 (humidity) : 0;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : 0, "current");

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserRoomStatCollection
		{
		private readonly List<WiserRoomStat> _roomStats = new List<WiserRoomStat> ();

		public List<WiserRoomStat> All => _roomStats;

		public int Count => _roomStats.Count;

		public WiserRoomStat GetById (int id)
			{
			return _roomStats.FirstOrDefault (stat => stat.Id == id);
			}
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
				int timeValue = _data.TryGetValue ("Time", out var time) ? Convert.ToInt32 (time) : 0;
				string timeStr = timeValue.ToString ("D4");
				return TimeSpan.ParseExact (timeStr.Substring (0, 2) + ":" + timeStr.Substring (2, 2), "hh\\:mm", null);
				}
			}

		public DateTime DateTime
			{
			get
				{
				try
					{
					var allDays = Constants.WEEKDAYS.Concat (Constants.WEEKENDS).ToList ();
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

		public object Setting
			{
			get
				{
				if (_scheduleType == Constants.TEXT_HEATING)
					{
					return WiserTemperatureFunctions.FromWiserTemp (_data.TryGetValue ("DegreesC", out var temp) ? Convert.ToInt32 (temp) : 0);
					}
				if (_scheduleType == Constants.TEXT_ONOFF)
					{
					return _data.TryGetValue ("State", out var state) ? state : null;
					}
				if (_scheduleType == Constants.TEXT_LEVEL)
					{
					return _data.TryGetValue ("Level", out var level) ? level : null;
					}
				return null;
				}
			}
		}

	public abstract class WiserSchedule
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedule));

		protected readonly WiserRestController _wiserRestController;
		protected readonly string _type;
		protected readonly Dictionary<string, object> _scheduleData;
		protected readonly Dictionary<string, string> _sunrises;
		protected readonly Dictionary<string, string> _sunsets;
		protected readonly List<Dictionary<string, object>> _assignments = new List<Dictionary<string, object>> ();
		protected readonly List<int> _deviceIds = new List<int> ();

		protected WiserSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
									  Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			{
			_wiserRestController = wiserRestController;
			_type = scheduleType;
			_scheduleData = scheduleData;
			_sunrises = sunrises;
			_sunsets = sunsets;
			}

		protected bool ValidateScheduleType (Dictionary<string, object> scheduleData)
			{
			return (scheduleData.TryGetValue ("Type", out var type) && type.ToString () == ScheduleType) ||
					 (scheduleData.TryGetValue ("SubType", out var subType) && subType.ToString () == ScheduleType);
			}

		protected bool IsValidTime (string timeValue)
			{
			return DateTime.TryParseExact (timeValue, "HH:mm", null, System.Globalization.DateTimeStyles.None, out _);
			}

		protected Dictionary<string, object> EnsureType (Dictionary<string, object> scheduleData)
			{
			if (!scheduleData.ContainsKey ("Type"))
				{
				scheduleData["Type"] = ScheduleType;
				}
			return scheduleData;
			}

		protected Dictionary<string, object> RemoveScheduleElements (Dictionary<string, object> scheduleData)
			{
			var result = new Dictionary<string, object> (scheduleData);
			var removeList = new[] { "id", "CurrentSetpoint", "CurrentState", "Description", "CurrentLevel", "Name", "Next", "Type" };
			foreach (var item in removeList)
				{
				if (result.ContainsKey (item))
					{
					result.Remove (item);
					}
				}
			return result;
			}

		protected abstract Dictionary<string, object> ConvertFromWiserSchedule (Dictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract Dictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData);
		protected abstract List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule);

		protected async Task<bool> SendScheduleCommandAsync (string action, Dictionary<string, object> scheduleData, int id = 0)
			{
			try
				{
				bool result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, _type).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		public List<int> DeviceIds => _deviceIds;

		public List<Dictionary<string, object>> Assignments => _assignments;

		public List<int> AssignmentIds => _assignments.Select (a => Convert.ToInt32 (a["id"])).ToList ();

		public List<string> AssignmentNames => _assignments.Select (a => a["name"].ToString ()).ToList ();

		public object CurrentSetting
			{
			get
				{
				if (_type == WiserScheduleTypeEnum.Heating.ToString ())
					{
					return WiserTemperatureFunctions.FromWiserTemp (
						 _scheduleData.TryGetValue ("CurrentSetpoint", out var setpoint) ? Convert.ToInt32 (setpoint) : Constants.TEMP_MINIMUM);
					}
				if (_type == WiserScheduleTypeEnum.OnOff.ToString ())
					{
					return _scheduleData.TryGetValue ("CurrentState", out var state) ? state : Constants.TEXT_UNKNOWN;
					}
				if (_type == WiserScheduleTypeEnum.Level.ToString ())
					{
					return _scheduleData.TryGetValue ("CurrentLevel", out var level) ? level : Constants.TEXT_UNKNOWN;
					}
				return null;
				}
			}

		public int Id => _scheduleData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Name => _scheduleData.TryGetValue ("Name", out var name) ? name.ToString () : null;

		public WiserScheduleNext Next
			{
			get
				{
				if (_scheduleData.TryGetValue ("Next", out var next) && next is Dictionary<string, object> nextDict)
					{
					return new WiserScheduleNext (_type, nextDict);
					}
				return null;
				}
			}

		public Dictionary<string, object> ScheduleData => RemoveScheduleElements (new Dictionary<string, object> (_scheduleData));

		public Dictionary<string, object> WsScheduleData
			{
			get
				{
				var s = RemoveScheduleElements (ConvertFromWiserSchedule (ScheduleData, genericSetpoint: true));
				return new Dictionary<string, object>
					 {
						  { "Id", Id },
						  { "Name", Name },
						  { "Type", _type },
						  { "SubType", ScheduleType },
						  { "Assignments", Assignments },
						  { "ScheduleData", s.Select(a => new Dictionary<string, object>
								{
									 { "day", a.Key },
									 { "slots", a.Value }
								}).ToList()
						  }
					 };
				}
			}

		public string ScheduleType => _type;

		public async Task<bool> CopyScheduleAsync (int toId)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (new Dictionary<string, object> (_scheduleData)), toId).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error copying schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> DeleteScheduleAsync ()
			{
			try
				{
				if (Id != 1000)
					{
					await SendScheduleCommandAsync ("DELETE", new Dictionary<string, object> ()).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ("You cannot delete the schedule for HotWater");
					return false;
					}
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error deleting schedule: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToFile (string scheduleFile)
			{
			try
				{
				File.WriteAllText (scheduleFile, JsonConvert.SerializeObject (EnsureType (_scheduleData), Newtonsoft.Json.Formatting.Indented));
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error saving schedule to file: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToYamlFile (string scheduleYamlFile)
			{
			try
				{
				var serializer = new YamlDotNet.Serialization.Serializer ();
				File.WriteAllText (scheduleYamlFile, serializer.Serialize (ConvertFromWiserSchedule (_scheduleData)));
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error saving schedule to yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleAsync (Dictionary<string, object> scheduleData)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData)).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error setting schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromFileAsync (string scheduleFile)
			{
			try
				{
				var scheduleData = JsonConvert.DeserializeObject<Dictionary<string, object>> (File.ReadAllText (scheduleFile));
				if (ValidateScheduleType (scheduleData))
					{
					await SetScheduleAsync (RemoveScheduleElements (scheduleData)).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"Error setting schedule from file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromYamlFileAsync (string scheduleYamlFile)
			{
			try
				{
				var deserializer = new YamlDotNet.Serialization.Deserializer ();
				var scheduleData = deserializer.Deserialize<Dictionary<string, object>> (File.ReadAllText (scheduleYamlFile));

				if (ValidateScheduleType (scheduleData))
					{
					var schedule = ConvertToWiserSchedule (scheduleData);
					await SetScheduleAsync (schedule).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"This is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error setting schedule from yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromWsDataAsync (Dictionary<string, object> scheduleData)
			{
			try
				{
				if (ValidateScheduleType (scheduleData))
					{
					var scheduleJson = new Dictionary<string, object> ();
					if (scheduleData.TryGetValue ("ScheduleData", out var scheduleDataObj) && scheduleDataObj is List<object> scheduleDataList)
						{
						foreach (var entry in scheduleDataList)
							{
							if (entry is Dictionary<string, object> entryDict)
								{
								scheduleJson[entryDict["day"].ToString ()] = entryDict["slots"];
								}
							}
						}

					var schedule = ConvertToWiserSchedule (scheduleJson);
					await SetScheduleAsync (schedule).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"Error setting schedule from websocket data: {ex.Message}");
				return false;
				}
			}
		}

	public class WiserHeatingSchedule : WiserSchedule
		{
		public WiserHeatingSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
											Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public async Task<bool> AssignScheduleAsync (List<int> roomIds, bool includeCurrent = true)
			{
			if (roomIds == null)
				{
				roomIds = new List<int> { };
				}

			if (includeCurrent)
				{
				roomIds = roomIds.Concat (AssignmentIds).ToList ();
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", roomIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> roomIds)
			{
			if (roomIds == null)
				{
				roomIds = new List<int> { };
				}

			var remainingRoomIds = new List<int> ();
			if (roomIds.Any () && AssignmentIds.Any ())
				{
				remainingRoomIds = AssignmentIds.Where (id => !roomIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingRoomIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayDict = daySchedule as Dictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TEXT_DEGREESC, out var tempObj) && tempObj is List<object> temps)
				{
				for (int i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i]).ToString ("D4");
					var time = DateTime.ParseExact (timeValue, "HHmm", null).ToString ("HH:mm");

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, time },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_TEMP,
								  WiserTemperatureFunctions.FromWiserTemp(Convert.ToInt32(temps[i])) }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<string> ();
			var temps = new List<int> ();

			foreach (var item in daySchedule)
				{
				if (item.TryGetValue (Constants.TEXT_TIME, out var timeValue))
					{
					string time = timeValue.ToString ().Replace (":", "");
					times.Add (time);
					}

				if (item.TryGetValue (Constants.TEXT_TEMP, out var tempValue) || item.TryGetValue (Constants.TEXT_SETPOINT, out tempValue))
					{
					double temp;
					if (tempValue.ToString ().ToLower () == Constants.TEXT_OFF.ToLower ())
						{
						temp = Constants.TEMP_OFF;
						}
					else
						{
						temp = Convert.ToDouble (tempValue);
						}

					temps.Add (WiserTemperatureFunctions.ToWiserTemp (temp));
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TEXT_TIME, times },
					 { Constants.TEXT_DEGREESC, temps }
				};
			}

		protected override Dictionary<string, object> ConvertFromWiserSchedule (Dictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name },
					 { "Description", $"{ScheduleType} schedule for {Name}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override Dictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new Dictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserOnOffSchedule : WiserSchedule
		{
		private readonly List<int> _deviceTypeIds = new List<int> ();

		public WiserOnOffSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
										 Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public List<int> DeviceTypeIds => _deviceTypeIds;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			if (includeCurrent)
				{
				deviceIds = deviceIds.Concat (AssignmentIds).ToList ();
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> deviceIds)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Any () && AssignmentIds.Any ())
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingDeviceIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayList = daySchedule as List<object>;

			if (dayList != null)
				{
				foreach (var item in dayList)
					{
					int timeValue = Convert.ToInt32 (item);
					int absTime = Math.Abs (timeValue);

					if (absTime > 2400)
						{
						absTime = 0;
						}

					var time = absTime.ToString ("D4");
					var formattedTime = DateTime.ParseExact (time, "HHmm", null).ToString ("HH:mm");

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, formattedTime },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_STATE,
								  timeValue == absTime ? Constants.TEXT_ON : Constants.TEXT_OFF }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();

			foreach (var entry in daySchedule)
				{
				try
					{
					int time = 0;

					if (entry.TryGetValue ("Time", out var timeValue) && IsValidTime (timeValue.ToString ()))
						{
						time = int.Parse (timeValue.ToString ().Replace (":", ""));
						time = time != 0 ? time : 2400;
						}

					if ((entry.TryGetValue ("State", out var stateValue) || entry.TryGetValue (Constants.TEXT_SETPOINT, out stateValue)) &&
						 stateValue.ToString ().Title () == Constants.TEXT_OFF)
						{
						time = time != 0 ? -Math.Abs (time) : -2400;
						}

					times.Add (time);
					}
				catch (Exception ex)
					{
					_LOGGER.Error ($"Error in ConvertYamlToWiserDay: {ex.Message}");
					times.Add (0);
					}
				}

			return times;
			}

		// Fix for CS0029: Adjusting the return type to match the expected type in the method.

		protected override Dictionary<string, object> ConvertFromWiserSchedule (Dictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
			  {
					{ "Name", Name },
					{ "Description", $"{ScheduleType} schedule for {Name}" },
					{ "Type", ScheduleType }
			  };

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override Dictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new Dictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserLevelSchedule : WiserSchedule
		{
		public WiserLevelSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
										 Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public string LevelType => _scheduleData.TryGetValue ("Type", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public int LevelTypeId => LevelType == WiserScheduleTypeEnum.Shutters.ToString () ? 2 : 1;

		public new WiserScheduleNext Next
			{
			get
				{
				if (_scheduleData.TryGetValue ("Next", out var next))
					{
					if (next is Dictionary<string, object> nextDict)
						{
						return new WiserScheduleNext (_type, nextDict);
						}
					return new WiserScheduleNext (_type, new Dictionary<string, object> { { "Day", "" }, { "Time", 0 }, { "Level", 0 } });
					}
				return null;
				}
			}

		public new Dictionary<string, object> ScheduleData
			{
			get
				{
				var scheduleData = RemoveScheduleElements (new Dictionary<string, object> (_scheduleData));
				if (scheduleData.Count > 0)
					{
					return scheduleData;
					}
				// Fix: Flattening the nested dictionary to match the expected return type.
				return Constants.DEFAULT_LEVEL_SCHEDULE.ToDictionary (kvp => kvp.Key, kvp => (object)kvp.Value);
				}
			}

		public new string ScheduleType => LevelType;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			if (includeCurrent)
				{
				deviceIds = deviceIds.Concat (AssignmentIds).ToList ();
				}

			var typeData = new Dictionary<string, object>
				{
					 { "id", Id },
					 { "Name", Name },
					 { "Type", LevelTypeId }
				};

			foreach (var kvp in ScheduleData)
				{
				typeData[kvp.Key] = kvp.Value;
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { _type, typeData }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> deviceIds)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Any () && AssignmentIds.Any ())
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingDeviceIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayDict = daySchedule as Dictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TEXT_LEVEL, out var levelObj) && levelObj is List<object> levels)
				{
				for (int i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i]);
					string timeStr;

					if (Constants.SPECIAL_TIMES.ContainsValue (timeValue))
						{
						if (replaceSpecialTimes)
							{
							timeStr = timeValue == Constants.SPECIAL_TIMES["Sunrise"]
								 ? _sunrises[day]
								 : _sunsets[day];
							}
						else
							{
							timeStr = Constants.SPECIAL_TIMES.FirstOrDefault (x => x.Value == timeValue).Key;
							}
						}
					else
						{
						timeStr = DateTime.ParseExact (timeValue.ToString ("D4"), "HHmm", null).ToString ("HH:mm");
						}

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, timeStr },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_LEVEL, levels[i] }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();
			var levels = new List<int> ();

			foreach (var entry in daySchedule)
				{
				foreach (var kvp in entry)
					{
					if (kvp.Key.Title () == Constants.TEXT_TIME)
						{
						int time;
						if (Constants.SPECIAL_TIMES.ContainsKey (kvp.Value.ToString ().Title ()))
							{
							time = Constants.SPECIAL_TIMES[kvp.Value.ToString ().Title ()];
							}
						else
							{
							if (IsValidTime (kvp.Value.ToString ()))
								{
								time = int.Parse (kvp.Value.ToString ().Replace (":", ""));
								}
							else
								{
								time = 0;
								}
							}
						times.Add (time);
						}
					if (kvp.Key.Title () == Constants.TEXT_LEVEL || kvp.Key.Title () == Constants.TEXT_SETPOINT)
						{
						levels.Add (Convert.ToInt32 (kvp.Value));
						}
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TEXT_TIME, times },
					 { Constants.TEXT_LEVEL, levels }
				};
			}

		protected override Dictionary<string, object> ConvertFromWiserSchedule (Dictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name },
					 { "Description", $"{ScheduleType} schedule for {Name}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override Dictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new Dictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

#if HEATACTUATOR
	public class WiserHeatingActuator : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERHEATINGACTUATOR, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_OFF);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF, "current");

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : 0;

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new { DeviceLockEnabled = value }, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new { Identify = value }, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : 0;

		public string OutputType => _deviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserHeatingActuatorCollection
		{
		private readonly List<WiserHeatingActuator> _heatingActuators = new List<WiserHeatingActuator> ();

		public List<WiserHeatingActuator> All => _heatingActuators;

		public int Count => _heatingActuators.Count;

		public WiserHeatingActuator GetById (int id)
			{
			return _heatingActuators.FirstOrDefault (actuator => actuator.Id == id);
			}
		}
#endif

#if SHUTTER
	public class WiserShutter : WiserElectricalLevelDevice
		{
		public class WiserLiftMovementRange
			{
			private readonly WiserShutter _shutterInstance;
			private readonly Dictionary<string, object> _data;

			public WiserLiftMovementRange (WiserShutter shutterInstance, Dictionary<string, object> data)
				{
				_shutterInstance = shutterInstance;
				_data = data;
				}

			public int? OpenTime => _data?.TryGetValue ("LiftOpenTime", out var time) == true ? (int?)Convert.ToInt32 (time) : null;

			public int? CloseTime => _data?.TryGetValue ("LiftCloseTime", out var time) == true ? (int?)Convert.ToInt32 (time) : null;

			public async Task SetOpenTimeAsync (int time)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = time, LiftCloseTime = CloseTime }).ConfigureAwait (false);
				}

			public async Task SetCloseTimeAsync (int time)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = OpenTime, LiftCloseTime = time }).ConfigureAwait (false);
				}
			}

		private readonly WiserRestController _wiserRestController;
		private readonly WiserSchedule _schedule;
		private string _awayAction;
		private string _mode;
		private string _name;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserShutter (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (data, deviceTypeData)
			{
			_wiserRestController = wiserRestController;
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", ShutterId }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSHUTTER, ShutterId);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		private bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		private bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterModeEnum))
			 .Cast<WiserShutterModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayActionEnum))
			 .Cast<WiserAwayActionEnum> ()
			 .Where (a => a == WiserAwayActionEnum.Close || a == WiserAwayActionEnum.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (ValidateAwayAction (value))
					{
					if (SendCommandAsync (new { AwayAction = value }).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Shutter away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public int CurrentLift => _deviceTypeData.TryGetValue ("CurrentLift", out var lift) ? Convert.ToInt32 (lift) : 0;

		public async Task SetCurrentLiftAsync (int percentage)
			{
			if (percentage >= 0 && percentage <= 100)
				{
				await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = percentage } }).ConfigureAwait (false);
				}
			else
				{
				throw new ArgumentException ("Shutter percentage must be between 0 and 100");
				}
			}

		public WiserLiftMovementRange DriveConfig
			{
			get
				{
				if (_deviceTypeData.TryGetValue ("DriveConfig", out var config) && config is Dictionary<string, object> configDict)
					{
					return new WiserLiftMovementRange (this, configDict);
					}
				return new WiserLiftMovementRange (this, null);
				}
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new { Identify = value }, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public bool IsOpen => CurrentLift == 100;

		public bool IsClosed => CurrentLift == 0;

		public bool IsClosing => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Closing";

		public bool IsOpening => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Opening";

		public bool IsStopped => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Stopped";

		public bool IsMoving => !IsStopped;

		public string LiftMovement => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) ? movement.ToString () : Constants.TEXT_UNKNOWN;

		public int ManualLift => _deviceTypeData.TryGetValue ("ManualLift", out var lift) ? Convert.ToInt32 (lift) : 0;

		public string Mode
			{
			get => _mode;
			set
				{
				if (ValidateMode (value))
					{
					if (SendCommandAsync (new { Mode = value }).Result)
						{
						_mode = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Shutter mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		new public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new { Name = value }).Result)
					{
					_name = value;
					}
				}
			}

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public string ScheduledLift => _deviceTypeData.TryGetValue ("ScheduledLift", out var lift) ? lift.ToString () : Constants.TEXT_UNKNOWN;

		public int ShutterId => _deviceTypeData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public int TargetLift => _deviceTypeData.TryGetValue ("TargetLift", out var lift) ? Convert.ToInt32 (lift) : 0;

		public async Task OpenAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 100 } }).ConfigureAwait (false);
			}

		public async Task CloseAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 0 } }).ConfigureAwait (false);
			}

		public async Task StopAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "Stop" } }).ConfigureAwait (false);
			}
		}

	public class WiserShutterCollection
		{
		private readonly List<WiserShutter> _shutters = new List<WiserShutter> ();

		public List<WiserShutter> All => _shutters;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterModeEnum))
			 .Cast<WiserShutterModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _shutters.Count;

		public WiserShutter GetById (int id)
			{
			return _shutters.FirstOrDefault (shutter => shutter.Id == id);
			}

		public WiserShutter GetByShutterId (int shutterId)
			{
			return _shutters.FirstOrDefault (shutter => shutter.ShutterId == shutterId);
			}

		public List<WiserShutter> GetByRoomId (int roomId)
			{
			return _shutters.Where (shutter => shutter.RoomId == roomId).ToList ();
			}
		}
#endif

	public class WiserSmartPlug : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private readonly WiserSchedule _schedule;
		private string _awayAction;
		private string _mode;
		private string _name;
		private bool _deviceLockEnabled;
		private string _outputState;
		private bool _identifyActive;

		public WiserSmartPlug (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_outputState = deviceTypeData.TryGetValue ("OutputState", out var state) ? state.ToString () : Constants.TEXT_OFF;
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSMARTPLUG, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		private bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		private bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugModeEnum))
			 .Cast<WiserSmartPlugModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayActionEnum))
			 .Cast<WiserAwayActionEnum> ()
			 .Where (a => a == WiserAwayActionEnum.Off || a == WiserAwayActionEnum.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (ValidateAwayAction (value))
					{
					if (SendCommandAsync (new
						{
						AwayAction = value
						}).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Smart Plug away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : -1;

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : -1;

		public string ManualState => _deviceTypeData.TryGetValue ("ManualState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public string Mode
			{
			get => _mode;
			set
				{
				if (ValidateMode (value))
					{
					if (SendCommandAsync (new
						{
						Mode = value
						}).Result)
						{
						_mode = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Smart Plug mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		new public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new
					{
					Name = value
					}).Result)
					{
					_name = value;
					}
				}
			}

		public bool IsOn => _outputState == Constants.TEXT_ON;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public string ScheduledState => _deviceTypeData.TryGetValue ("ScheduledState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public async Task<bool> TurnOnAsync ()
			{
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TEXT_ON
				}).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TEXT_ON;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TEXT_OFF
				}).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TEXT_OFF;
				}
			return result;
			}
		}

	public class WiserSmartPlugCollection
		{
		private readonly List<WiserSmartPlug> _smartPlugs = new List<WiserSmartPlug> ();

		public List<WiserSmartPlug> All => _smartPlugs;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugModeEnum))
			 .Cast<WiserSmartPlugModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _smartPlugs.Count;

		public WiserSmartPlug GetById (int id)
			{
			return _smartPlugs.FirstOrDefault (plug => plug.Id == id);
			}
		}

#if LIGHT
	public class WiserLight : WiserElectricalLevelDevice
		{
		protected readonly WiserRestController _wiserRestController;
		protected readonly WiserSchedule _schedule;
		protected string _awayAction;
		protected string _mode;
		protected string _name;
		protected bool _deviceLockEnabled;
		protected string _currentState;
		protected bool _identifyActive;

		public WiserLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (data, deviceTypeData)
			{
			_wiserRestController = wiserRestController;
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_currentState = deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : Constants.TEXT_OFF;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", LightId }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		protected void SendCommand (object cmd, bool deviceLevel = false)
			{
			SendCommandAsync (cmd, deviceLevel).Wait ();
			}

		protected async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERLIGHT, LightId);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		protected bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		protected bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserLightModeEnum))
			 .Cast<WiserLightModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayActionEnum))
			 .Cast<WiserAwayActionEnum> ()
			 .Where (a => a == WiserAwayActionEnum.Off || a == WiserAwayActionEnum.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (ValidateAwayAction (value))
					{
					if (SendCommandAsync (new { AwayAction = value }).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Light away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public string CurrentState => _deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : "0";

		public bool Identify
			{
			get => _identifyActive;
			set
				{
				if (SendCommandAsync (new { Identify = value }, true).Result)
					{
					_identifyActive = value;
					}
				}
			}

		public bool IsDimmable => _deviceTypeData.TryGetValue ("IsDimmable", out var dimmable) && Convert.ToBoolean (dimmable);

		public bool IsOn => _currentState == Constants.TEXT_ON;

		public int LightId => _deviceTypeData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Mode
			{
			get => _mode;
			set
				{
				if (ValidateMode (value))
					{
					if (SendCommandAsync (new { Mode = value }).Result)
						{
						_mode = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Light mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		new public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new { Name = value }).Result)
					{
					_name = value;
					}
				}
			}

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public int TargetState => _deviceTypeData.TryGetValue ("TargetState", out var state) ? Convert.ToInt32 (state) : 0;

		public async Task<bool> TurnOnAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_ON } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TEXT_ON;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_OFF } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TEXT_OFF;
				}
			return result;
			}
		}

	public class WiserDimmableLight : WiserLight
		{
		public class WiserOutputRange
			{
			private readonly Dictionary<string, object> _data;

			public WiserOutputRange (Dictionary<string, object> data)
				{
				_data = data;
				}

			public int? Minimum => _data?.TryGetValue ("Minimum", out var min) == true ? (int?)Convert.ToInt32 (min) : null;

			public int? Maximum => _data?.TryGetValue ("Maximum", out var max) == true ? (int?)Convert.ToInt32 (max) : null;
			}

		public WiserDimmableLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData, schedule)
			{
			}

		public int CurrentLevel => _deviceTypeData.TryGetValue ("CurrentLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public int CurrentPercentage
			{
			get => _deviceTypeData.TryGetValue ("CurrentPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;
			set
				{
				if (value >= 0 && value <= 100)
					{
					SendCommand (new { RequestOverride = new { State = Constants.TEXT_ON, Percentage = value } });
					}
				else
					{
					throw new ArgumentException ("Brightness level percentage must be between 0 and 100");
					}
				}
			}

		public int ManualLevel => _deviceTypeData.TryGetValue ("ManualLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public int OverrideLevel => _deviceTypeData.TryGetValue ("OverrideLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public WiserOutputRange OutputRange
			{
			get
				{
				if (_deviceTypeData.TryGetValue ("OutputRange", out var range) && range is Dictionary<string, object> rangeDict)
					{
					return new WiserOutputRange (rangeDict);
					}
				return new WiserOutputRange (null);
				}
			}

		public int ScheduledPercentage => _data.TryGetValue ("ScheduledPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;

		public int TargetPercentage => _deviceTypeData.TryGetValue ("TargetPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;
		}

	public class WiserLightCollection
		{
		private readonly List<WiserLight> _lights = new List<WiserLight> ();

		public List<WiserLight> All => _lights;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserLightModeEnum))
			 .Cast<WiserLightModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _lights.Count;

		public List<WiserDimmableLight> DimmableLights => _lights.OfType<WiserDimmableLight> ().ToList ();

		public List<WiserLight> OnOffLights => _lights.Where (light => !light.IsDimmable).ToList ();

		public WiserLight GetById (int id)
			{
			return _lights.FirstOrDefault (light => light.Id == id);
			}

		public WiserLight GetByLightId (int lightId)
			{
			return _lights.FirstOrDefault (light => light.LightId == lightId);
			}

		public List<WiserLight> GetByRoomId (int roomId)
			{
			return _lights.Where (light => light.RoomId == roomId).ToList ();
			}
		}
#endif
	public class WiserMoment
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _momentData;

		public WiserMoment (WiserRestController wiserRestController, Dictionary<string, object> momentData)
			{
			_wiserRestController = wiserRestController;
			_momentData = momentData;
			}

		private async Task<bool> SendCommandAsync (object cmd)
			{
			bool result = await _wiserRestController.SendCommandAsync (RestConstants.WISERSYSTEM, cmd).ConfigureAwait (false);
			return result;
			}

		public int Id => _momentData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Name => _momentData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

		public async Task<bool> ActivateAsync ()
			{
			return await SendCommandAsync (new
				{
				TriggerMoment = Id
				}).ConfigureAwait (false);
			}
		}

	public class WiserHeatingChannel
		{
		private readonly Dictionary<string, object> _data;

		public WiserHeatingChannel (Dictionary<string, object> data)
			{
			_data = data;
			}

		public string DemandOnOffOutput => _data.TryGetValue ("DemandOnOffOutput", out var output) ? output.ToString () : Constants.TEXT_UNKNOWN;

		public string HeatingRelayStatus => _data.TryGetValue ("HeatingRelayState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public bool IsSmartValvePreventingDemand => _data.TryGetValue ("IsSmartValvePreventingDemand", out var preventing) && Convert.ToBoolean (preventing);

		public string Name => _data.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

		public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		public List<int> RoomIds => _data.TryGetValue ("RoomIds", out var roomIds) && roomIds is List<object> roomIdsList
			 ? roomIdsList.Select (id => Convert.ToInt32 (id)).ToList ()
			 : new List<int> ();
		}

	public class WiserUFHRelay
		{
		public int DemandPercentage
			{
			get;
			}
		public bool Polarity
			{
			get;
			}
		public int Id
			{
			get;
			}

		public WiserUFHRelay (Dictionary<string, object> relayData)
			{
			DemandPercentage = relayData.TryGetValue ("DemandPercentage", out var demand) ? Convert.ToInt32 (demand) : 0;
			Polarity = relayData.TryGetValue ("Polarity", out var polarity) && Convert.ToBoolean (polarity);
			Id = relayData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;
			}
		}

	public class WiserUFHController : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;
		private readonly List<WiserUFHRelay> _relays = new List<WiserUFHRelay> ();

		public WiserUFHController (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = false;
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			if (deviceTypeData.TryGetValue ("Relays", out var relays) && relays is List<object> relaysList)
				{
				foreach (var relay in relaysList)
					{
					if (relay is Dictionary<string, object> relayDict)
						{
						_relays.Add (new WiserUFHRelay (relayDict));
						}
					}
				}
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERUFHCONTROLLER, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF, "current");

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}


		public bool? DewDetected => _deviceTypeData.TryGetValue ("DewDetected", out var detected) ? (bool?)Convert.ToBoolean (detected) : null;

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public bool? InterlockActive => _deviceTypeData.TryGetValue ("InterlockActive", out var active) ? (bool?)Convert.ToBoolean (active) : null;

		public bool? IsFullStrip => _deviceTypeData.TryGetValue ("IsFullStrip", out var fullStrip) ? (bool?)Convert.ToBoolean (fullStrip) : null;

		public int MaxFloorTemperature => _deviceTypeData.TryGetValue ("MaxHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_MAXIMUM;

		public int MinFloorTemperature => _deviceTypeData.TryGetValue ("MinHeatFloorTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF;

		new public string Name => _deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

		public string OutputType => _deviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public List<WiserUFHRelay> Relays => _relays;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserUFHControllerCollection
		{
		private readonly List<WiserUFHController> _ufhControllers = new List<WiserUFHController> ();

		public List<WiserUFHController> All => _ufhControllers;

		public int Count => _ufhControllers.Count;

		public WiserUFHController GetById (int id)
			{
			return _ufhControllers.FirstOrDefault (controller => controller.Id == id);
			}
		}

	public class WiserRoom
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _data;
		private WiserSchedule _schedule;
		private readonly List<WiserDevice> _devices;
		private string _mode;
		private string _name;
		private bool _windowDetectionActive;

		public WiserRoom (WiserRestController wiserRestController, Dictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			_wiserRestController = wiserRestController;
			_data = room;
			_schedule = schedule;
			_devices = devices;
			_mode = EffectiveHeatingMode (
				 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : "",
				 CurrentTargetTemperature
			);
			_name = room.TryGetValue ("Name", out var name) ? name.ToString () : "";
			_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				}
			}
		public void Update (Dictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			var oldId = Id;
			var oldName = Name;

			_data.Clear ();
			foreach (var kvp in room)
				{
				_data[kvp.Key] = kvp.Value;
				}
			_schedule = schedule;
			_devices.Clear ();
			foreach (var device in devices)
				{
				_devices.Add (device);
				}
			_mode = EffectiveHeatingMode (
				 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : "",
				 CurrentTargetTemperature
				);

			_name = room.TryGetValue ("Name", out var name) ? name.ToString () : "";
			_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection);

			// Add device id to schedule
			if (_schedule != null)
				{
				if ( _schedule.Assignments.Count == 0 ||oldId != Id || oldName != Name || _schedule.Assignments.Any (a => (int)a["id"] == oldId || (string)a["name"] == oldName))
					{
					// Remove old assignment if the id or name has changed
					_schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
					_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
					}
				}
			}

		private string EffectiveHeatingMode (string mode, double temp)
			{
			if (mode.Equals (Constants.TEXT_MANUAL, StringComparison.OrdinalIgnoreCase) && temp == Constants.TEMP_OFF)
				{
				return WiserHeatingModeEnum.Off.ToString ();
				}
			else if (mode.Equals (Constants.TEXT_MANUAL, StringComparison.OrdinalIgnoreCase))
				{
				return WiserHeatingModeEnum.Manual.ToString ();
				}
			return WiserHeatingModeEnum.Auto.ToString ();
			}

		private async Task<bool> SendCommandAsync (object cmd, WiserRestActionEnum method = WiserRestActionEnum.PATCH)
			{
			string url = string.Format (RestConstants.WISERROOM, Id);
			bool result = await _wiserRestController.SendCommandAsync (url, cmd, method).ConfigureAwait (false);
			return result;
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserHeatingModeEnum))
			 .Cast<WiserHeatingModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && Convert.ToBoolean (suppressed);

		public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && Convert.ToInt32 (time) > 0
			 ? DateTimeOffset.FromUnixTimeSeconds (Convert.ToInt32 (time)).DateTime
			 : DateTime.MinValue;

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTime - DateTime.Now).TotalSeconds
			 : 0;

		public int ComfortModeScore => _data.TryGetValue ("ComfortModeScore", out var score) ? Convert.ToInt32 (score) : 0;

		public string ControlDirection => _data.TryGetValue ("ControlDirection", out var direction) ? direction.ToString () : Constants.TEXT_UNKNOWN;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CurrentSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_MINIMUM);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CalculatedTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_MINIMUM, "current");

		public int? CurrentHumidity
			{
			get
				{
				foreach (var device in Devices)
					{
					if (device is WiserRoomStat roomStat)
						{
						return roomStat.CurrentHumidity;
						}
					}
				return null;
				}
			}

		public string DemandType => _data.TryGetValue ("DemandType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public List<WiserDevice> Devices => _devices;

		public double DisplayedSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("DisplayedSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_MINIMUM, "current");

		public List<int> HeatingActuatorIds => _data.TryGetValue ("HeatingActuatorIds", out var ids) && ids is List<object> idsList
			 ? idsList.Select (id => Convert.ToInt32 (id)).OrderBy (id => id).ToList ()
			 : new List<int> ();

		public string HeatingRate => _data.TryGetValue ("HeatingRate", out var rate) ? rate.ToString () : Constants.TEXT_UNKNOWN;

		public string HeatingType => _data.TryGetValue ("HeatingType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public bool IsAwayMode => _data.TryGetValue ("SetpointOrigin", out var origin) && origin.ToString ().Contains ("Away") ||
										 _data.TryGetValue ("SetPointOrigin", out var origin2) && origin2.ToString ().Contains ("Away");

		public bool IsBoost => _data.TryGetValue ("SetpointOrigin", out var origin) && origin.ToString ().Contains ("Boost") ||
									 _data.TryGetValue ("SetPointOrigin", out var origin2) && origin2.ToString ().Contains ("Boost");

		public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
										 type.ToString () != Constants.TEXT_UNKNOWN &&
										 type.ToString () != Constants.TEXT_NONE;

		public bool IsHeating => _data.TryGetValue ("ControlOutputState", out var state) && state.ToString () == Constants.TEXT_ON;

		public double ManualTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ManualSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_MINIMUM);

		public string Mode
			{
			get => _mode;
			set
				{
				try
					{
					WiserHeatingModeEnum mode = (WiserHeatingModeEnum)Enum.Parse (typeof (WiserHeatingModeEnum), value, true);

					// Cancel any overrides on mode change
					if (IsOverride)
						{
						CancelOverridesAsync ().Wait ();
						}

					if (mode == WiserHeatingModeEnum.Off)
						{
						SetManualTemperatureAsync (Constants.TEMP_OFF).Wait ();
						}
					else if (mode == WiserHeatingModeEnum.Manual)
						{
						if (SendCommandAsync (new
							{
							Mode = WiserHeatingModeEnum.Manual.ToString ()
							}).Result)
							{
							if (CurrentTargetTemperature == Constants.TEMP_OFF)
								{
								SetTargetTemperatureAsync (ScheduledTargetTemperature).Wait ();
								}
							}
						}
					else if (mode == WiserHeatingModeEnum.Auto)
						{
						SendCommandAsync (new
							{
							Mode = WiserHeatingModeEnum.Auto.ToString ()
							}).Wait ();
						}

					_mode = mode.ToString ();
					}
				catch (ArgumentException)
					{
					throw new ArgumentException ($"{value} is not a valid Heating mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new
					{
					Name = value.Title ()
					}).Result)
					{
					_name = value.Title ();
					}
				}
			}

		public int NumberOfHeatingActuators => HeatingActuatorIds.Count;

		public int NumberOfSmartvalves => SmartvalveIds.Count;

		public double OverrideTargetTemperature => _data.TryGetValue ("OverrideSetpoint", out var setPoint) ? Convert.ToDouble (setPoint) / 10 : 0;

		public string OverrideType => _data.TryGetValue ("OverrideType", out var type) ? type.ToString () : Constants.TEXT_NONE;

		public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		public int? RoomstatId => _data.TryGetValue ("RoomStatId", out var id) ? (int?)Convert.ToInt32 (id) : null;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public double ScheduledTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ScheduledSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_MINIMUM);

		public List<int> SmartvalveIds => _data.TryGetValue ("SmartValveIds", out var ids) && ids is List<object> idsList
			 ? idsList.Select (id => Convert.ToInt32 (id)).OrderBy (id => id).ToList ()
			 : new List<int> ();

		public string TargetTemperatureOrigin => _data.TryGetValue ("SetpointOrigin", out var origin)
			 ? origin.ToString ()
			 : _data.TryGetValue ("SetPointOrigin", out var origin2)
				  ? origin2.ToString ()
				  : Constants.TEXT_UNKNOWN;

		public int? UnderfloorHeatingId => _data.TryGetValue ("UnderFloorHeatingId", out var id) ? (int?)Convert.ToInt32 (id) : null;

		public List<int> UnderfloorHeatingRelayIds => _data.TryGetValue ("UfhRelayIds", out var ids) && ids is List<object> idsList
			 ? idsList.Select (id => Convert.ToInt32 (id)).OrderBy (id => id).ToList ()
			 : new List<int> ();

		public bool WindowDetectionActive
			{
			get => _windowDetectionActive;
			set
				{
				if (SendCommandAsync (new
					{
					WindowDetectionActive = value
					}).Result)
					{
					_windowDetectionActive = value;
					}
				}
			}

		public string WindowState => _data.TryGetValue ("WindowState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public async Task<bool> DeleteAsync ()
			{
			return await SendCommandAsync (null, WiserRestActionEnum.DELETE).ConfigureAwait (false);
			}

		public async Task<bool> BoostAsync (double incTemp, int duration)
			{
			if (duration == 0)
				{
				return await CancelBoostAsync ().ConfigureAwait (false);
				}
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = "Boost",
					DurationMinutes = duration,
					IncreaseSetPointBy = WiserTemperatureFunctions.ToWiserTemp (incTemp, "delta")
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> CancelBoostAsync ()
			{
			if (IsBoost)
				{
				return await CancelOverridesAsync ().ConfigureAwait (false);
				}
			return true;
			}

		public async Task<bool> SetTargetTemperatureAsync (double temp)
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_MANUAL,
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> SetTargetTemperatureForDurationAsync (double temp, int duration)
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_MANUAL,
					DurationMinutes = duration,
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> SetTargetTemperatureForDurationOfScheduleAsync (double temp)
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_MANUAL,
					DurationMinutes = (int)Math.Ceiling ((Schedule.Next.DateTime - DateTime.Now).TotalMinutes),
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> SetManualTemperatureAsync (double temp)
			{
			if (Mode != WiserHeatingModeEnum.Manual.ToString ())
				{
				Mode = WiserHeatingModeEnum.Manual.ToString ();
				}
			return await SetTargetTemperatureAsync (temp).ConfigureAwait (false);
			}

		public async Task<bool> ScheduleAdvanceAsync ()
			{
			if (await CancelBoostAsync ().ConfigureAwait (false))
				{
				return await SetTargetTemperatureAsync (Convert.ToDouble (Schedule.Next.Setting)).ConfigureAwait (false);
				}
			return false;
			}

		public async Task<bool> CancelOverridesAsync ()
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_NONE
					}
				}).ConfigureAwait (false);
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

	public static class StringExtensions
		{
		public static string Title (this string str)
			{
			if (string.IsNullOrWhiteSpace (str))
				return str;
			return CultureInfo.CurrentCulture.TextInfo.ToTitleCase (str.ToLower ());
			}

		public static string Capitalize (this string str)
			{
			if (string.IsNullOrWhiteSpace (str))
				return str;
			return char.ToUpper (str[0]) + str.Substring (1).ToLower ();
			}
		}
	}

// -----

