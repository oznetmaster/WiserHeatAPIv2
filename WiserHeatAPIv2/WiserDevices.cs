//-----------------------------------------------------------------------
// <copyright file="WiserDevices.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, object> _data;
		private readonly IDictionary<string, object> _deviceTypeData;
		private readonly WiserSignalStrength _signal;
		private readonly int _id;
		private readonly int _deviceTypeId;
		private readonly int _roomId;
		private bool _deviceLockEnabled;
		private bool _identifyActive;
		private string _name;


		public WiserDevice (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData)
			{
			_wiserRestController = wiserRestController;
			_data = data;
			_deviceTypeData = deviceTypeData;
			_signal = new WiserSignalStrength (data);
			_id = data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;
			_roomId = deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId, CultureInfo.InvariantCulture) : 0;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled, CultureInfo.InvariantCulture);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify, CultureInfo.InvariantCulture);
			_deviceTypeId = deviceTypeData.TryGetValue ("id", out var deviceTypeId) ? Convert.ToInt32 (deviceTypeId, CultureInfo.InvariantCulture) : 0;
			if (deviceTypeData.TryGetValue ("Name", out var name))
				{
				_name = name.ToString ();
				}
			else
				{
				_name = $"{ProductType}-{Id}";
				}
			}

		private Task<bool> SendDeviceCommandAsync (object cmd, CancellationToken cancellationToken = default)
			{
			return WiserRestController.SendCommandAsync (
				string.Format (CultureInfo.InvariantCulture, RestConstants.WiserDevice, Id),
				cmd,
				cancellationToken: cancellationToken
				);
			}

		public bool DeviceLockEnabled
			{
			get
				{
				return _deviceLockEnabled;
				}
			set
				{
				if (_deviceLockEnabled != value)
					{
					SetDeviceLockEnabledAsync (value).Wait ();
					}
				}
			}

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

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value, CancellationToken cancellationToken = default)
			{
			if (_identifyActive == value)
				{
				return true; // No change needed
				}

			if (await SendDeviceCommandAsync (new
				{
				Identify = value
				}, cancellationToken).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}
		public virtual string Name
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

		public int RoomId => _roomId;

		public int DeviceTypeId => _deviceTypeId;

		public string FirmwareVersion => Data.TryGetValue ("ActiveFirmwareVersion", out var version) ? version.ToString () : Constants.TextUnknown;

		public int Id => _id;

		public virtual string Model => Data.TryGetValue ("ModelIdentifier", out var model) ? model.ToString () : Constants.TextUnknown;

		public int NodeId => Data.TryGetValue ("NodeId", out var nodeId) ? Convert.ToInt32 (nodeId, CultureInfo.InvariantCulture) : 0;

		public string ProductIdentifier => Data.TryGetValue ("ProductIdentifier", out var id) ? id.ToString () : Constants.TextUnknown;

		public string ProductModel => Data.TryGetValue ("ProductModel", out var model) ? model.ToString () : Constants.TextUnknown;

		public int ParentNodeId => Data.TryGetValue ("ParentNodeId", out var nodeId) ? Convert.ToInt32 (nodeId, CultureInfo.InvariantCulture) : 0;

		public string ProductType => Data.TryGetValue ("ProductType", out var type) ? type.ToString () : Constants.TextUnknown;

		public string SerialNumber => Data.TryGetValue ("SerialNumber", out var serial) ? serial.ToString () : Constants.TextUnknown;

		public WiserSignalStrength Signal => _signal;

		protected WiserRestController WiserRestController => _wiserRestController;

		protected IDictionary<string, object> Data => _data;

		protected IDictionary<string, object> DeviceTypeData => _deviceTypeData;
		}

	public class WiserDevices
		{
		private readonly WiserRestController _wiserRestController;
		private ConcurrentDictionary<int, Dictionary<string, object>> _devicesList;
		private IDictionary<string, object> _domainData;
		private WiserSchedules _schedules;

		private readonly WiserSmartValves _smartvalvesCollection = new WiserSmartValves ();
		private readonly WiserRoomStats _roomstatsCollection = new WiserRoomStats ();
		private readonly WiserSmartPlugs _smartplugsCollection = new WiserSmartPlugs ();
#if HEATACTUATOR
		private readonly WiserHeatingActuators _heatingActuatorsCollection = new WiserHeatingActuators ();
#endif
		private readonly WiserUFHControllers _ufhControllersCollection = new WiserUFHControllers ();
#if SHUTTER
		private readonly WiserShutters _shuttersCollection = new WiserShutters ();
#endif
#if LIGHT
		private readonly WiserLights _lightsCollection = new WiserLights ();
#endif

		public WiserDevices (WiserRestController wiserRestController, IDictionary<string, object> domainData, WiserSchedules schedules)
			{
			_wiserRestController = wiserRestController;
			if (domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
				_devicesList = new ConcurrentDictionary<int, Dictionary<string, object>> (
					devicesList.ToDictionary (d => Convert.ToInt32 (d["id"], CultureInfo.InvariantCulture), d => d)
				);
			else
				_devicesList = new ConcurrentDictionary<int, Dictionary<string, object>> ();
			_domainData = domainData;
			_schedules = schedules;

			Build (_devicesList.Values);
			}

		private void Build (IEnumerable<IDictionary<string, object>> deviceList)
			{
			foreach (Dictionary<string, object> device in deviceList)
				{
				var deviceId = Convert.ToInt32 (device["id"], CultureInfo.InvariantCulture);

				// Add smart valve (iTRV) object to collection
				if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "iTRV")
					{
					var smartvalveInfo = (_domainData.TryGetValue ("SmartValve", out var smartValves) && smartValves is List<Dictionary<string, object>> smartValvesList)
						? smartValvesList.FirstOrDefault (sv => sv.TryGetValue ("id", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
						: null;

					if (smartvalveInfo != null)
						{
						smartvalveInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
						_smartvalvesCollection.All.Add (
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
					var roomstatInfo = (_domainData.TryGetValue ("RoomStat", out var roomStats) && roomStats is List<Dictionary<string, object>> roomStatsList)
						? roomStatsList.FirstOrDefault (rs => rs.TryGetValue ("id", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
						: null;

					if (roomstatInfo != null)
						{
						roomstatInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
						_roomstatsCollection.All.Add (
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
					var smartplugInfo = (_domainData.TryGetValue ("SmartPlug", out var smartPlugs) && smartPlugs is List<Dictionary<string, object>> smartPlugsList)
						? smartPlugsList.FirstOrDefault (sp => sp.TryGetValue ("id", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
						: null;

					if (smartplugInfo != null)
						{
						var smartplugSchedule = _schedules.GetByType (WiserScheduleType.OnOff)
							 .FirstOrDefault (s => s.Id == (smartplugInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));

						//smartplugInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
						_smartplugsCollection.All.Add (
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
					var heatingActuatorInfo = (_domainData.TryGetValue ("HeatingActuator", out var heatingActuators) && heatingActuators is List<Dictionary<string, object>> heatingActuatorsList)
						? heatingActuatorsList.FirstOrDefault (ha => ha.TryGetValue ("id", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == Convert.ToInt32 (device["id"], CultureInfo.InvariantCulture))
						: null;

					if (heatingActuatorInfo != null)
						{
						heatingActuatorInfo["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"], CultureInfo.InvariantCulture));
						_heatingActuatorsCollection.All.Add (
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
					var ufhControllerInfo = (_domainData.TryGetValue ("UnderFloorHeating", out var ufhControllers) && ufhControllers is List<Dictionary<string, object>> ufhControllersList)
						? ufhControllersList.FirstOrDefault (ufh => ufh.TryGetValue ("id", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
						: null;

					if (ufhControllerInfo != null)
						{
						ufhControllerInfo["RoomId"] = GetTempDeviceRoomId (_domainData, deviceId);
						_ufhControllersCollection.All.Add (
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
					var shutterInfo = (_domainData.TryGetValue ("Shutter", out var shutters) && shutters is List<Dictionary<string, object>> shuttersList)
						? shuttersList.FirstOrDefault (s => s.TryGetValue ("DeviceId", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == Convert.ToInt32 (device["id"], CultureInfo.InvariantCulture))
						: null;

					if (shutterInfo != null)
						{
						var shutterSchedule = _schedules.GetByType (WiserScheduleType.Level)
							 .FirstOrDefault (s => s.Id == (shutterInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));

						_shuttersCollection.All.Add (
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
					var lightInfo = (_domainData.TryGetValue ("Light", out var lights) && lights is List<Dictionary<string, object>> lightsList)
						? lightsList.FirstOrDefault (l => l.TryGetValue ("DeviceId", out var id) && Convert.ToInt32 (id, CultureInfo.InvariantCulture) == Convert.ToInt32 (device["id"], CultureInfo.InvariantCulture))
						: null;

					if (lightInfo != null)
						{
						var lightSchedule = _schedules.GetByType (WiserScheduleType.Level)
							 .FirstOrDefault (s => s.Id == (lightInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));

						if (productType.ToString () == "DimmableLight")
							{
							_lightsCollection.All.Add (
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
							_lightsCollection.All.Add (
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

		public void Update (IDictionary<string, object> domainData, WiserSchedules schedules)
			{
			_domainData = domainData;
			_schedules = schedules;
			if (domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
				{
				var dlh = new HashSet<int> (_devicesList.Keys);
				var newDeviceHash = new HashSet<int> (devicesList.Select (d => Convert.ToInt32 (d["id"], CultureInfo.InvariantCulture)));
				var removedDevicesHash = dlh.Except (newDeviceHash).ToHashSet<int> ();
				var addedDevicesHash = newDeviceHash.Except (dlh).ToHashSet<int> ();
				if (removedDevicesHash.Count > 0)
					{
					// Remove devices that are no longer present
					foreach (var id in removedDevicesHash)
						_devicesList.TryRemove (id, out _);

					// Remove from collections
					_smartvalvesCollection.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
					_roomstatsCollection.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
					_smartplugsCollection.All.RemoveAll (d => removedDevicesHash.Contains (d.Id));
					}
				// Add new devices
				if (addedDevicesHash.Count > 0)
					{
					var newDevices = devicesList.Where (d => addedDevicesHash.Contains (Convert.ToInt32 (d["id"], CultureInfo.InvariantCulture)));
					foreach (var newDevice in newDevices)
						_devicesList[Convert.ToInt32 (newDevice["id"], CultureInfo.InvariantCulture)] = newDevice;

					// Rebuild collections with new devices
					Build (newDevices);
					}
				}
			else
				_devicesList = new ConcurrentDictionary<int, Dictionary<string, object>> ();
			}

		private int GetTempDeviceRoomId (IDictionary<string, object> domainData, int deviceId)
			{
			if (domainData.TryGetValue ("Room", out var rooms) && rooms is List<Dictionary<string, object>> roomsList)
				{
				foreach (var room in roomsList)
					{
					int roomId = Convert.ToInt32 (room["id"], CultureInfo.InvariantCulture);

					if (room.TryGetValue ("SmartValveIds", out var smartValveIds) && smartValveIds is List<object> smartValveIdsList)
						{
						foreach (var id in smartValveIdsList)
							{
							if (Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
								return roomId;
							}
						}
#if HEATACTUATOR
					if (room.TryGetValue ("HeatingActuatorIds", out var heatingActuatorIds) && heatingActuatorIds is List<object> heatingActuatorIdsList)
						{
						foreach (var id in heatingActuatorIdsList)
							{
							if (Convert.ToInt32 (id, CultureInfo.InvariantCulture) == deviceId)
								return roomId;
							}
						}
#endif
					if (room.TryGetValue ("RoomStatId", out var roomStatId))
						{
						if (Convert.ToInt32 (roomStatId, CultureInfo.InvariantCulture) == deviceId)
							return roomId;
						}
					if (room.TryGetValue ("UnderFloorHeatingId", out var ufhId))
						{
						if (Convert.ToInt32 (ufhId, CultureInfo.InvariantCulture) == deviceId)
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

		public List<WiserDevice> All =>
			 _smartvalvesCollection.All.Cast<WiserDevice> ()
			 .Concat (_roomstatsCollection.All)
			 .Concat (_smartplugsCollection.All)
#if HEATACTUATOR
			 .Concat (_heatingActuatorsCollection.All)
#endif
			 .Concat (_ufhControllersCollection.All)
#if SHUTTER
			 .Concat (_shuttersCollection.All)
#endif
#if LIGHT
			 .Concat (_lightsCollection.All)
#endif
			 .ToList ();

		public int Count => All.Count;

#if HEATACTUATOR
		public WiserHeatingActuators HeatingActuators => _heatingActuatorsCollection;
#endif

#if LIGHT
		public WiserLights Lights => _lightsCollection;
#endif

		public WiserRoomStats Roomstats => _roomstatsCollection;

#if SHUTTER
		public WiserShutters Shutters => _shuttersCollection;
#endif

		public WiserSmartPlugs Smartplugs => _smartplugsCollection;

		public WiserSmartValves Smartvalves => _smartvalvesCollection;

		public WiserUFHControllers UfhControllers => _ufhControllersCollection;

		public WiserDevice GetById (int id)
			{
			return All.FirstOrDefault (device => device.Id == id);
			}

		public List<WiserDevice> GetByRoomId (int roomId)
			{
			return All.Where (device =>
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
			).ToList ();
			}

		public WiserDevice GetByNodeId (int nodeId)
			{
			return All.FirstOrDefault (device => device.NodeId == nodeId);
			}

		public WiserDevice GetBySerialNumber (string serialNumber)
			{
			return All.FirstOrDefault (device => device.SerialNumber == serialNumber);
			}

		public List<WiserDevice> GetByParentNodeId (int nodeId)
			{
			return All.Where (device => device.ParentNodeId == nodeId).ToList ();
			}
		}
	}
