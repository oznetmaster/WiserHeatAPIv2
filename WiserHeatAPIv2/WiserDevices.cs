//-----------------------------------------------------------------------
// <copyright file="WiserDevices.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.Constants;
using static WiserHeatApiV2.RestConstants;
using static WiserHeatApiV2.StringExtensions;

namespace WiserHeatApiV2;

/// <summary>
/// Base type for all Wiser devices. Exposes common identity, state and commands.
/// </summary>
/// <remarks>
/// Most read-only properties surface values from the hub payload; when a field is absent the string-returning
/// properties fall back to <see cref="Constants.TEXT_UNKNOWN"/> and numeric properties typically fall back to 0.
/// Synchronous setters block and delegate to async operations, which can propagate exceptions synchronously.
/// </remarks>
public class WiserDevice
	{
	private string _name;
	private bool _deviceLockEnabled;
	/// <summary>Creates a new device wrapper from hub payloads.</summary>
	/// <param name="wiserRestController">REST controller used to send commands.</param>
	/// <param name="data">Raw device data payload.</param>
	/// <param name="deviceTypeData">Type-specific device payload.</param>
	/// <remarks>
	/// Initializes identifiers, links signal strength metrics and sets defaults based on the payload provided.
	/// </remarks>
	public WiserDevice (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData)
		{
		WiserRestController = wiserRestController;
		Data = data;
		DeviceTypeData = deviceTypeData;
		Signal = new WiserSignalStrength (data);
		Id = data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
		RoomId = deviceTypeData.TryGetValue ("RoomId", out var roomId) ? ConvertInvariant.ToInt32 (roomId) : 0;
		_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && ConvertInvariant.ToBoolean (lockEnabled);
		Identify = data.TryGetValue ("IdentifyActive", out var identify) && ConvertInvariant.ToBoolean (identify);
		DeviceTypeId = deviceTypeData.TryGetValue ("id", out var deviceTypeId) ? ConvertInvariant.ToInt32 (deviceTypeId) : 0;
		ProductType = data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;
		_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : $"{ProductType}-{Id}";
		}

	/// <summary>
	/// Sends a command to this device.
	/// </summary>
	/// <param name="cmd">Anonymous object payload to send.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result is <see langword="true"/> if the hub
	/// returned a success status code; otherwise, <see langword="false"/>.
	/// </returns>
	private Task<bool> SendDeviceCommandAsync (object cmd, CancellationToken cancellationToken = default) =>
		WiserRestController.SendCommandAsync (
			WISER_REST_DEVICE.FormatInvariant (Id),
			cmd,
			cancellationToken: cancellationToken
			);

	/// <summary>Gets or sets whether the device lock is enabled.</summary>
	/// <remarks>
	/// Setter is synchronous and delegates to <see cref="SetDeviceLockEnabledAsync(bool, CancellationToken)"/>, blocking until completion. Exceptions from the async operation are propagated synchronously.
	/// </remarks>
	public bool DeviceLockEnabled
		{
		get => _deviceLockEnabled;
		set
			{
			if (_deviceLockEnabled != value)
				{
				_ = SetDeviceLockEnabledAsync (value).GetAwaiter ().GetResult ();
				}
			}
		}

	/// <summary>
	/// Asynchronously enables or disables the device lock.
	/// </summary>
	/// <param name="value">Desired state.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result is <see langword="true"/> if the operation
	/// succeeded; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetDeviceLockEnabledAsync (bool value, CancellationToken cancellationToken = default)
		{
		if (_deviceLockEnabled == value)
			{
			return true; // No change needed
			}

		if (await SendDeviceCommandAsync (new
			{
			DeviceLockEnabled = value
			}, cancellationToken).ConfigureAwait (false))
			{
			_deviceLockEnabled = value;
			return true;
			}

		return false;
		}

	/// <summary>Gets whether the device is currently in identify mode.</summary>
	public bool Identify { get; private set; }
	/// <summary>
	/// Asynchronously enables or disables identify mode.
	/// </summary>
	/// <param name="value"><see langword="true"/> to enable identify; <see langword="false"/> to disable.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result is <see langword="true"/> if the operation
	/// succeeded; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetIdentifyAsync (bool value, CancellationToken cancellationToken = default)
		{
		if (Identify == value)
			{
			return true; // No change needed
			}

		if (await SendDeviceCommandAsync (new
			{
			Identify = value
			}, cancellationToken).ConfigureAwait (false))
			{
			Identify = value;
			return true;
			}

		return false;
		}
	/// <summary>
	/// Gets or sets the device name.
	/// </summary>
	/// <remarks>
	/// Setter is synchronous and sends a command to the hub, blocking until completion. The hub may normalize the
	/// name. Exceptions from the operation are propagated synchronously.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown if the new name is null or whitespace.</exception>
	public string Name
		{
		get => _name;
		set
			{
			if (string.IsNullOrWhiteSpace (value))
				{
				throw new ArgumentException ("Name cannot be null or empty.", nameof (value));
				}

			if (value != _name)
				{
				if (SendDeviceCommandAsync (new { Name = value }).Result)
					{
					_name = value;
					}
				}
			}
		}

	/// <summary>Gets the room id for this device, if any.</summary>
	public int RoomId { get; }

	/// <summary>Gets the type id for this device.</summary>
	public int DeviceTypeId { get; }

	/// <summary>
	/// Gets the active firmware version string.
	/// </summary>
	/// <value>The firmware version, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.</value>
	public string FirmwareVersion => Data.TryGetValue ("ActiveFirmwareVersion", out var version) ? version.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the unique device id.</summary>
	public int Id { get; }

	/// <summary>
	/// Gets the model identifier.
	/// </summary>
	/// <value>The model identifier, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.</value>
	public virtual string Model => Data.TryGetValue ("ModelIdentifier", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the Zigbee node id.</summary>
	public int NodeId => Data.TryGetValue ("NodeId", out var nodeId) ? ConvertInvariant.ToInt32 (nodeId) : 0;

	/// <summary>
	/// Gets the product identifier.
	/// </summary>
	/// <value>The product identifier, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.</value>
	public string ProductIdentifier => Data.TryGetValue ("ProductIdentifier", out var id) ? id.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets the product model.
	/// </summary>
	/// <value>The product model, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.</value>
	public string ProductModel => Data.TryGetValue ("ProductModel", out var model) ? model.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the parent node id (if any).</summary>
	public int ParentNodeId => Data.TryGetValue ("ParentNodeId", out var nodeId) ? ConvertInvariant.ToInt32 (nodeId) : 0;

	/// <summary>Gets the product type.</summary>
	public string ProductType { get; }

	/// <summary>
	/// Gets the device serial number.
	/// </summary>
	/// <value>The serial number, or <see cref="Constants.TEXT_UNKNOWN"/> if not provided.</value>
	public string SerialNumber => Data.TryGetValue ("SerialNumber", out var serial) ? serial.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets radio signal metrics for this device.</summary>
	public WiserSignalStrength Signal { get; }

	/// <summary>Gets the REST controller used to communicate with the hub.</summary>
	protected WiserRestController WiserRestController { get; }

	/// <summary>Gets the raw device data dictionary.</summary>
	protected IDictionary<string, object> Data { get; }

	/// <summary>Gets the device-type specific data dictionary.</summary>
	protected IDictionary<string, object> DeviceTypeData { get; }
	}

/// <summary>
/// Device aggregation and lookup helpers. Maintains typed collections and search APIs.
/// </summary>
/// <remarks>
/// Builds typed device collections (smart valves, room stats, plugs, UFH controllers, etc.) from the domain payload
/// and keeps them up to date via <see cref="Update(System.Collections.Generic.IDictionary{string, object}, WiserSchedules)"/>.
/// </remarks>
public class WiserDevices
	{
	private readonly WiserRestController _wiserRestController;
	private ConcurrentDictionary<int, Dictionary<string, object>> _devicesList;
	private IDictionary<string, object> _domainData;
	private WiserSchedules _schedules;

	/// <summary>Creates a device catalog from domain data.</summary>
	/// <param name="wiserRestController">REST controller used to send commands.</param>
	/// <param name="domainData">Hub domain payload (contains Device and device-type arrays).</param>
	/// <param name="schedules">Schedules service used to map schedule assignments.</param>
	/// <remarks>
	/// Initializes typed collections by scanning the provided domain payload. Collections may be empty if the
	/// corresponding device arrays are not present.
	/// </remarks>
	public WiserDevices (WiserRestController wiserRestController, IDictionary<string, object> domainData, WiserSchedules schedules)
		{
		_wiserRestController = wiserRestController;
		_devicesList = domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList
			? new ConcurrentDictionary<int, Dictionary<string, object>> (
				devicesList.ToDictionary (d => ConvertInvariant.ToInt32 (d["id"]), d => d))
			: new ConcurrentDictionary<int, Dictionary<string, object>> ();
		_domainData = domainData;
		_schedules = schedules;

		Build (_devicesList.Values);
		}

	private void Build (IEnumerable<IDictionary<string, object>> deviceList)
		{
		foreach (Dictionary<string, object> device in deviceList.Cast<Dictionary<string, object>> ())
			{
			var deviceId = ConvertInvariant.ToInt32 (device["id"]);

			// Add smart valve (iTRV) object to collection
			if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "iTRV")
				{
				Dictionary<string, object>? smartvalveInfo = (_domainData.TryGetValue ("SmartValve", out var smartValves) && smartValves is List<Dictionary<string, object>> smartValvesList)
					? smartValvesList.FirstOrDefault (sv => sv.TryGetValue ("id", out var id) && ConvertInvariant.ToInt32 (id) == deviceId)
					: null;

				if (smartvalveInfo != null)
					{
					smartvalveInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
					Smartvalves.All.Add (
						new WiserSmartValve (
							_wiserRestController,
							device,
							smartvalveInfo
						)
					);
					}
				}

			// Add room stat object to collection
			else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "RoomStat")
				{
				Dictionary<string, object>? roomstatInfo = (_domainData.TryGetValue ("RoomStat", out var roomStats) && roomStats is List<Dictionary<string, object>> roomStatsList)
					? roomStatsList.FirstOrDefault (rs => rs.TryGetValue ("id", out var id) && ConvertInvariant.ToInt32 (id) == deviceId)
					: null;

				if (roomstatInfo != null)
					{
					roomstatInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
					Roomstats.All.Add (
						new WiserRoomStat (
							_wiserRestController,
							device,
							roomstatInfo
						)
					);
					}
				}

			// Add smart plug object to collection
			else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "SmartPlug")
				{
				Dictionary<string, object>? smartplugInfo = (_domainData.TryGetValue ("SmartPlug", out var smartPlugs) && smartPlugs is List<Dictionary<string, object>> smartPlugsList)
					? smartPlugsList.FirstOrDefault (sp => sp.TryGetValue ("id", out var id) && ConvertInvariant.ToInt32 (id) == deviceId)
					: null;

				if (smartplugInfo != null)
					{
					WiserSchedule smartplugSchedule = _schedules.GetByType (WiserScheduleType.OnOff)
						 .FirstOrDefault (s => s.Id == (smartplugInfo.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0));

					//smartplugInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
					Smartplugs.All.Add (
						 new WiserSmartPlug (
							  _wiserRestController,
							  device,
							  smartplugInfo,
							  smartplugSchedule
						 )
					);
					}
				}

#if HEATACTUATOR
			// Add heating actuator object to collection
			else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "HeatingActuator")
				{
				Dictionary<string, object>? heatingActuatorInfo = (_domainData.TryGetValue ("HeatingActuator", out var heatingActuators) && heatingActuators is List<Dictionary<string, object>> heatingActuatorsList)
					? heatingActuatorsList.FirstOrDefault (ha => ha.TryGetValue ("id", out var id) && ConvertInvariant.ToInt32 (id) == ConvertInvariant.ToInt32 (device["id"]))
					: null;

				if (heatingActuatorInfo != null)
					{
					heatingActuatorInfo["RoomId"] = GetTempDeviceRoomId (_domainData, ConvertInvariant.ToInt32 (device["id"]));
					HeatingActuators.All.Add (
						 new WiserHeatingActuator (
							  _wiserRestController,
							  device,
							  heatingActuatorInfo
						 )
					);
					}
				}
