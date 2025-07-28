//-----------------------------------------------------------------------
// <copyright file="WiserRoom.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserRoom
		{
		private readonly WiserRestController _wiserRestController;
		private readonly ConcurrentDictionary<string, object> _data;
		private string _mode;
		private string _name;
		private bool _windowDetectionActive;

		public WiserRoom (WiserRestController wiserRestController, IDictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			_wiserRestController = wiserRestController;
			_data = new ConcurrentDictionary<string, object> (room);
			Schedule = schedule;
			Devices = devices;
			// Initialize properties from the room data
			Id = _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;
			_mode = EffectiveHeatingMode (
				 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : string.Empty,
				 CurrentTargetTemperature
			);
			_name = room.TryGetValue ("Name", out var name) ? name.ToString () : string.Empty;
			_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection, CultureInfo.InvariantCulture);

			// Add device id to schedule
			Schedule?.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
			}

		private readonly object _lockUpdate = new ();
		public void Update (IDictionary<string, object> room, WiserSchedule schedule, List<WiserDevice> devices)
			{
			lock (_lockUpdate)
				{
				var oldId = Id;
				var oldName = Name;

				var dhs = _data.Keys.ToHashSet<string> ();
				var newKeys = room.Keys.ToHashSet<string> ();
				// Remove keys that are not in the new _data
				foreach (var key in dhs.Except (newKeys))
					{
					_ = _data.TryRemove (key, out _);
					}

				foreach (KeyValuePair<string, object> kvp in room)
					{
					_data[kvp.Key] = kvp.Value;
					}

				var dhi = Devices.Select (d => d.Id).ToHashSet ();
				var newDeviceIds = devices.Select (d => d.Id).ToHashSet ();
				var deletedDevices = dhi.Except (newDeviceIds).ToList ();
				var addedDevices = newDeviceIds.Except (dhi).ToList ();
				// Remove devices that are not in the new data
				_ = Devices.RemoveAll (d => deletedDevices.Contains (d.Id));

				foreach (WiserDevice device in devices)
					{
					if (!dhi.Contains (device.Id))
						{
						// Add new device if it doesn't already exist
						Devices.Add (device);
						}
					}

				_mode = EffectiveHeatingMode (
					 _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : string.Empty,
					 CurrentTargetTemperature
					);

				Id = room.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;
				_name = room.TryGetValue ("Name", out var name) ? name.ToString () : string.Empty;
				_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && Convert.ToBoolean (detection, CultureInfo.InvariantCulture);

				// Add device id to schedule
				if (schedule != null)
					{
					if (schedule.Assignments.Count != 0 && (oldId != Id || oldName != Name) /*|| _schedule.Assignments.Any (a => (int)a["id"] == oldId || (string)a["name"] == oldName)*/)
						{
						// Remove old assignment if the id or name has changed
						_ = schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
						}

					if (!schedule.Assignments.Any (a => (int)a["id"] == Id && (string)a["name"] == Name))
						{
						// Add new assignment if it doesn't already exist
						schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
						}
					}
				}
			}

		private static string EffectiveHeatingMode (string mode, double temp)
			{
			if (mode.Equals (Constants.TextManual, StringComparison.OrdinalIgnoreCase) && temp == Constants.TempOff)
				{
				return WiserHeatingMode.Off.ToString ();
				}
			else if (mode.Equals (Constants.TextManual, StringComparison.OrdinalIgnoreCase))
				{
				return WiserHeatingMode.Manual.ToString ();
				}

			return WiserHeatingMode.Auto.ToString ();
			}

		private Task<bool> SendCommandAsync (object? cmd, WiserRestAction method = WiserRestAction.PATCH, CancellationToken cancellationToken = default) =>
			_wiserRestController.SendCommandAsync (
				string.Format (CultureInfo.InvariantCulture, RestConstants.WiserRoom, Id),
				cmd,
				method,
				cancellationToken
			);

		public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserHeatingMode))
			 .Cast<WiserHeatingMode> ()
			 .Select (m => m.ToString ())];

		public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && Convert.ToBoolean (suppressed, CultureInfo.InvariantCulture);

		public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && Convert.ToInt32 (time, CultureInfo.InvariantCulture) > 0
			 ? DateTimeOffset.FromUnixTimeSeconds (Convert.ToInt32 (time, CultureInfo.InvariantCulture)).DateTime
			 : DateTime.MinValue;

		public DateTime BoostEndTimeLocal => BoostEndTime.ToLocalTime ();

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTimeLocal - DateTime.Now).TotalSeconds
			 : 0;

		public int ComfortModeScore => _data.TryGetValue ("ComfortModeScore", out var score) ? Convert.ToInt32 (score, CultureInfo.InvariantCulture) : 0;

		public string ControlDirection => _data.TryGetValue ("ControlDirection", out var direction) ? direction.ToString () : Constants.TextUnknown;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CurrentSetPoint", out var setPoint) ? setPoint : Constants.TempMinimum);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("CalculatedTemperature", out var temp) ? temp : Constants.TempMinimum, "current");

		public int? CurrentHumidity
			{
			get
				{
				foreach (WiserDevice device in Devices)
					{
					if (device is WiserRoomStat roomStat)
						{
						return roomStat.CurrentHumidity;
						}
					}

				return null;
				}
			}

		public string DemandType => _data.TryGetValue ("DemandType", out var type) ? type.ToString () : Constants.TextUnknown;

		public List<WiserDevice> Devices { get; }

		public double DisplayedSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("DisplayedSetPoint", out var setPoint) ? setPoint : Constants.TempMinimum, "current");

