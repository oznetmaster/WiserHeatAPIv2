// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{	public class WiserDevice
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

	public class WiserDeviceCollection
		{
		private readonly WiserRestController _wiserRestController;
		private Dictionary<string, object> _deviceData;
		private Dictionary<string, object> _domainData;
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

		public WiserDeviceCollection (WiserRestController wiserRestController, Dictionary<string, object> domainData, WiserScheduleCollection schedules)
			{
			_wiserRestController = wiserRestController;
			_deviceData = domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList
				 ? new Dictionary<string, object> { { "Device", devicesList } }
				 : new Dictionary<string, object> ();
			_domainData = domainData;
			_schedules = schedules;

			Build ();
			}

		private void Build ()
			{
			if (_deviceData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList)
				{
				foreach (var deviceObj in devicesList)
					{
					if (deviceObj is Dictionary<string, object> device)
						{
						// Add smart valve (iTRV) object to collection
						if (device.TryGetValue ("ProductType", out var productType) && productType.ToString () == "iTRV")
							{
							var smartvalveInfo = _domainData.TryGetValue ("SmartValve", out var smartValves) && smartValves is List<Dictionary<string, object>> smartValvesList
								 ? smartValvesList
									  .Where (sv => sv.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (smartvalveInfo.Count > 0)
								{
								smartvalveInfo[0]["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"]));
								_smartvalvesCollection.All.Add (
									 new WiserSmartValve (
										  _wiserRestController,
										  device,
										  smartvalveInfo[0]
									 )
								);
								}
							}

						// Add room stat object to collection
						else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "RoomStat")
							{
							var roomstatInfo = _domainData.TryGetValue ("RoomStat", out var roomStats) && roomStats is List<Dictionary<string, object>> roomStatsList
								 ? roomStatsList
									  .Where (rs => rs.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (roomstatInfo.Count > 0)
								{
								roomstatInfo[0]["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"]));
								_roomstatsCollection.All.Add (
									 new WiserRoomStat (
										  _wiserRestController,
										  device,
										  roomstatInfo[0]
									 )
								);
								}
							}

						// Add smart plug object to collection
						else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "SmartPlug")
							{
							var smartplugInfo = _domainData.TryGetValue ("SmartPlug", out var smartPlugs) && smartPlugs is List<Dictionary<string, object>> smartPlugsList
								 ? smartPlugsList
									  .Where (sp => sp.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (smartplugInfo.Count > 0)
								{
								var smartplugSchedule = _schedules.GetByType (WiserScheduleTypeEnum.OnOff)
									 .Where (s => s.Id == (smartplugInfo[0].TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0))
									 .ToList ();

								_smartplugsCollection.All.Add (
									 new WiserSmartPlug (
										  _wiserRestController,
										  device,
										  smartplugInfo[0],
										  smartplugSchedule.Count > 0 ? smartplugSchedule[0] : null
									 )
								);
								}
							}

#if HEATACTUATOR
						// Add heating actuator object to collection
						else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "HeatingActuator")
							{
							var heatingActuatorInfo = _domainData.TryGetValue ("HeatingActuator", out var heatingActuators) && heatingActuators is List<Dictionary<string, object>> heatingActuatorsList
								 ? heatingActuatorsList
									  .Where (ha => ha.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (heatingActuatorInfo.Count > 0)
								{
								heatingActuatorInfo[0]["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"]));
								_heatingActuatorsCollection.All.Add (
									 new WiserHeatingActuator (
										  _wiserRestController,
										  device,
										  heatingActuatorInfo[0]
									 )
								);
								}
							}
