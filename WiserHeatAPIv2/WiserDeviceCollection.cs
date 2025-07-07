//-----------------------------------------------------------------------
// <copyright file="WiserDeviceCollection.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{
	public class WiserDevice
		{
		protected readonly IDictionary<string, object> _data;
		protected readonly WiserSignalStrength _signal;

		public WiserDevice (IDictionary<string, object> data)
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

	public class WiserDeviceCollection
		{
		private readonly WiserRestController _wiserRestController;
		private ConcurrentDictionary<int, Dictionary<string, object>> _devicesList;
		private IDictionary<string, object> _domainData;
		private WiserScheduleCollection _schedules;

		private readonly WiserSmartValveCollection _smartvalvesCollection = new WiserSmartValveCollection ();
		private readonly WiserRoomStatCollection _roomstatsCollection = new WiserRoomStatCollection ();
		private readonly WiserSmartPlugCollection _smartplugsCollection = new WiserSmartPlugCollection ();
#if HEATACTUATOR
		private readonly WiserHeatingActuatorCollection _heatingActuatorsCollection = new WiserHeatingActuatorCollection ();
#endif
		private readonly WiserUFHControllerCollection _ufhControllersCollection = new WiserUFHControllerCollection ();
#if SHUTTER
		private readonly WiserShutterCollection _shuttersCollection = new WiserShutterCollection ();
#endif
#if LIGHT
		private readonly WiserLightCollection _lightsCollection = new WiserLightCollection ();
#endif

		public WiserDeviceCollection (WiserRestController wiserRestController, IDictionary<string, object> domainData, WiserScheduleCollection schedules)
			{
			_wiserRestController = wiserRestController;
			if (domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
				_devicesList = new ConcurrentDictionary<int, Dictionary<string, object>> (
					devicesList.ToDictionary (d => Convert.ToInt32 (d["id"]), d => d)
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
				var deviceId = Convert.ToInt32 (device["id"]);

				// Add smart valve (iTRV) object to collection
				if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "iTRV")
					{
					var smartvalveInfo = (_domainData.TryGetValue ("SmartValve", out var smartValves) && smartValves is List<Dictionary<string, object>> smartValvesList)
						? smartValvesList.FirstOrDefault (sv => sv.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == deviceId)
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
						? roomStatsList.FirstOrDefault (rs => rs.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == deviceId)
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
						? smartPlugsList.FirstOrDefault (sp => sp.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == deviceId)
						: null;

					if (smartplugInfo != null)
						{
						var smartplugSchedule = _schedules.GetByType (WiserScheduleTypeEnum.OnOff)
							 .FirstOrDefault (s => s.Id == (smartplugInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));

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
							var heatingActuatorInfo = (_domainData.TryGetValue("HeatingActuator", out var heatingActuators) && heatingActuators is List<Dictionary<string, object>> heatingActuatorsList)
								? heatingActuatorsList.FirstOrDefault(ha => ha.TryGetValue("id", out var id) && Convert.ToInt32(id) == Convert.ToInt32(device["id"]))
								: null;

							if (heatingActuatorInfo != null)
								{
								heatingActuatorInfo["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"]));
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
						? ufhControllersList.FirstOrDefault (ufh => ufh.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == deviceId)
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
							var shutterInfo = (_domainData.TryGetValue("Shutter", out var shutters) && shutters is List<Dictionary<string, object>> shuttersList)
								? shuttersList.FirstOrDefault(s => s.TryGetValue("DeviceId", out var id) && Convert.ToInt32(id) == Convert.ToInt32(device["id"]))
								: null;

							if (shutterInfo != null)
								{
								var shutterSchedule = _schedules.GetByType (WiserScheduleTypeEnum.Level)
									 .FirstOrDefault(s => s.Id == (shutterInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));

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
							var lightInfo = (_domainData.TryGetValue("Light", out var lights) && lights is List<Dictionary<string, object>> lightsList)
								? lightsList.FirstOrDefault(l => l.TryGetValue("DeviceId", out var id) && Convert.ToInt32(id) == Convert.ToInt32(device["id"]))
								: null;

							if (lightInfo != null)
								{
								var lightSchedule = _schedules.GetByType (WiserScheduleTypeEnum.Level)
									 .FirstOrDefault(s => s.Id == (lightInfo.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));

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

		public void Update (IDictionary<string, object> domainData, WiserScheduleCollection schedules)
			{
			_domainData = domainData;
			_schedules = schedules;
			if (domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
				{
				var dlh = new HashSet<int> (_devicesList.Keys);
				var newDeviceHash = new HashSet<int> (devicesList.Select (d => Convert.ToInt32 (d["id"])));
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
					var newDevices = devicesList.Where (d => addedDevicesHash.Contains (Convert.ToInt32 (d["id"])));
					foreach (var newDevice in newDevices)
						_devicesList[Convert.ToInt32 (newDevice["id"])] = newDevice;

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
					int roomId = Convert.ToInt32 (room["id"]);

					if (room.TryGetValue ("SmartValveIds", out var smartValveIds) && smartValveIds is List<object> smartValveIdsList)
						{
						foreach (var id in smartValveIdsList)
							{
							if (Convert.ToInt32 (id) == deviceId)
								return roomId;
							}
						}
#if HEATACTUATOR
						if (room.TryGetValue("HeatingActuatorIds", out var heatingActuatorIds) && heatingActuatorIds is List<object> heatingActuatorIdsList)
							{
								foreach (var id in heatingActuatorIdsList)
								{
									if (Convert.ToInt32(id) == deviceId)
										return roomId;
								}
							}
#endif
					if (room.TryGetValue ("RoomStatId", out var roomStatId))
						{
						if (Convert.ToInt32 (roomStatId) == deviceId)
							return roomId;
						}
					if (room.TryGetValue ("UnderFloorHeatingId", out var ufhId))
						{
						if (Convert.ToInt32 (ufhId) == deviceId)
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
		public WiserHeatingActuatorCollection HeatingActuators => _heatingActuatorsCollection;
#endif

#if LIGHT
		public WiserLightCollection Lights => _lightsCollection;
#endif

		public WiserRoomStatCollection Roomstats => _roomstatsCollection;

#if SHUTTER
		public WiserShutterCollection Shutters => _shuttersCollection;
#endif

		public WiserSmartPlugCollection Smartplugs => _smartplugsCollection;

		public WiserSmartValveCollection Smartvalves => _smartvalvesCollection;

		public WiserUFHControllerCollection UfhControllers => _ufhControllersCollection;

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
