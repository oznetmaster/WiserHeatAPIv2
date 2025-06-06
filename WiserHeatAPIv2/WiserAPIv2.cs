// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

