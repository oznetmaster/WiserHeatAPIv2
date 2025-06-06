// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using log4net;

using Newtonsoft.Json;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{

	public abstract class WiserSchedule
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedule));

		protected readonly WiserRestController _wiserRestController;
		protected readonly string _type;
		protected readonly ConcurrentDictionary<string, object> _scheduleData;
		protected readonly Dictionary<string, string> _sunrises;
		protected readonly Dictionary<string, string> _sunsets;
		protected readonly List<Dictionary<string, object>> _assignments = new List<Dictionary<string, object>> ();
		protected readonly List<int> _deviceIds = new List<int> ();

		protected WiserSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
									  Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			{
			_wiserRestController = wiserRestController;
			_type = scheduleType;
			_scheduleData = new ConcurrentDictionary<string, object> (scheduleData);
			_sunrises = sunrises;
			_sunsets = sunsets;
			}

		protected bool ValidateScheduleType (Dictionary<string, object> scheduleData)
			{
			return (scheduleData.TryGetValue ("Type", out var type) && type.ToString () == ScheduleType) ||
					 (scheduleData.TryGetValue ("SubType", out var subType) && subType.ToString () == ScheduleType);
			}

		protected bool IsValidTime (string timeValue)
			{
			return DateTime.TryParseExact (timeValue, "HH:mm", null, System.Globalization.DateTimeStyles.None, out _);
			}

		protected ConcurrentDictionary<string, object> EnsureType (ConcurrentDictionary<string, object> scheduleData)
			{
			if (!scheduleData.ContainsKey ("Type"))
				{
				scheduleData["Type"] = ScheduleType;
				}
			return scheduleData;
			}

		protected ConcurrentDictionary<string, object> RemoveScheduleElements (ConcurrentDictionary<string, object> scheduleData)
			{
			var result = new ConcurrentDictionary<string, object> (scheduleData);
			var removeList = new[] { "id", "CurrentSetpoint", "CurrentState", "Description", "CurrentLevel", "Name", "Next", "Type" };
			foreach (var item in removeList)
				{
				//if (result.ContainsKey (item))
					{
					result.TryRemove (item, out _);
					}
				}
			return result;
			}

		protected Dictionary<string, object> RemoveScheduleElements (Dictionary<string, object> scheduleData)
			{
			var result = new Dictionary<string, object> (scheduleData);
			var removeList = new[] { "id", "CurrentSetpoint", "CurrentState", "Description", "CurrentLevel", "Name", "Next", "Type" };
			foreach (var item in removeList)
				{
				if (result.ContainsKey (item))
					{
					result.Remove (item);
					}
				}
			return result;
			}

		protected abstract Dictionary<string, object> ConvertFromWiserSchedule (ConcurrentDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract ConcurrentDictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData);
		protected abstract List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule);

		protected async Task<bool> SendScheduleCommandAsync (string action, ConcurrentDictionary<string, object> scheduleData, int id = 0)
			{
			try
				{
				bool result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, _type).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		protected async Task<bool> SendScheduleCommandAsync (string action, Dictionary<string, object> scheduleData, int id = 0)
			{
			try
				{
				bool result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, _type).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		public List<int> DeviceIds => _deviceIds;

		public List<Dictionary<string, object>> Assignments => _assignments;

		public List<int> AssignmentIds => _assignments.Select (a => Convert.ToInt32 (a["id"])).ToList ();

		public List<string> AssignmentNames => _assignments.Select (a => a["name"].ToString ()).ToList ();

		public object CurrentSetting
			{
			get
				{
				if (_type == WiserScheduleTypeEnum.Heating.ToString ())
					{
					return WiserTemperatureFunctions.FromWiserTemp (
						 _scheduleData.TryGetValue ("CurrentSetpoint", out var setpoint) ? Convert.ToInt32 (setpoint) : Constants.TEMP_MINIMUM);
					}
				if (_type == WiserScheduleTypeEnum.OnOff.ToString ())
					{
					return _scheduleData.TryGetValue ("CurrentState", out var state) ? state : Constants.TEXT_UNKNOWN;
					}
				if (_type == WiserScheduleTypeEnum.Level.ToString ())
					{
					return _scheduleData.TryGetValue ("CurrentLevel", out var level) ? level : Constants.TEXT_UNKNOWN;
					}
				return null;
				}
			}

		public int Id => _scheduleData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public string Name => _scheduleData.TryGetValue ("Name", out var name) ? name.ToString () : null;

		public WiserScheduleNext Next
			{
			get
				{
				if (_scheduleData.TryGetValue ("Next", out var next) && next is Dictionary<string, object> nextDict)
					{
					return new WiserScheduleNext (_type, nextDict);
					}
				return null;
				}
			}

		public ConcurrentDictionary<string, object> ScheduleData => RemoveScheduleElements (new ConcurrentDictionary<string, object> (_scheduleData));

		public Dictionary<string, object> WsScheduleData
			{
			get
				{
				var s = RemoveScheduleElements (ConvertFromWiserSchedule (ScheduleData, genericSetpoint: true));
				return new Dictionary<string, object>
					 {
						  { "Id", Id },
						  { "Name", Name },
						  { "Type", _type },
						  { "SubType", ScheduleType },
						  { "Assignments", Assignments },
						  { "ScheduleData", s.Select(a => new Dictionary<string, object>
								{
									 { "day", a.Key },
									 { "slots", a.Value }
								}).ToList()
						  }
					 };
				}
			}

		public string ScheduleType => _type;

		public async Task<bool> CopyScheduleAsync (int toId)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (new ConcurrentDictionary<string, object> (_scheduleData)), toId).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error copying schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> DeleteScheduleAsync ()
			{
			try
				{
				if (Id != 1000)
					{
					await SendScheduleCommandAsync ("DELETE", new ConcurrentDictionary<string, object> ()).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ("You cannot delete the schedule for HotWater");
					return false;
					}
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error deleting schedule: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToFile (string scheduleFile)
			{
			try
				{
				File.WriteAllText (scheduleFile, JsonConvert.SerializeObject (EnsureType (_scheduleData), Newtonsoft.Json.Formatting.Indented));
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error saving schedule to file: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToYamlFile (string scheduleYamlFile)
			{
			try
				{
				var serializer = new YamlDotNet.Serialization.Serializer ();
				File.WriteAllText (scheduleYamlFile, serializer.Serialize (ConvertFromWiserSchedule (_scheduleData)));
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error saving schedule to yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleAsync (ConcurrentDictionary<string, object> scheduleData)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData)).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error setting schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleAsync (Dictionary<string, object> scheduleData)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData)).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error setting schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromFileAsync (string scheduleFile)
			{
			try
				{
				var scheduleData = JsonConvert.DeserializeObject<Dictionary<string, object>> (File.ReadAllText (scheduleFile));
				if (ValidateScheduleType (scheduleData))
					{
					await SetScheduleAsync (RemoveScheduleElements (scheduleData)).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"Error setting schedule from file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromYamlFileAsync (string scheduleYamlFile)
			{
			try
				{
				var deserializer = new YamlDotNet.Serialization.Deserializer ();
				var scheduleData = deserializer.Deserialize<Dictionary<string, object>> (File.ReadAllText (scheduleYamlFile));

				if (ValidateScheduleType (scheduleData))
					{
					var schedule = ConvertToWiserSchedule (scheduleData);
					await SetScheduleAsync (schedule).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"This is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error setting schedule from yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromWsDataAsync (Dictionary<string, object> scheduleData)
			{
			try
				{
				if (ValidateScheduleType (scheduleData))
					{
					var scheduleJson = new Dictionary<string, object> ();
					if (scheduleData.TryGetValue ("ScheduleData", out var scheduleDataObj) && scheduleDataObj is List<object> scheduleDataList)
						{
						foreach (var entry in scheduleDataList)
							{
							if (entry is Dictionary<string, object> entryDict)
								{
								scheduleJson[entryDict["day"].ToString ()] = entryDict["slots"];
								}
							}
						}

					var schedule = ConvertToWiserSchedule (scheduleJson);
					await SetScheduleAsync (schedule).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					return false;
					}
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"Error setting schedule from websocket data: {ex.Message}");
				return false;
				}
			}
		}

	public class WiserHeatingSchedule : WiserSchedule
		{
		public WiserHeatingSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
											Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public async Task<bool> AssignScheduleAsync (List<int> roomIds, bool includeCurrent = true)
			{
			if (roomIds == null)
				{
				roomIds = new List<int> { };
				}

			if (includeCurrent)
				{
				roomIds = roomIds.Concat (AssignmentIds).ToList ();
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", roomIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> roomIds)
			{
			if (roomIds == null)
				{
				roomIds = new List<int> { };
				}

			var remainingRoomIds = new List<int> ();
			if (roomIds.Any () && AssignmentIds.Any ())
				{
				remainingRoomIds = AssignmentIds.Where (id => !roomIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingRoomIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayDict = daySchedule as Dictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TEXT_DEGREESC, out var tempObj) && tempObj is List<object> temps)
				{
				for (int i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i]).ToString ("D4");
					var time = DateTime.ParseExact (timeValue, "HHmm", null).ToString ("HH:mm");

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, time },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_TEMP,
								  WiserTemperatureFunctions.FromWiserTemp(Convert.ToInt32(temps[i])) }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<string> ();
			var temps = new List<int> ();

			foreach (var item in daySchedule)
				{
				if (item.TryGetValue (Constants.TEXT_TIME, out var timeValue))
					{
					string time = timeValue.ToString ().Replace (":", "");
					times.Add (time);
					}

				if (item.TryGetValue (Constants.TEXT_TEMP, out var tempValue) || item.TryGetValue (Constants.TEXT_SETPOINT, out tempValue))
					{
					double temp;
					if (tempValue.ToString ().ToLower () == Constants.TEXT_OFF.ToLower ())
						{
						temp = Constants.TEMP_OFF;
						}
					else
						{
						temp = Convert.ToDouble (tempValue);
						}

					temps.Add (WiserTemperatureFunctions.ToWiserTemp (temp));
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TEXT_TIME, times },
					 { Constants.TEXT_DEGREESC, temps }
				};
			}

		protected override Dictionary<string, object> ConvertFromWiserSchedule (ConcurrentDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name },
					 { "Description", $"{ScheduleType} schedule for {Name}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override ConcurrentDictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new ConcurrentDictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserOnOffSchedule : WiserSchedule
		{
		private readonly List<int> _deviceTypeIds = new List<int> ();

		public WiserOnOffSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
										 Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public List<int> DeviceTypeIds => _deviceTypeIds;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			if (includeCurrent)
				{
				deviceIds = deviceIds.Concat (AssignmentIds).ToList ();
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> deviceIds)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Any () && AssignmentIds.Any ())
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingDeviceIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayList = daySchedule as List<object>;

			if (dayList != null)
				{
				foreach (var item in dayList)
					{
					int timeValue = Convert.ToInt32 (item);
					int absTime = Math.Abs (timeValue);

					if (absTime > 2400)
						{
						absTime = 0;
						}

					var time = absTime.ToString ("D4");
					var formattedTime = DateTime.ParseExact (time, "HHmm", null).ToString ("HH:mm");

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, formattedTime },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_STATE,
								  timeValue == absTime ? Constants.TEXT_ON : Constants.TEXT_OFF }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();

			foreach (var entry in daySchedule)
				{
				try
					{
					int time = 0;

					if (entry.TryGetValue ("Time", out var timeValue) && IsValidTime (timeValue.ToString ()))
						{
						time = int.Parse (timeValue.ToString ().Replace (":", ""));
						time = time != 0 ? time : 2400;
						}

					if ((entry.TryGetValue ("State", out var stateValue) || entry.TryGetValue (Constants.TEXT_SETPOINT, out stateValue)) &&
						 stateValue.ToString ().Title () == Constants.TEXT_OFF)
						{
						time = time != 0 ? -Math.Abs (time) : -2400;
						}

					times.Add (time);
					}
				catch (Exception ex)
					{
					_LOGGER.Error ($"Error in ConvertYamlToWiserDay: {ex.Message}");
					times.Add (0);
					}
				}

			return times;
			}

		// Fix for CS0029: Adjusting the return type to match the expected type in the method.

		protected override Dictionary<string, object> ConvertFromWiserSchedule (ConcurrentDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
			  {
					{ "Name", Name },
					{ "Description", $"{ScheduleType} schedule for {Name}" },
					{ "Type", ScheduleType }
			  };

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override ConcurrentDictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new ConcurrentDictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserLevelSchedule : WiserSchedule
		{
		public WiserLevelSchedule (WiserRestController wiserRestController, string scheduleType, Dictionary<string, object> scheduleData,
										 Dictionary<string, string> sunrises, Dictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public string LevelType => _scheduleData.TryGetValue ("Type", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public int LevelTypeId => LevelType == WiserScheduleTypeEnum.Shutters.ToString () ? 2 : 1;

		public new WiserScheduleNext Next
			{
			get
				{
				if (_scheduleData.TryGetValue ("Next", out var next))
					{
					if (next is Dictionary<string, object> nextDict)
						{
						return new WiserScheduleNext (_type, nextDict);
						}
					return new WiserScheduleNext (_type, new Dictionary<string, object> { { "Day", "" }, { "Time", 0 }, { "Level", 0 } });
					}
				return null;
				}
			}

		public new ConcurrentDictionary<string, object> ScheduleData
			{
			get
				{
				var scheduleData = RemoveScheduleElements (new ConcurrentDictionary<string, object> (_scheduleData));
				if (scheduleData.Count > 0)
					{
					return scheduleData;
					}
				// Fix: Flattening the nested dictionary to match the expected return type.
				return ConvertToWiserSchedule (Constants.DEFAULT_LEVEL_SCHEDULE.ToDictionary (kvp => kvp.Key, kvp => (object)kvp.Value));
				}
			}

		public new string ScheduleType => LevelType;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			if (includeCurrent)
				{
				deviceIds = deviceIds.Concat (AssignmentIds).ToList ();
				}

			var typeData = new Dictionary<string, object>
				{
					 { "id", Id },
					 { "Name", Name },
					 { "Type", LevelTypeId }
				};

			foreach (var kvp in ScheduleData)
				{
				typeData[kvp.Key] = kvp.Value;
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { _type, typeData }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> UnassignScheduleAsync (List<int> deviceIds)
			{
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Any () && AssignmentIds.Any ())
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return await AssignScheduleAsync (remainingDeviceIds, false).ConfigureAwait (false);
			}

		protected override List<Dictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<Dictionary<string, object>> ();
			var dayDict = daySchedule as Dictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TEXT_LEVEL, out var levelObj) && levelObj is List<object> levels)
				{
				for (int i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i]);
					string timeStr;

					if (Constants.SPECIAL_TIMES.ContainsValue (timeValue))
						{
						if (replaceSpecialTimes)
							{
							timeStr = timeValue == Constants.SPECIAL_TIMES["Sunrise"]
								 ? _sunrises[day]
								 : _sunsets[day];
							}
						else
							{
							timeStr = Constants.SPECIAL_TIMES.FirstOrDefault (x => x.Value == timeValue).Key;
							}
						}
					else
						{
						timeStr = DateTime.ParseExact (timeValue.ToString ("D4"), "HHmm", null).ToString ("HH:mm");
						}

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TEXT_TIME, timeStr },
								{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_LEVEL, levels[i] }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<Dictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();
			var levels = new List<int> ();

			foreach (var entry in daySchedule)
				{
				foreach (var kvp in entry)
					{
					if (kvp.Key.Title () == Constants.TEXT_TIME)
						{
						int time;
						if (Constants.SPECIAL_TIMES.ContainsKey (kvp.Value.ToString ().Title ()))
							{
							time = Constants.SPECIAL_TIMES[kvp.Value.ToString ().Title ()];
							}
						else
							{
							if (IsValidTime (kvp.Value.ToString ()))
								{
								time = int.Parse (kvp.Value.ToString ().Replace (":", ""));
								}
							else
								{
								time = 0;
								}
							}
						times.Add (time);
						}
					if (kvp.Key.Title () == Constants.TEXT_LEVEL || kvp.Key.Title () == Constants.TEXT_SETPOINT)
						{
						levels.Add (Convert.ToInt32 (kvp.Value));
						}
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TEXT_TIME, times },
					 { Constants.TEXT_LEVEL, levels }
				};
			}

		protected override Dictionary<string, object> ConvertFromWiserSchedule (ConcurrentDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name },
					 { "Description", $"{ScheduleType} schedule for {Name}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override ConcurrentDictionary<string, object> ConvertToWiserSchedule (Dictionary<string, object> scheduleData)
			{
			var scheduleOutput = new ConcurrentDictionary<string, object> ();

			try
				{
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.WEEKDAYS.Contains (day.Title ()) || Constants.WEEKENDS.Contains (day.Title ()) || Constants.SPECIAL_DAYS.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<Dictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SPECIAL_DAYS.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TEXT_WEEKDAYS)
								{
								foreach (var weekday in Constants.WEEKDAYS)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}
							if (day.Title () == Constants.TEXT_WEEKENDS)
								{
								foreach (var weekendDay in Constants.WEEKENDS)
									{
									scheduleOutput[weekendDay] = scheduleDay;
									}
								}
							}
						else
							{
							scheduleOutput[day] = scheduleDay;
							}
						}
					}
				return scheduleOutput;
				}
			catch (Exception ex)
				{
				_LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

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