#if HEATACTUATOR
		public List<int> HeatingActuatorIds => _data.TryGetValue ("HeatingActuatorIds", out var ids) && ids is List<object> idsList
			 ? [.. idsList.Select (id => Convert.ToInt32 (id, CultureInfo.InvariantCulture)).OrderBy (id => id)]
			 : new List<int> ();
#endif
		public string HeatingRate => _data.TryGetValue ("HeatingRate", out var rate) ? rate.ToString () : Constants.TextUnknown;

		public string HeatingType => _data.TryGetValue ("HeatingType", out var type) ? type.ToString () : Constants.TextUnknown;

		public int Id { get; private set; }

		public bool IsAwayMode => (_data.TryGetValue ("SetpointOrigin", out var origin) && origin.ToString ().Contains ("Away")) ||
										 (_data.TryGetValue ("SetPointOrigin", out var origin2) && origin2.ToString ().Contains ("Away"));

		public bool IsBoost => (_data.TryGetValue ("SetpointOrigin", out var origin) && origin.ToString ().Contains ("Boost")) ||
									 (_data.TryGetValue ("SetPointOrigin", out var origin2) && origin2.ToString ().Contains ("Boost"));

		public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
										 type.ToString () != Constants.TextUnknown &&
										 type.ToString () != Constants.TextNone;

		public bool IsHeating => _data.TryGetValue ("ControlOutputState", out var state) && state.ToString () == Constants.TextOn;

		public double ManualTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ManualSetPoint", out var setPoint) ? setPoint : Constants.TempMinimum);

		public string Mode
			{
			get => _mode;
			set
				{
				// If the mode is already set to the desired value, no need to change
				if (_mode == value)
					{
					return; // No change needed
					}
				// For cancellation support, use SetModeAsync instead
				_ = SetModeAsync (value).GetAwaiter ().GetResult ();
				}
			}

		public async Task<bool> SetModeAsync (string value, CancellationToken cancellationToken = default)
			{
			try
				{
				var mode = (WiserHeatingMode)Enum.Parse (typeof (WiserHeatingMode), value, true);

				// Cancel any overrides on mode change
				if (IsOverride)
					{
					_ = await CancelOverridesAsync (cancellationToken).ConfigureAwait (false);
					}

				if (mode == WiserHeatingMode.Off)
					{
					_ = await SetManualTemperatureAsync (Constants.TempOff, cancellationToken).ConfigureAwait (false);
					}
				else if (mode == WiserHeatingMode.Manual)
					{
					if (await SendCommandAsync (new { Mode = WiserHeatingMode.Manual.ToString () }, cancellationToken: cancellationToken).ConfigureAwait (false))
						{
						if (CurrentTargetTemperature == Constants.TempOff)
							{
							_ = await SetTargetTemperatureAsync (ScheduledTargetTemperature, cancellationToken).ConfigureAwait (false);
							}
						}
					}
				else if (mode == WiserHeatingMode.Auto)
					{
					_ = await SendCommandAsync (new { Mode = WiserHeatingMode.Auto.ToString () }, cancellationToken: cancellationToken).ConfigureAwait (false);
					}

				_mode = mode.ToString ();
				return true;
				}
			catch (ArgumentException)
				{
				throw new ArgumentException ($"{value} is not a valid Heating mode. Valid modes are {string.Join (", ", AvailableModes)}");
				}
			}

		public string Name
			{
			get => _name;
			set
				{
				// If the name is already set to the desired value, no need to change
				if (_name == value)
					{
					return; // No change needed
					}
				// For cancellation support, use SetNameAsync instead
				_ = SetNameAsync (value).GetAwaiter ().GetResult ();
				}
			}

		public async Task<bool> SetNameAsync (string value, CancellationToken cancellationToken = default)
			{
			if (await SendCommandAsync (new { Name = value.Title () }, cancellationToken: cancellationToken).ConfigureAwait (false))
				{
				_name = value.Title ();
				return true;
				}

			return false;
			}

