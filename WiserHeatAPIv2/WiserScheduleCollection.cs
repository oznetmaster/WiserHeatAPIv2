namespace WiserHeatApiV2
	{
	using log4net;

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public class WiserScheduleCollection
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserScheduleCollection));
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, string> _sunrises;
		private readonly Dictionary<string, string> _sunsets;
		private readonly List<WiserHeatingSchedule> _heatingSchedules = new List<WiserHeatingSchedule> ();
		private readonly List<WiserOnOffSchedule> _onoffSchedules = new List<WiserOnOffSchedule> ();
		private readonly List<WiserLevelSchedule> _levelSchedules = new List<WiserLevelSchedule> ();

		public WiserScheduleCollection (WiserRestController wiserRestController, Dictionary<string, object> scheduleData,
												  Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			{
			_wiserRestController = wiserRestController;
			_sunrises = sunrises;
			_sunsets = sunsets;
			Build (scheduleData);
			}

		private void Build (Dictionary<string, object> scheduleData)
			{
			foreach (var scheduleType in scheduleData.Keys)
				{
				if (scheduleData[scheduleType] is List<Dictionary<string, object>> schedules)
					{
					foreach (var schedule in schedules)
						{
						if (schedule is Dictionary<string, object> scheduleDict)
							{
							if (scheduleType == WiserScheduleTypeEnum.Heating.ToString ())
								{
								_heatingSchedules.Add (new WiserHeatingSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
							if (scheduleType == WiserScheduleTypeEnum.OnOff.ToString ())
								{
								_onoffSchedules.Add (new WiserOnOffSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
							if (scheduleType == WiserScheduleTypeEnum.Level.ToString ())
								{
								_levelSchedules.Add (new WiserLevelSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
							}
						}
					}
				}
			}

		public void Update (Dictionary<string, object> scheduleData, Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			{
			if (scheduleData != null)
				{
				_heatingSchedules.Clear ();
				_onoffSchedules.Clear ();
				_levelSchedules.Clear ();
				Build (scheduleData);
				}
			if (sunrises != null)
				{
				_sunrises.Clear ();
				foreach (var kv in sunrises)
					_sunrises[kv.Key] = kv.Value;
				}
			if (sunsets != null)
				{
				_sunsets.Clear ();
				foreach (var kv in sunsets)
					_sunsets[kv.Key] = kv.Value;
				}
			}

		private async Task<bool> SendScheduleCommandAsync (string action, Dictionary<string, object> scheduleData, int id = 0)
			{
			try
				{
				bool result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		public List<WiserSchedule> All => _heatingSchedules.Cast<WiserSchedule> ()
			 .Concat (_onoffSchedules.Cast<WiserSchedule> ())
			 .Concat (_levelSchedules.Cast<WiserSchedule> ())
			 .ToList ();

		public int Count => All.Count;

		public List<WiserHeatingSchedule> HeatingSchedules => _heatingSchedules;

		public List<WiserLevelSchedule> LevelSchedules => _levelSchedules;

		public List<WiserOnOffSchedule> OnoffSchedules => _onoffSchedules;

		public WiserSchedule GetById (WiserScheduleTypeEnum scheduleType, int id)
			{
			// Adjust schedule type for lighting and shutters
			if (scheduleType == WiserScheduleTypeEnum.Lighting || scheduleType == WiserScheduleTypeEnum.Shutters)
				{
				scheduleType = WiserScheduleTypeEnum.Level;
				}

			try
				{
				if (scheduleType == WiserScheduleTypeEnum.Level)
					{
					return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Id == id);
					}
				return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Id == id);
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserHeatingSchedule GetByRoomId (int roomId)
			{
			try
				{
				return _heatingSchedules.FirstOrDefault (s => s.AssignmentIds.Contains (roomId));
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserSchedule GetByDeviceId (int deviceId)
			{
			try
				{
				return _onoffSchedules.Concat<WiserSchedule> (_levelSchedules)
					 .FirstOrDefault (s => s.DeviceIds.Contains (deviceId));
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserSchedule GetByName (WiserScheduleTypeEnum scheduleType, string name)
			{
			try
				{
				if (scheduleType == WiserScheduleTypeEnum.Level)
					{
					return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Name == name);
					}
				return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Name == name);
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public List<WiserSchedule> GetByType (WiserScheduleTypeEnum scheduleType)
			{
			if (scheduleType == WiserScheduleTypeEnum.Heating)
				{
				return _heatingSchedules.Cast<WiserSchedule> ().ToList ();
				}
			if (scheduleType == WiserScheduleTypeEnum.OnOff)
				{
				return _onoffSchedules.Cast<WiserSchedule> ().ToList ();
				}
			if (scheduleType == WiserScheduleTypeEnum.Level)
				{
				return _levelSchedules.Cast<WiserSchedule> ().ToList ();
				}

			return All.Where (s => s.ScheduleType == scheduleType.ToString ()).ToList ();
			}

		public async Task<bool> CopyScheduleAsync (WiserScheduleTypeEnum scheduleType, int fromId, int toId)
			{
			var fromSchedule = GetById (scheduleType, fromId);
			var toSchedule = GetById (scheduleType, toId);

			if (fromSchedule != null && toSchedule != null)
				{
				if (fromSchedule.ScheduleType == toSchedule.ScheduleType)
					{
					return await fromSchedule.CopyScheduleAsync (toId).ConfigureAwait (false);
					}
				else
					{
					Console.WriteLine ($"You cannot copy from {fromSchedule.ScheduleType} to {toSchedule.ScheduleType} schedules. They must be of the same type");
					}
				}
			else
				{
				Console.WriteLine ($"Invalid schedule id for {(fromSchedule == null ? "from_id" : "to_id")}");
				}
			return false;
			}

		public async Task<bool> CreateScheduleAsync (WiserScheduleTypeEnum scheduleType, string name, List<int> assignments = null)
			{
			if (assignments == null)
				{
				assignments = new List<int> ();
				}

			var typeData = new Dictionary<string, object> { { "Name", name } };

			if (scheduleType == WiserScheduleTypeEnum.Lighting || scheduleType == WiserScheduleTypeEnum.Level)
				{
				typeData["Type"] = 1;
				foreach (var kvp in Constants.DEFAULT_LEVEL_SCHEDULE)
					{
					typeData[kvp.Key] = kvp.Value;
					}
				scheduleType = WiserScheduleTypeEnum.Level;
				}

			if (scheduleType == WiserScheduleTypeEnum.Shutters)
				{
				typeData["Type"] = 2;
				foreach (var kvp in Constants.DEFAULT_LEVEL_SCHEDULE)
					{
					typeData[kvp.Key] = kvp.Value;
					}
				scheduleType = WiserScheduleTypeEnum.Level;
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", assignments },
					 { scheduleType.ToString(), typeData }
				};

			return await SendScheduleCommandAsync ("CREATE", scheduleData).ConfigureAwait (false);
			}
		}
	}