#endif
			// Add ufh controller object to collection
			else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "UnderFloorHeating")
				{
				Dictionary<string, object>? ufhControllerInfo = (_domainData.TryGetValue ("UnderFloorHeating", out var ufhControllers) && ufhControllers is List<Dictionary<string, object>> ufhControllersList)
					? ufhControllersList.FirstOrDefault (ufh => ufh.TryGetValue ("id", out var id) && ConvertInvariant.ToInt32 (id) == deviceId)
					: null;

				if (ufhControllerInfo != null)
					{
					ufhControllerInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
					UfhControllers.All.Add (
						 new WiserUFHController (
							  _wiserRestController,
							  device,
							  ufhControllerInfo
						 )
					);
					}
				}
#if SHUTTER
			// Add shutter object to collection
			else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "Shutter")
				{
				Dictionary<string, object>? shutterInfo = (_domainData.TryGetValue ("Shutter", out var shutters) && shutters is List<Dictionary<string, object>> shuttersList)
					? shuttersList.FirstOrDefault (s => s.TryGetValue ("DeviceId", out var id) && ConvertInvariant.ToInt32 (id) == ConvertInvariant.ToInt32 (device["id"]))
					: null;

				if (shutterInfo != null)
					{
					WiserSchedule shutterSchedule = _schedules.GetByType (WiserScheduleType.Level)
						 .FirstOrDefault (s => s.Id == (shutterInfo.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0));

					Shutters.All.Add (
						 new WiserShutter (
							  _wiserRestController,
							  device,
							  shutterInfo,
							  shutterSchedule
						 )
					);
					}
				}
