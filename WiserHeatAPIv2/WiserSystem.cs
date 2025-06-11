// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserSystem
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _data;
		private Dictionary<string, object> _systemData;
		private WiserHubCapabilitiesInfo _capabilityData;
		private WiserCloud _cloudData;
		private Dictionary<string, object> _deviceData;
		private WiserNetwork _networkData;
		private WiserOpentherm _openthermData;
		private WiserSignalStrength _signal;
		private WiserFirmwareUpgradeInfo _upgradeData;
		private WiserZigbee _zigbeeData;

		private bool _automaticDaylightSaving;
		private bool _awayModeAffectsHotwater;
		private int _awayModeTargetTemperature;
		private bool _comfortModeEnabled;
		private int _degradedModeTargetTemperature;
		private DateTime _hubTime;
		private string _overrideType;
		private int _timezoneOffset;
		private bool _valveProtectionEnabled;

		public WiserSystem (WiserRestController wiserRestController, Dictionary<string, object> domainData,
								Dictionary<string, object> networkData, List<Dictionary<string, object>> deviceData,
								Dictionary<string, object> openthermData)
			{
			_wiserRestController = wiserRestController;
			_data = domainData;

			Build (networkData, deviceData, openthermData);
			}

		private void Build (Dictionary<string, object> networkData, List<Dictionary<string, object>> deviceData, Dictionary<string, object> openthermData)
			{
			_systemData = _data.TryGetValue ("System", out var system) && system is Dictionary<string, object> systemDict
				 ? systemDict : new Dictionary<string, object> ();

			// Sub classes for system setting values
			_capabilityData = new WiserHubCapabilitiesInfo (_data.TryGetValue ("DeviceCapabilityMatrix", out var matrix) && matrix is Dictionary<string, object> matrixDict
				 ? matrixDict : new Dictionary<string, object> ());
			_cloudData = new WiserCloud (_systemData.TryGetValue ("CloudConnectionStatus", out var status) ? status.ToString () : "",
				 _data.TryGetValue ("Cloud", out var cloud) && cloud is Dictionary<string, object> cloudDict
				 ? cloudDict : new Dictionary<string, object> ());
			_deviceData = GetSystemDevice (/*deviceData.TryGetValue ("Device", out var devices) && devices is List<object> deviceList
				 ? deviceList.Cast<Dictionary<string, object>> ().ToList ()
				 : new List<Dictionary<string, object>> ()*/ deviceData);
			_networkData = new WiserNetwork (networkData.TryGetValue ("Station", out var station) && station is Dictionary<string, object> stationDict
				 ? stationDict : new Dictionary<string, object> ());
			_openthermData = new WiserOpentherm (openthermData,
				 _systemData.TryGetValue ("OpenThermConnectionStatus", out var otStatus) ? otStatus.ToString () : Constants.TEXT_UNKNOWN);
			_signal = new WiserSignalStrength (_deviceData);
			_upgradeData = new WiserFirmwareUpgradeInfo (_data.TryGetValue ("UpgradeInfo", out var upgrade) && upgrade is List<object> upgradeList
				 ? upgradeList.Cast<Dictionary<string, object>> ().ToList () : new List<Dictionary<string, object>> ());
			_zigbeeData = new WiserZigbee (_data.TryGetValue ("Zigbee", out var zigbee) && zigbee is Dictionary<string, object> zigbeeDict
				 ? zigbeeDict : new Dictionary<string, object> ());

			// Variables to hold values for settable values
			_automaticDaylightSaving = _systemData.TryGetValue ("AutomaticDaylightSaving", out var ads) && Convert.ToBoolean (ads);
			_awayModeAffectsHotwater = _systemData.TryGetValue ("AwayModeAffectsHotWater", out var amah) && Convert.ToBoolean (amah);
			_awayModeTargetTemperature = _systemData.TryGetValue ("AwayModeSetPointLimit", out var amtl) ? Convert.ToInt32 (amtl) : 0;
			_comfortModeEnabled = _systemData.TryGetValue ("ComfortModeEnabled", out var cme) && Convert.ToBoolean (cme);
			_degradedModeTargetTemperature = _systemData.TryGetValue ("DegradedModeSetpointThreshold", out var dmst) ? Convert.ToInt32 (dmst) : 0;
			_hubTime = _systemData.TryGetValue ("UnixTime", out var time) ? DateTimeOffset.FromUnixTimeSeconds (Convert.ToInt32 (time)).DateTime : DateTime.Now;
			_overrideType = _systemData.TryGetValue ("OverrideType", out var ot) ? ot.ToString () : "";
			_timezoneOffset = _systemData.TryGetValue ("TimeZoneOffset", out var tzo) ? Convert.ToInt32 (tzo) : 0;
			_valveProtectionEnabled = _systemData.TryGetValue ("ValveProtectionEnabled", out var vpe) && Convert.ToBoolean (vpe);
			}

		public void Update (Dictionary<string, object> domainData, Dictionary<string, object> networkData, List<Dictionary<string, object>> deviceData, Dictionary<string, object> openthermData)
			{
			if (domainData != null)
				{
				_data.Clear ();
				foreach (var kv in domainData)
					_data[kv.Key] = kv.Value;
				}

			Build (networkData, deviceData, openthermData);
			}

		private Dictionary<string, object> GetSystemDevice (List<Dictionary<string, object>> deviceData)
			{
			foreach (var device in deviceData)
				{
				if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "Controller")
					{
					return device;
					}
				}
			return new Dictionary<string, object> ();
			}

		private async Task<bool> SendCommandAsync (object cmd, string path = null)
			{
			string url = path != null ? $"{RestConstants.WISERSYSTEM}/{path}" : RestConstants.WISERSYSTEM;
			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public string ActiveSystemVersion => _systemData.TryGetValue ("ActiveSystemVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

		public bool AutomaticDaylightSavingEnabled
			{
			get => _automaticDaylightSaving;
			set
				{
				if (SendCommandAsync (new
					{
					AutomaticDaylightSaving = value.ToString ().ToLower ()
					}).Result)
					{
					_automaticDaylightSaving = value;
					}
				}
			}

		public bool AwayModeEnabled
			{
			get => _overrideType == "Away";
			set
				{
				if (SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = value ? 2 : 0
						}
					}).Result)
					{
					_overrideType = value ? "Away" : "";
					}
				}
			}

		public bool AwayModeAffectsHotwater
			{
			get => _awayModeAffectsHotwater;
			set
				{
				if (SendCommandAsync (new
					{
					AwayModeAffectsHotWater = value.ToString ().ToLower ()
					}).Result)
					{
					_awayModeAffectsHotwater = value;
					}
				}
			}

		public double AwayModeTargetTemperature
			{
			get => WiserTemperatureFunctions.FromWiserTemp (_awayModeTargetTemperature);
			set
				{
				int temp = WiserTemperatureFunctions.ToWiserTemp (value);
				if (SendCommandAsync (new
					{
					AwayModeSetPointLimit = temp
					}).Result)
					{
					_awayModeTargetTemperature = temp;
					}
				}
			}

		public string BoilerFuelType => _systemData.TryGetValue ("BoilerSettings", out var settings) && settings is Dictionary<string, object> settingsDict
			 ? settingsDict.TryGetValue ("FuelType", out var fuelType) ? fuelType.ToString () : Constants.TEXT_UNKNOWN
			 : Constants.TEXT_UNKNOWN;

		public string BrandName => _systemData.TryGetValue ("BrandName", out var brand) ? brand.ToString () : null;

		public WiserHubCapabilitiesInfo Capabilities => _capabilityData;

		public WiserCloud Cloud => _cloudData;

		public bool ComfortModeEnabled
			{
			get => _comfortModeEnabled;
			set
				{
				if (SendCommandAsync (new
					{
					ComfortModeEnabled = value
					}).Result)
					{
					_comfortModeEnabled = value;
					}
				}
			}

		public double DegradedModeTargetTemperature
			{
			get => WiserTemperatureFunctions.FromWiserTemp (_degradedModeTargetTemperature);
			set
				{
				int temp = WiserTemperatureFunctions.ToWiserTemp (value);
				if (SendCommandAsync (new
					{
					DegradedModeSetpointThreshold = temp
					}).Result)
					{
					_degradedModeTargetTemperature = temp;
					}
				}
			}

		public bool EcoModeEnabled
			{
			get => _systemData.TryGetValue ("EcoModeEnabled", out var eco) && Convert.ToBoolean (eco);
			set
				{
				if (SendCommandAsync (new
					{
					EcoModeEnabled = value
					}).Result)
					{
					_systemData["EcoModeEnabled"] = value;
					}
				}
			}

		public bool FirmwareOverTheAirEnabled => _systemData.TryGetValue ("FotaEnabled", out var fota) && Convert.ToBoolean (fota);

		public string FirmwareVersion => _deviceData.TryGetValue ("ActiveFirmwareVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

		public WiserGPS GeoPosition => new WiserGPS (_systemData.TryGetValue ("GeoPosition", out var geo) && geo is Dictionary<string, object> geoDict
			 ? geoDict : new Dictionary<string, object> ());

		public int HardwareGeneration => _systemData.TryGetValue ("HardwareGeneration", out var gen) ? Convert.ToInt32 (gen) : 0;

		public bool HeatingButtonOverrideState => _systemData.TryGetValue ("HeatingButtonOverrideState", out var state) && state.ToString () == Constants.TEXT_ON;

		public bool HotwaterButtonOverrideState => _systemData.TryGetValue ("HotWaterButtonOverrideState", out var state) && state.ToString () == Constants.TEXT_ON;

		public DateTime HubTime => _hubTime;

		public int Id => _deviceData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public bool IsAwayModeEnabled => _overrideType == "Away";

		public string Model => _deviceData.TryGetValue ("ModelIdentifier", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

		public string Name => Network.Hostname;

		public WiserNetwork Network => _networkData;

		public int NodeId => _deviceData.TryGetValue ("NodeId", out var nodeId) ? Convert.ToInt32 (nodeId) : 0;

		public WiserOpentherm Opentherm => _openthermData;

		public string PairingStatus => _systemData.TryGetValue ("PairingStatus", out var status) ? status.ToString () : Constants.TEXT_UNKNOWN;

		public int ParentNodeId => _deviceData.TryGetValue ("ParentNodeId", out var nodeId) ? Convert.ToInt32 (nodeId) : 0;

		public string ProductType => _deviceData.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public WiserSignalStrength Signal => _signal;

		public Dictionary<string, string> SunriseTimes => _systemData.TryGetValue ("SunriseTimes", out var times) && times is List<object> timesList
			 ? SpecialTimes.SunriseTimes (timesList.Select (s => Convert.ToInt32 (s)).ToList ())
			 : new Dictionary<string, string> ();

		public Dictionary<string, string> SunsetTimes => _systemData.TryGetValue ("SunsetTimes", out var times) && times is List<object> timesList
			 ? SpecialTimes.SunsetTimes (timesList.Select (s => Convert.ToInt32 (s)).ToList ())
			 : new Dictionary<string, string> ();

		public string SystemMode => _systemData.TryGetValue ("SystemMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

		public int TimezoneOffset
			{
			get => _timezoneOffset;
			set
				{
				if (SendCommandAsync (new
					{
					TimeZoneOffset = value
					}).Result)
					{
					_timezoneOffset = value;
					}
				}
			}

		public bool UserOverridesActive => _systemData.TryGetValue ("UserOverridesActive", out var active) && Convert.ToBoolean (active);

		public bool ValveProtectionEnabled
			{
			get => _valveProtectionEnabled;
			set
				{
				if (SendCommandAsync (new
					{
					ValveProtectionEnabled = value
					}).Result)
					{
					_valveProtectionEnabled = value;
					}
				}
			}

		public WiserZigbee Zigbee => _zigbeeData;

		public async Task<bool> AllowAddDeviceAsync (int allowTime = 120)
			{
			return await SendCommandAsync (allowTime, "RequestPermitJoin").ConfigureAwait (false);
			}

		public async Task<bool> BoostAllRoomsAsync (double incTemp, int duration)
			{
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

		public async Task<bool> CancelAllOverridesAsync ()
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = "CancelUserOverrides"
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> ConnectToNetworkAsync (string ssid, string password, int? channel = null, string securityMode = null)
			{
			var cmdData = new Dictionary<string, object> { { "Enabled", true } };

			if (!string.IsNullOrEmpty (ssid) && !string.IsNullOrEmpty (password))
				{
				cmdData["SSID"] = ssid;
				cmdData["SecurityKey"] = password;
				}

			if (channel.HasValue)
				{
				cmdData["Channel"] = channel.Value;
				}

			if (!string.IsNullOrEmpty (securityMode))
				{
				cmdData["SecurityMode"] = securityMode;
				}

			return await _wiserRestController.SendCommandAsync ($"{RestConstants.WISERHUBNETWORK}/Station", cmdData).ConfigureAwait (false);
			}
		}
	}