#endif
						// Add ufh controller object to collection
						else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "UnderFloorHeating")
							{
							var ufhControllerInfo = _domainData.TryGetValue ("UnderFloorHeating", out var ufhControllers) && ufhControllers is List<Dictionary<string, object>> ufhControllersList
								 ? ufhControllersList
									  .Where (ufh => ufh.TryGetValue ("id", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (ufhControllerInfo.Count > 0)
								{
								ufhControllerInfo[0]["RoomId"] = GetTempDeviceRoomId (_domainData, Convert.ToInt32 (device["id"]));
								_ufhControllersCollection.All.Add (
									 new WiserUFHController (
										  _wiserRestController,
										  device,
										  ufhControllerInfo[0]
									 )
								);
								}
							}
#if SHUTTER
						// Add shutter object to collection
						else if (device.TryGetValue ("ProductType", out productType) && productType.ToString () == "Shutter")
							{
							var shutterInfo = _domainData.TryGetValue ("Shutter", out var shutters) && shutters is List<Dictionary<string, object>> shuttersList
								 ? shuttersList
									  .Where (s => s.TryGetValue ("DeviceId", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (shutterInfo.Count > 0)
								{
								var shutterSchedule = _schedules.GetByType (WiserScheduleTypeEnum.Level)
									 .Where (s => s.Id == (shutterInfo[0].TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0))
									 .ToList ();

								_shuttersCollection.All.Add (
									 new WiserShutter (
										  _wiserRestController,
										  device,
										  shutterInfo[0],
										  shutterSchedule.Count > 0 ? shutterSchedule[0] : null
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
							var lightInfo = _domainData.TryGetValue ("Light", out var lights) && lights is List<Dictionary<string, object>> lightsList
								 ? lightsList
									  .Where (l => l.TryGetValue ("DeviceId", out var id) && Convert.ToInt32 (id) == Convert.ToInt32 (device["id"]))
									  .ToList ()
								 : new List<Dictionary<string, object>> ();

							if (lightInfo.Count > 0)
								{
								var lightSchedule = _schedules.GetByType (WiserScheduleTypeEnum.Level)
									 .Where (s => s.Id == (lightInfo[0].TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0))
									 .ToList ();

								if (productType.ToString () == "DimmableLight")
									{
									_lightsCollection.All.Add (
										 new WiserDimmableLight (
											  _wiserRestController,
											  device,
											  lightInfo[0],
											  lightSchedule.Count > 0 ? lightSchedule[0] : null
										 )
									);
									}
								else
									{
									_lightsCollection.All.Add (
										 new WiserLight (
											  _wiserRestController,
											  device,
											  lightInfo[0],
											  lightSchedule.Count > 0 ? lightSchedule[0] : null
										 )
									);
									}
								}
							}
#endif
						}
					}
				}
			}

		public void Update (Dictionary<string, object> domainData, WiserScheduleCollection schedules)
			{
			_deviceData = domainData.TryGetValue ("Device", out var devices) && devices is List<Dictionary<string, object>> devicesList
				 ? new Dictionary<string, object> { { "Device", devicesList } }
				 : new Dictionary<string, object> ();
			_domainData = domainData;
			_schedules = schedules;

			Build ();
			}

		private int GetTempDeviceRoomId (Dictionary<string, object> domainData, int deviceId)
			{
			if (domainData.TryGetValue ("Room", out var rooms) && rooms is List<Dictionary<string, object>> roomsList)
				{
				foreach (var roomObj in roomsList)
					{
					if (roomObj is Dictionary<string, object> room)
						{
						var roomDeviceList = new List<int> ();

						if (room.TryGetValue ("SmartValveIds", out var smartValveIds) && smartValveIds is List<object> smartValveIdsList)
							roomDeviceList.AddRange (smartValveIdsList.Select (id => Convert.ToInt32 (id)));

						if (room.TryGetValue ("HeatingActuatorIds", out var heatingActuatorIds) && heatingActuatorIds is List<object> heatingActuatorIdsList)
							roomDeviceList.AddRange (heatingActuatorIdsList.Select (id => Convert.ToInt32 (id)));

						if (room.TryGetValue ("RoomStatId", out var roomStatId))
							roomDeviceList.Add (Convert.ToInt32 (roomStatId));

						if (room.TryGetValue ("UnderFloorHeatingId", out var ufhId))
							roomDeviceList.Add (Convert.ToInt32 (ufhId));

						if (roomDeviceList.Contains (deviceId))
							return Convert.ToInt32 (room["id"]);
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