#endif
#if LIGHT
			// Add light object to collection
			else if (device.TryGetValue ("ProductType", out productType) &&
				  (productType.ToString () == "OnOffLight" || productType.ToString () == "DimmableLight"))
				{
				Dictionary<string, object>? lightInfo = (_domainData.TryGetValue ("Light", out var lights) && lights is List<Dictionary<string, object>> lightsList)
					? lightsList.FirstOrDefault (l => l.TryGetValue ("DeviceId", out var id) && ConvertInvariant.ToInt32 (id) == ConvertInvariant.ToInt32 (device["id"]))
					: null;

				if (lightInfo != null)
					{
					WiserSchedule lightSchedule = _schedules.GetByType (WiserScheduleType.Level)
						 .FirstOrDefault (s => s.Id == (lightInfo.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0));

					if (productType.ToString () == "DimmableLight")
						{
						Lights.All.Add (
							 new WiserDimmableLight (
								  _wiserRestController,
								  device,
								  lightInfo,
								  lightSchedule
							 )
						);
						}
					else
						{
						Lights.All.Add (
							 new WiserLight (
								  _wiserRestController,
								  device,
								  lightInfo,
								  lightSchedule
							 )
						);
						}
					}
				}
#endif
			}
		}

	/// <summary>
	/// Updates the device catalog with the latest hub domain data.
	/// </summary>
	/// <param name="domainData">Latest domain payload from the hub.</param>
	/// <param name="schedules">Schedules service for mapping schedule assignments.</param>
	/// <remarks>
	/// Reconciles added and removed devices, updates the internal index and extends typed collections for new devices.
	/// Devices no longer present are removed from both the index and the typed collections.
	/// </remarks>
	public void Update (IDictionary<string, object> domainData, WiserSchedules schedules)
		{
		_domainData = domainData;
		_schedules = schedules;
		if (domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
			{
			var dlh = new HashSet<int> (_devicesList.Keys);
			var newDeviceHash = new HashSet<int> (devicesList.Select (d => ConvertInvariant.ToInt32 (d["id"])));
			var removedDevicesHash = dlh.Except (newDeviceHash).ToHashSet<int> ();
			var addedDevicesHash = newDeviceHash.Except (dlh).ToHashSet<int> ();
			if (removedDevicesHash.Count > 0)
				{
				// Remove devices that are no longer present
				foreach (var id in removedDevicesHash)
					_ = _devicesList.TryRemove (id, out _);

				// Remove from collections
				_ = Smartvalves.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
				_ = Roomstats.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
				_ = Smartplugs.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
				}
			// Add new devices
			if (addedDevicesHash.Count > 0)
				{
				IEnumerable<Dictionary<string, object>> newDevices = devicesList.Where (d => addedDevicesHash.Contains (ConvertInvariant.ToInt32 (d["id"])));
				foreach (Dictionary<string, object> newDevice in newDevices)
					_devicesList[ConvertInvariant.ToInt32 (newDevice["id"]) ] = newDevice;

				// Rebuild collections with new devices
				Build (newDevices);
				}
			}
		else
			{
			_devicesList = new ConcurrentDictionary<int, Dictionary<string, object>> ();
			}
		}

	private int GetTempDeviceRoomId (IDictionary<string, object> domainData, int deviceId)
		{
		if (domainData.TryGetValue ("Room", out var rooms) && rooms is List<Dictionary<string, object>> roomsList)
			{
			foreach (Dictionary<string, object> room in roomsList)
				{
				var roomId = ConvertInvariant.ToInt32 (room["id"]);

				if (room.TryGetValue ("SmartValveIds", out var smartValveIds) && smartValveIds is List<object> smartValveIdsList)
					{
					foreach (var id in smartValveIdsList)
						{
						if (ConvertInvariant.ToInt32 (id) == deviceId)
							return roomId;
						}
					}
#if HEATACTUATOR
				if (room.TryGetValue ("HeatingActuatorIds", out var heatingActuatorIds) && heatingActuatorIds is List<object> heatingActuatorIdsList)
					{
					foreach (var id in heatingActuatorIdsList)
						{
						if (ConvertInvariant.ToInt32 (id) == deviceId)
							return roomId;
						}
					}
#endif
				if (room.TryGetValue ("RoomStatId", out var roomStatId))
					{
					if (ConvertInvariant.ToInt32 (roomStatId) == deviceId)
						return roomId;
					}

				if (room.TryGetValue ("UnderFloorHeatingId", out var ufhId))
					{
					if (ConvertInvariant.ToInt32 (ufhId) == deviceId)
						return roomId;
					}

				if (Smartplugs.All.FirstOrDefault (sp => sp.Id == deviceId && sp.RoomId == roomId) != null)
					{
					return roomId;
					}
				}
			}

		return 0;
		}

	/// <summary>
	/// Gets all devices across all categories.
	/// </summary>
	/// <value>
	/// A flattened list containing devices from all typed collections (e.g., smart valves, room stats,
	/// smart plugs, UFH controllers, and optionally actuators, shutters, and lights depending on build symbols).
	/// </value>
	public List<WiserDevice> All =>
		 [
			 .. Smartvalves.All.Cast<WiserDevice> ()
,
			 .. Roomstats.All,
			 .. Smartplugs.All,
#if HEATACTUATOR
			 .. HeatingActuators.All,
#endif
			 .. UfhControllers.All,
#if SHUTTER
			 .. Shutters.All,
#endif
#if LIGHT
			 .. Lights.All,
			 #endif
			 ];

	/// <summary>
	/// Gets the total count of devices.
	/// </summary>
	/// <value>The number of devices across all typed collections.</value>
	public int Count => All.Count;

#if HEATACTUATOR
	/// <summary>
	/// Gets the heating actuator collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserHeatingActuator"/> instances.</value>
	public WiserHeatingActuators HeatingActuators { get; } = new WiserHeatingActuators ();
#endif

#if LIGHT
	/// <summary>
	/// Gets the lights collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserLight"/> and <see cref="WiserDimmableLight"/> instances.</value>
	public WiserLights Lights { get; } = new WiserLights ();
#endif
	/// <summary>
	/// Gets the room stats collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserRoomStat"/> instances.</value>
	public WiserRoomStats Roomstats { get; } = new WiserRoomStats ();

#if SHUTTER
	/// <summary>
	/// Gets the shutters collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserShutter"/> instances.</value>
	public WiserShutters Shutters { get; } = new WiserShutters ();
#endif

	/// <summary>
	/// Gets the smart plugs collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserSmartPlug"/> instances.</value>
	public WiserSmartPlugs Smartplugs { get; } = new WiserSmartPlugs ();

	/// <summary>
	/// Gets the smart valves collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserSmartValve"/> instances.</value>
	public WiserSmartValves Smartvalves { get; } = new WiserSmartValves ();

	/// <summary>
	/// Gets the UFH controllers collection.
	/// </summary>
	/// <value>A collection of <see cref="WiserUFHController"/> instances.</value>
	public WiserUFHControllers UfhControllers { get; } = new WiserUFHControllers ();

	/// <summary>
	/// Find a device by its id.
	/// </summary>
	/// <param name="id">Device id to search for.</param>
	/// <returns>The matching <see cref="WiserDevice"/>, or <see langword="null"/> if not found.</returns>
	public WiserDevice GetById (int id) => All.FirstOrDefault (device => device.Id == id);

	/// <summary>
	/// Get all devices located in a specific room.
	/// </summary>
	/// <param name="roomId">Room id to filter by.</param>
	/// <returns>List of devices in the given room.</returns>
	public List<WiserDevice> GetByRoomId (int roomId) =>
		[.. All.Where (device =>
			 (device is WiserSmartValve valve && valve.RoomId == roomId) ||
			 (device is WiserRoomStat stat && stat.RoomId == roomId) ||
#if HEATACTUATOR
			 (device is WiserHeatingActuator actuator && actuator.RoomId == roomId) ||
#endif
			 (device is WiserUFHController controller && controller.RoomId == roomId) ||
			 (device is WiserSmartPlug plug && plug.RoomId == roomId)
#if SHUTTER
			 || (device is WiserShutter shutter && shutter.RoomId == roomId)
#endif
#if LIGHT
			 || (device is WiserLight light && light.RoomId == roomId)
#endif
		)];

	/// <summary>
	/// Find a device by Zigbee node id.
	/// </summary>
	/// <param name="nodeId">Zigbee node id.</param>
	/// <returns>The first device with the given node id, or <see langword="null"/>.</returns>
	public WiserDevice GetByNodeId (int nodeId) => All.FirstOrDefault (device => device.NodeId == nodeId);

	/// <summary>
	/// Find a device by serial number.
	/// </summary>
	/// <param name="serialNumber">Serial number to match.</param>
	/// <returns>The first device with the given serial number, or <see langword="null"/>.</returns>
	public WiserDevice GetBySerialNumber (string serialNumber) => All.FirstOrDefault (device => device.SerialNumber == serialNumber);

	/// <summary>
	/// Get all devices with the specified parent node id.
	/// </summary>
	/// <param name="nodeId">Parent node id to match.</param>
	/// <returns>List of devices having the provided parent node id.</returns>
	public List<WiserDevice> GetByParentNodeId (int nodeId) => [.. All.Where (device => device.ParentNodeId == nodeId)];
	}

