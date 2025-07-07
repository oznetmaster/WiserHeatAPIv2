//-----------------------------------------------------------------------
// <copyright file="WiserRoom.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserRoom
		{
		private readonly WiserRestController _wiserRestController;
		private readonly ConcurrentDictionary<string, object> _data;
		private WiserSchedule _schedule;
		private readonly List<WiserDevice> _devices;
		private string _mode;
		private string _name;
		private bool _windowDetectionActive;

		public WiserRoom (WiserRestController wiserRestController, IDictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			_wiserRestController = wiserRestController;
			_data = new ConcurrentDictionary<string, object> (room);
			_schedule = schedule;
			_devices = devices;
			_mode = EffectiveHeatingMode (
				 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : string.Empty,
				 CurrentTargetTemperature
			);
			_name = room.TryGetValue ("Name", out var name) ? name.ToString () : string.Empty;
			_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				}
			}

		object _lockUpdate = new object ();
		public void Update (IDictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			lock (_lockUpdate)
				{
				var oldId = Id;
				var oldName = Name;

				var dhs = _data.Keys.ToHashSet<string> ();
				var newKeys = room.Keys.ToHashSet<string> ();
				// Remove keys that are not in the new data
				foreach (var key in dhs.Except (newKeys))
					{
					_data.TryRemove (key, out _);
					}

				foreach (var kvp in room)
					{
					_data[kvp.Key] = kvp.Value;
					}

				var dhi = _devices.Select (d => d.Id).ToHashSet ();
				var newDeviceIds = devices.Select (d => d.Id).ToHashSet ();
				var deletedDevices = dhi.Except (newDeviceIds).ToList ();
				var addedDevices = newDeviceIds.Except (dhi).ToList ();
				// Remove devices that are not in the new data
				_devices.RemoveAll (d => deletedDevices.Contains (d.Id));

				foreach (var device in devices) 
					{
					if (!dhi.Contains (device.Id))
						{
						// Add new device if it doesn't already exist
						_devices.Add (device);
						}
					}

				_mode = EffectiveHeatingMode (
					 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : string.Empty,
					 CurrentTargetTemperature
					);

				_name = room.TryGetValue ("Name", out var name) ? name.ToString () : string.Empty;
				_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection);

				// Add device id to schedule
				if (schedule != null)
					{
					if (schedule.Assignments.Count != 0 && (oldId != Id || oldName != Name) /*|| _schedule.Assignments.Any (a => (int)a["id"] == oldId || (string)a["name"] == oldName)*/)
						{
						// Remove old assignment if the id or name has changed
						schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
						}
					if (!schedule.Assignments.Any (a => (int)a["id"] == Id && (string)a["name"] == Name))
						{
						// Add new assignment if it doesn't already exist
						schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
						}
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

		public DateTime BoostEndTimeLocal => BoostEndTime.ToLocalTime ();

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTimeLocal - DateTime.Now).TotalSeconds
			 : 0;

		public int ComfortModeScore => _data.TryGetValue ("ComfortModeScore", out var score) ? Convert.ToInt32 (score) : 0;

		public string ControlDirection => _data.TryGetValue ("ControlDirection", out var direction) ? direction.ToString () : Constants.TEXT_UNKNOWN;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CurrentSetPoint", out var setPoint) ? setPoint : Constants.TEMP_MINIMUM);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CalculatedTemperature", out var temp) ? temp : Constants.TEMP_MINIMUM, "current");

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
			 _data.TryGetValue ("DisplayedSetPoint", out var setPoint) ? setPoint : Constants.TEMP_MINIMUM, "current");

#if HEATACTUATOR
		public List<int> HeatingActuatorIds => _data.TryGetValue ("HeatingActuatorIds", out var ids) && ids is List<object> idsList
			 ? idsList.Select (id => Convert.ToInt32 (id)).OrderBy (id => id).ToList ()
			 : new List<int> ();
#endif
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
			 _data.TryGetValue ("ManualSetPoint", out var setPoint) ? setPoint : Constants.TEMP_MINIMUM);

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

#if HEATACTUATOR
		public int NumberOfHeatingActuators => HeatingActuatorIds.Count;
#endif

		public int NumberOfSmartvalves => SmartvalveIds.Count;

		public double OverrideTargetTemperature => _data.TryGetValue ("OverrideSetpoint", out var setPoint) ? Convert.ToDouble (setPoint) / 10 : 0;

		public string OverrideType => _data.TryGetValue ("OverrideType", out var type) ? type.ToString () : Constants.TEXT_NONE;

		public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		public int? RoomstatId => _data.TryGetValue ("RoomStatId", out var id) ? (int?)Convert.ToInt32 (id) : null;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public double ScheduledTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ScheduledSetPoint", out var setPoint) ? setPoint : Constants.TEMP_MINIMUM);

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
	public class WiserRoomCollection
		{
		private readonly WiserRestController _wiserRestController;
		private readonly List<WiserRoom> _rooms = new List<WiserRoom> ();

		public WiserRoomCollection (WiserRestController wiserRestController, List<Dictionary<string, object>> roomData,
														  WiserScheduleCollection schedules, WiserDeviceCollection devices)
			{
			_wiserRestController = wiserRestController;
			// Add room objects
			foreach (var room in roomData)
				{
				var schedule = schedules.GetByType (WiserScheduleTypeEnum.Heating)
					  .FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));
				var roomDevices = devices.GetByRoomId (room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId) : 0);
				_rooms.Add (new WiserRoom (
					  wiserRestController,
					  room,
					  schedule,
					  roomDevices
				));
				}
			}

		public void Update (List<Dictionary<string, object>> roomData, WiserScheduleCollection schedules, WiserDeviceCollection devices)
			{
			// For simplicity, just rebuild the collection
			// (You can optimize this if needed)
			// This assumes you have a Build method or similar
			// Build(roomData, schedules, devices);

			// Remove rooms that are not in the new data
			var newRoomIds = new HashSet<int> (roomData.Select (r => r.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0));
			_rooms.RemoveAll (room => !newRoomIds.Contains (room.Id));

			// Update existing rooms or add new ones
			foreach (var room in roomData)
				{
				var schedule = schedules.GetByType (WiserScheduleTypeEnum.Heating)
					.FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));
				var idroom = room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId) : 0;
				var roomDevices = devices.GetByRoomId (idroom);
				var existingRoom = _rooms.FirstOrDefault (r => r.Id == idroom);
				if (existingRoom != null)
					existingRoom.Update (room, schedule, roomDevices);
				else
					{
					_rooms.Add (new WiserRoom (
						_wiserRestController,
						room,
						schedule,
						roomDevices
					));
					}
				}
			}

		public List<WiserRoom> All => _rooms;
		public int Count => _rooms.Count;
		public async Task<bool> AddAsync (string name)
			{
			return await _wiserRestController.SendCommandAsync (RestConstants.WISERROOM, new
				{
				name = name
				}, WiserRestActionEnum.POST).ConfigureAwait (false);
			}
		public WiserRoom GetById (int id)
			{
			return _rooms.FirstOrDefault (room => room.Id == id);
			}
		public WiserRoom GetByName (string name)
			{
			return _rooms.FirstOrDefault (room => room.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			}
		public WiserRoom GetByScheduleId (int scheduleId)
			{
			return _rooms.FirstOrDefault (room => room.ScheduleId == scheduleId);
			}
		public WiserRoom GetByDeviceId (int deviceId)
			{
			foreach (var room in _rooms)
				{
				foreach (var device in room.Devices)
					{
					if (device.Id == deviceId)
						return room;
					}
				}
			return null;
			}
		}
	}