#if HEATACTUATOR
		public int NumberOfHeatingActuators => HeatingActuatorIds.Count;
#endif

		public int NumberOfSmartvalves => SmartvalveIds.Count;

		public double OverrideTargetTemperature => _data.TryGetValue ("OverrideSetpoint", out var setPoint) ? Convert.ToDouble (setPoint, CultureInfo.InvariantCulture) / 10 : 0;

		public string OverrideType => _data.TryGetValue ("OverrideType", out var type) ? type.ToString () : Constants.TextNone;

		public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand, CultureInfo.InvariantCulture) : 0;

		public int? RoomstatId => _data.TryGetValue ("RoomStatId", out var id) ? (int?)Convert.ToInt32 (id, CultureInfo.InvariantCulture) : null;

		public WiserSchedule? Schedule { get; }

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public double ScheduledTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ScheduledSetPoint", out var setPoint) ? setPoint : Constants.TempMinimum);

		public List<int> SmartvalveIds => _data.TryGetValue ("SmartValveIds", out var ids) && ids is List<object> idsList
			 ? [.. idsList.Select (id => Convert.ToInt32 (id, CultureInfo.InvariantCulture)).OrderBy (id => id)]
			 : new List<int> ();

		public string TargetTemperatureOrigin => _data.TryGetValue ("SetpointOrigin", out var origin)
			 ? origin.ToString ()
			 : _data.TryGetValue ("SetPointOrigin", out var origin2)
				  ? origin2.ToString ()
				  : Constants.TextUnknown;

		public int? UnderfloorHeatingId => _data.TryGetValue ("UnderFloorHeatingId", out var id) ? (int?)Convert.ToInt32 (id, CultureInfo.InvariantCulture) : null;

		public List<int> UnderfloorHeatingRelayIds => _data.TryGetValue ("UfhRelayIds", out var ids) && ids is List<object> idsList
			 ? [.. idsList.Select (id => Convert.ToInt32 (id, CultureInfo.InvariantCulture)).OrderBy (id => id)]
			 : new List<int> ();

		public bool WindowDetectionActive
			{
			get => _windowDetectionActive;
			set
				{
				// If the window detection is already set to the desired value, no need to change
				if (_windowDetectionActive == value)
					{
					return; // No change needed
					}
				// For cancellation support, use SetWindowDetectionActiveAsync instead
				_ = SetWindowDetectionActiveAsync (value).GetAwaiter ().GetResult ();
				}
			}

		public async Task<bool> SetWindowDetectionActiveAsync (bool value, CancellationToken cancellationToken = default)
			{
			if (await SendCommandAsync (new { WindowDetectionActive = value }, cancellationToken: cancellationToken).ConfigureAwait (false))
				{
				_windowDetectionActive = value;
				return true;
				}

			return false;
			}

		public string WindowState => _data.TryGetValue ("WindowState", out var state) ? state.ToString () : Constants.TextUnknown;

		public Task<bool> DeleteAsync (CancellationToken cancellationToken = default) =>
			SendCommandAsync (null, WiserRestAction.DELETE, cancellationToken);

		public Task<bool> BoostAsync (double incTemp, int duration, CancellationToken cancellationToken = default) =>
			duration == 0
				? CancelBoostAsync (cancellationToken)
				: SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = "Boost",
						DurationMinutes = duration,
						IncreaseSetPointBy = WiserTemperatureFunctions.ToWiserTemp (incTemp, "delta")
						}
					}, cancellationToken: cancellationToken);

		public Task<bool> CancelBoostAsync (CancellationToken cancellationToken = default) =>
			IsBoost ? CancelOverridesAsync (cancellationToken) : Task.FromResult (true);

		public Task<bool> SetTargetTemperatureAsync (double temp, CancellationToken cancellationToken = default) =>
			SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TextManual,
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}, cancellationToken: cancellationToken);

		public Task<bool> SetTargetTemperatureForDurationAsync (double temp, int duration, CancellationToken cancellationToken = default) =>
			SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TextManual,
					DurationMinutes = duration,
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}, cancellationToken: cancellationToken);

		public Task<bool> SetTargetTemperatureForDurationOfScheduleAsync (double temp, CancellationToken cancellationToken = default) =>
			Schedule == null || Schedule.Next == null
				? throw new InvalidOperationException ("No next schedule available to set duration.")
				: SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TextManual,
						DurationMinutes = (int)Math.Ceiling ((Schedule.Next.DateTime - DateTime.Now).TotalMinutes),
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
						}
					}, cancellationToken: cancellationToken);

		public Task<bool> SetManualTemperatureAsync (double temp, CancellationToken cancellationToken = default)
			{
			if (Mode != WiserHeatingMode.Manual.ToString ())
				{
				Mode = WiserHeatingMode.Manual.ToString ();
				}

			return SetTargetTemperatureAsync (temp, cancellationToken);
			}

		public async Task<bool> ScheduleAdvanceAsync (CancellationToken cancellationToken = default) =>
			Schedule == null || Schedule.Next == null
				? throw new InvalidOperationException ("No next schedule available to advance.")
				: await CancelBoostAsync (cancellationToken).ConfigureAwait (false) && await SetTargetTemperatureAsync (Convert.ToDouble (Schedule.Next.Setting, CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait (false);

		public Task<bool> CancelOverridesAsync (CancellationToken cancellationToken = default) =>
			SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TextNone
					}
				}, cancellationToken: cancellationToken);
		}

	public class WiserRooms
		{
		private readonly WiserRestController _wiserRestController;

		public WiserRooms (WiserRestController wiserRestController, List<Dictionary<string, object>> roomData,
														  WiserSchedules schedules, WiserDevices devices)
			{
			_wiserRestController = wiserRestController;
			// Add room objects
			foreach (Dictionary<string, object> room in roomData)
				{
				WiserSchedule schedule = schedules.GetByType (WiserScheduleType.Heating)
					  .FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));
				List<WiserDevice> roomDevices = devices.GetByRoomId (room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId, CultureInfo.InvariantCulture) : 0);
				All.Add (new WiserRoom (
					  wiserRestController,
					  room,
					  schedule,
					  roomDevices
				));
				}
			}

		public void Update (List<Dictionary<string, object>> roomData, WiserSchedules schedules, WiserDevices devices)
			{
			// For simplicity, just rebuild the collection
			// (You can optimize this if needed)
			// This assumes you have a Build method or similar
			// Build(roomData, schedules, devices);

			// Remove rooms that are not in the new _data
			var newRoomIds = new HashSet<int> (roomData.Select (r => r.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));
			_ = All.RemoveAll (room => !newRoomIds.Contains (room.Id));

			// Update existing rooms or add new ones
			foreach (Dictionary<string, object> room in roomData)
				{
				WiserSchedule schedule = schedules.GetByType (WiserScheduleType.Heating)
					.FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0));
				var idroom = room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId, CultureInfo.InvariantCulture) : 0;
				List<WiserDevice> roomDevices = devices.GetByRoomId (idroom);
				WiserRoom? existingRoom = All.FirstOrDefault (r => r.Id == idroom);
				if (existingRoom != null)
					{
					existingRoom.Update (room, schedule, roomDevices);
					}
				else
					{
					All.Add (new WiserRoom (
						_wiserRestController,
						room,
						schedule,
						roomDevices
					));
					}
				}
			}

		public List<WiserRoom> All { get; } = [];
		public int Count => All.Count;
		public Task<bool> AddAsync (string name, CancellationToken cancellationToken = default) =>
			_wiserRestController.SendCommandAsync (RestConstants.WiserRoom, new
			{
			name
			}, WiserRestAction.POST, cancellationToken);
		public WiserRoom GetById (int id) => All.FirstOrDefault (room => room.Id == id);
		public WiserRoom GetByName (string name) => All.FirstOrDefault (room => room.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
		public WiserRoom GetByScheduleId (int scheduleId) => All.FirstOrDefault (room => room.ScheduleId == scheduleId);
		public WiserRoom? GetByDeviceId (int deviceId)
			{
			foreach (WiserRoom room in All)
				{
				foreach (WiserDevice device in room.Devices)
					{
					if (device.Id == deviceId)
						return room;
					}
				}

			return null;
			}
		}
	}
