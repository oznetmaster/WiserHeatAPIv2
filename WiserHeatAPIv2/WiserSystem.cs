// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.RestConstants;

namespace WiserHeatApiV2;

/// <summary>
/// Represents the Wiser Hub system state, settings, capabilities, and network information.
/// </summary>
public class WiserSystem
	{
	private readonly WiserRestController _wiserRestController;
	private readonly IDictionary<string, object> _data;
	private Dictionary<string, object>? _systemData;
	private Dictionary<string, object>? _deviceData;
	private WiserFirmwareUpgradeInfo? _upgradeData;
	private bool _automaticDaylightSaving;
	private bool _awayModeAffectsHotwater;
	private int _awayModeTargetTemperature;
	private bool _comfortModeEnabled;
	private int _degradedModeTargetTemperature;
	private string? _overrideType;
	private int _timezoneOffset;
	private bool _valveProtectionEnabled;

	/// <summary>
	/// Creates a WiserSystem view from hub domain/network/device data.
	/// </summary>
	public WiserSystem (WiserRestController wiserRestController, IDictionary<string, object> domainData,
						IDictionary<string, object> networkData, List<Dictionary<string, object>> deviceData,
						IDictionary<string, object> openthermData)
		{
		_wiserRestController = wiserRestController;
		_data = domainData;

		Build (networkData, deviceData, openthermData);
		}

	private void Build (IDictionary<string, object> networkData, List<Dictionary<string, object>> deviceData, IDictionary<string, object> openthermData)
		{
		_systemData = _data.TryGetValue ("System", out var system) && system is Dictionary<string, object> systemDict
			 ? systemDict : [];

		// Sub classes for system setting values
		Capabilities = new WiserHubCapabilitiesInfo (_data.TryGetValue ("DeviceCapabilityMatrix", out var matrix) && matrix is Dictionary<string, object> matrixDict
			 ? matrixDict : []);
		Cloud = new WiserCloud (_systemData.GetStringOr ("CloudConnectionStatus", ""),
			 _data.TryGetValue ("Cloud", out var cloud) && cloud is Dictionary<string, object> cloudDict
			 ? cloudDict : []);
		_deviceData = GetSystemDevice (/*deviceData.TryGetValue ("Device", out var devices) && devices is List<object> deviceList
			 ? deviceList.Cast<Dictionary<string, object>> ().ToList ()
			 : new List<Dictionary<string, object>> ()*/ deviceData);
		Network = new WiserNetwork (networkData.TryGetValue ("Station", out var station) && station is Dictionary<string, object> stationDict
			 ? stationDict : []);
#if OPENTHERM
		Opentherm = new WiserOpentherm (openthermData,
			 _systemData.GetStringOr ("OpenThermConnectionStatus"));
#endif
		Signal = new WiserSignalStrength (_deviceData);
		_upgradeData = new WiserFirmwareUpgradeInfo (_data.TryGetValue ("UpgradeInfo", out var upgrade) && upgrade is List<object> upgradeList
			 ? upgradeList.Cast<Dictionary<string, object>> ().ToList () : []);
		Zigbee = new WiserZigbee (_data.TryGetValue ("Zigbee", out var zigbee) && zigbee is Dictionary<string, object> zigbeeDict
			 ? zigbeeDict : []);

		// Variables to hold values for settable values
		_automaticDaylightSaving = _systemData.TryGetValue ("AutomaticDaylightSaving", out var ads) && ConvertInvariant.ToBoolean (ads);
		_awayModeAffectsHotwater = _systemData.TryGetValue ("AwayModeAffectsHotWater", out var amah) && ConvertInvariant.ToBoolean (amah);
		_awayModeTargetTemperature = _systemData.TryGetValue ("AwayModeSetPointLimit", out var amtl) ? ConvertInvariant.ToInt32 (amtl) : 0;
		_comfortModeEnabled = _systemData.TryGetValue ("ComfortModeEnabled", out var cme) && ConvertInvariant.ToBoolean (cme);
		_degradedModeTargetTemperature = _systemData.TryGetValue ("DegradedModeSetpointThreshold", out var dmst) ? ConvertInvariant.ToInt32 (dmst) : 0;
		HubTime = _systemData.TryGetValue ("UnixTime", out var time) ? DateTimeOffset.FromUnixTimeSeconds (ConvertInvariant.ToInt32 (time)).DateTime : DateTime.Now;
		_overrideType = _systemData.TryGetValue ("OverrideType", out var ot) ? ot.ToString () : "";
		_timezoneOffset = _systemData.TryGetValue ("TimeZoneOffset", out var tzo) ? ConvertInvariant.ToInt32 (tzo) : 0;
		_valveProtectionEnabled = _systemData.TryGetValue ("ValveProtectionEnabled", out var vpe) && ConvertInvariant.ToBoolean (vpe);
		}

	/// <summary>
	/// Update the system state using latest hub data payloads.
	/// </summary>
	public void Update (IDictionary<string, object> domainData, IDictionary<string, object> networkData, List<Dictionary<string, object>> deviceData, IDictionary<string, object> openthermData)
		{
		if (domainData != null)
			{
			_data.Clear ();
			foreach (KeyValuePair<string, object> kv in domainData)
				_data[kv.Key] = kv.Value;
			}

		Build (networkData, deviceData, openthermData);
		}

	private static Dictionary<string, object> GetSystemDevice (List<Dictionary<string, object>> deviceData)
		{
		foreach (Dictionary<string, object> device in deviceData)
			{
			if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "Controller")
				{
				return device;
				}
			}

		return [];
		}

	private Task<bool> SendCommandAsync (object cmd, string? path = null, CancellationToken cancellationToken = default)
		{
		var url = path != null ? $"{RestConstants.WISER_REST_SYSTEM}/{path}" : RestConstants.WISER_REST_SYSTEM;
		return _wiserRestController.SendCommandAsync (url, cmd, cancellationToken: cancellationToken);
		}

	/// <summary>Gets the active system version string.</summary>
	public string? ActiveSystemVersion => _systemData == null ? Constants.TEXT_UNKNOWN : _systemData.TryGetValue ("ActiveSystemVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets or sets whether automatic daylight saving is enabled.</summary>
	public bool AutomaticDaylightSavingEnabled
		{
		get => _automaticDaylightSaving;
		set
			{
			if (SendCommandAsync (new
				{
				AutomaticDaylightSaving = value.ToString ().ToLowerInvariant ()
				}).Result)
				{
				_automaticDaylightSaving = value;
				}
			}
		}

	/// <summary>Gets or sets whether Away mode is enabled.</summary>
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

	/// <summary>Gets or sets whether Away mode affects hot water.</summary>
	public bool AwayModeAffectsHotwater
		{
		get => _awayModeAffectsHotwater;
		set
			{
			if (SendCommandAsync (new
				{
				AwayModeAffectsHotWater = value.ToString ().ToLowerInvariant ()
				}).Result)
				{
				_awayModeAffectsHotwater = value;
				}
			}
		}

	/// <summary>Gets or sets the Away mode target temperature.</summary>
	public double AwayModeTargetTemperature
		{
		get => WiserTemperatureFunctions.FromWiserTemp (_awayModeTargetTemperature);
		set
			{
			var temp = WiserTemperatureFunctions.ToWiserTemp (value);
			if (SendCommandAsync (new
				{
				AwayModeSetPointLimit = temp
				}).Result)
				{
				_awayModeTargetTemperature = temp;
				}
			}
		}

	/// <summary>Gets the boiler fuel type string.</summary>
	public string? BoilerFuelType => _systemData == null ? Constants.TEXT_UNKNOWN : _systemData.TryGetValue ("BoilerSettings", out var settings) && settings is Dictionary<string, object> settingsDict
		 ? settingsDict.TryGetValue ("FuelType", out var fuelType) ? fuelType.ToString () : Constants.TEXT_UNKNOWN
		 : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the hub brand name, if supplied.</summary>
	public string? BrandName => _systemData == null ? null : _systemData.TryGetValue ("BrandName", out var brand) ? brand.ToString () : null;

	/// <summary>Gets reported controller capability flags.</summary>
	public WiserHubCapabilitiesInfo? Capabilities
		{
		get; private set;
		}

	/// <summary>Gets cloud connectivity state/details.</summary>
	public WiserCloud? Cloud
		{
		get; private set;
		}

	/// <summary>Gets or sets Wiser Comfort Mode.</summary>
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

	/// <summary>Gets or sets the Degraded Mode target temperature.</summary>
	public double DegradedModeTargetTemperature
		{
		get => WiserTemperatureFunctions.FromWiserTemp (_degradedModeTargetTemperature);
		set
			{
			var temp = WiserTemperatureFunctions.ToWiserTemp (value);
			if (SendCommandAsync (new
				{
				DegradedModeSetpointThreshold = temp
				}).Result)
				{
				_degradedModeTargetTemperature = temp;
				}
			}
		}

	/// <summary>Gets or sets Eco mode.</summary>
	public bool EcoModeEnabled
		{
		get => _systemData != null && _systemData.TryGetValue ("EcoModeEnabled", out var eco) && ConvertInvariant.ToBoolean (eco);
		set
			{
			if (SendCommandAsync (new
				{
				EcoModeEnabled = value
				}).Result)
				{
				_systemData!["EcoModeEnabled"] = value;
				}
			}
		}

	/// <summary>Gets whether firmware over-the-air is enabled.</summary>
	public bool FirmwareOverTheAirEnabled => _systemData!.TryGetValue ("FotaEnabled", out var fota) && ConvertInvariant.ToBoolean (fota);

	/// <summary>Gets the hub firmware version string.</summary>
	public string? FirmwareVersion => _deviceData!.TryGetValue ("ActiveFirmwareVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the hub GPS geo position.</summary>
	public WiserGPS GeoPosition => new (_systemData!.TryGetValue ("GeoPosition", out var geo) && geo is Dictionary<string, object> geoDict
		 ? geoDict : []);

	/// <summary>Gets the hardware generation of the hub.</summary>
	public int HardwareGeneration => _systemData!.TryGetValue ("HardwareGeneration", out var gen) ? ConvertInvariant.ToInt32 (gen) : 0;

	/// <summary>Gets whether the heating button override is active.</summary>
	public bool HeatingButtonOverrideState => _systemData!.TryGetValue ("HeatingButtonOverrideState", out var state) && state.ToString () == Constants.TEXT_ON;

	/// <summary>Gets whether the hot water button override is active.</summary>
	public bool HotwaterButtonOverrideState => _systemData!.TryGetValue ("HotWaterButtonOverrideState", out var state) && state.ToString () == Constants.TEXT_ON;

	/// <summary>Gets the hub time reported by the controller.</summary>
	public DateTime HubTime
		{
		get; private set;
		}

	/// <summary>Gets the controller device id.</summary>
	public int Id => _deviceData!.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets a value indicating Away mode is enabled.</summary>
	public bool IsAwayModeEnabled => _overrideType == "Away";

	/// <summary>Gets the model identifier for the controller.</summary>
	public string? Model => _deviceData!.TryGetValue ("ModelIdentifier", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the host name of the controller.</summary>
	public string Name => Network?.Hostname ?? "No Name";

	/// <summary>Gets the network information for the controller.</summary>
	public WiserNetwork? Network
		{
		get; private set;
		}

	/// <summary>Gets the controller Zigbee node id.</summary>
	public int NodeId => _deviceData!.TryGetValue ("NodeId", out var nodeId) ? ConvertInvariant.ToInt32 (nodeId) : 0;

#if OPENTHERM
	/// <summary>Gets OpenTherm telemetry and status, if supported.</summary>
	public WiserOpentherm? Opentherm
		{
		get; private set;
		}
#endif

	/// <summary>Gets the pairing status string.</summary>
	public string? PairingStatus => _systemData!.TryGetValue ("PairingStatus", out var status) ? status.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the parent Zigbee node id.</summary>
	public int ParentNodeId => _deviceData!.TryGetValue ("ParentNodeId", out var nodeId) ? ConvertInvariant.ToInt32 (nodeId) : 0;

	/// <summary>Gets the product type string.</summary>
	public string? ProductType => _deviceData!.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets Zigbee signal metrics for the controller.</summary>
	public WiserSignalStrength? Signal
		{
		get; private set;
		}

	/// <summary>Gets formatted sunrise times for the week.</summary>
	public Dictionary<string, string> SunriseTimes => _systemData!.TryGetValue ("SunriseTimes", out var times) && times is List<object> timesList
			 ? SpecialTimes.SunriseTimes ([.. timesList.Select (ConvertInvariant.ToInt32)])
			 : [];

	/// <summary>Gets formatted sunset times for the week.</summary>
	public Dictionary<string, string> SunsetTimes => _systemData!.TryGetValue ("SunsetTimes", out var times) && times is List<object> timesList
			 ? SpecialTimes.SunsetTimes ([.. timesList.Select (ConvertInvariant.ToInt32)])
			 : [];

	/// <summary>Gets the system mode string.</summary>
	public string? SystemMode => _systemData!.TryGetValue ("SystemMode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets or sets the time zone offset in minutes.</summary>
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

	/// <summary>Gets whether any user overrides are active.</summary>
	public bool UserOverridesActive => _systemData!.TryGetValue ("UserOverridesActive", out var active) && ConvertInvariant.ToBoolean (active);

	/// <summary>Gets or sets whether valve protection is enabled.</summary>
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

	/// <summary>Gets Zigbee radio info for the hub.</summary>
	public WiserZigbee? Zigbee
		{
		get; private set;
		}

	/// <summary>
	/// Temporarily allows joining new devices for a specified duration.
	/// </summary>
	/// <param name="allowTime">Permit-join time in seconds (default 120).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the request was accepted.</returns>
	public Task<bool> AllowAddDeviceAsync (int allowTime = 120, CancellationToken cancellationToken = default) =>
		SendCommandAsync (allowTime, "RequestPermitJoin", cancellationToken);

	/// <summary>
	/// Boosts all rooms by a temperature delta for a specified duration.
	/// </summary>
	/// <param name="incTemp">Increase in degrees.</param>
	/// <param name="duration">Duration in minutes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True on success.</returns>
	public Task<bool> BoostAllRoomsAsync (double incTemp, int duration, CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = "Boost",
				DurationMinutes = duration,
				IncreaseSetPointBy = WiserTemperatureFunctions.ToWiserTemp (incTemp, "delta")
				}
			}, cancellationToken: cancellationToken);

	/// <summary>
	/// Cancels all user overrides across the system.
	/// </summary>
	public Task<bool> CancelAllOverridesAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = "CancelUserOverrides"
				}
			}, cancellationToken: cancellationToken);

	/// <summary>
	/// Connects the hub to a Wi-Fi network.
	/// </summary>
	/// <param name="ssid">Network SSID.</param>
	/// <param name="password">Network password.</param>
	/// <param name="channel">Optional channel number.</param>
	/// <param name="securityMode">Optional security mode string.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True on success.</returns>
	public Task<bool> ConnectToNetworkAsync (string ssid, string password, int? channel = null, string? securityMode = null, CancellationToken cancellationToken = default)
		{
		var cmdData = new Dictionary<string, object?> { { "Enabled", true } };

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

		// Use the host from the private data dictionary, which is set from the constructor
		var host = _wiserRestController.GetHost ();
		return _wiserRestController.SendCommandAsync ($"{WISER_HUB_NETWORK.FormatInvariant (host)}/Station", cmdData, cancellationToken: cancellationToken);
		}
	}