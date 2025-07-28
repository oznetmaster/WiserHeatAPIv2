// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

using static WiserHeatApiV2.Constants;

namespace WiserHeatApiV2
	{

	public abstract class WiserSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
									  IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
		{
		protected bool ValidateScheduleType (IDictionary<string, object>? scheduleData) =>
			scheduleData != null && ((scheduleData.TryGetValue ("Type", out var type) && type?.ToString () == ScheduleType) ||
					 (scheduleData.TryGetValue ("SubType", out var subType) && subType?.ToString () == ScheduleType));

		protected static bool IsValidTime (string timeValue) =>
			DateTime.TryParseExact (timeValue, "HH:mm", null, System.Globalization.DateTimeStyles.None, out _);

		protected IDictionary<string, object> EnsureType (IDictionary<string, object> scheduleData)
			{
			if (!scheduleData.ContainsKey ("Type"))
				{
				scheduleData["Type"] = ScheduleType;
				}

			return scheduleData;
			}

		private static readonly string[] _removeList = ["id", "CurrentSetpoint", "CurrentState", "Description", "CurrentLevel", "Name", "Next", "Type"];

		protected static ConcurrentDictionary<string, object> ConcurrentRemoveScheduleElements (IDictionary<string, object> scheduleData)
			{
			var result = new ConcurrentDictionary<string, object> (scheduleData);
			foreach (var item in _removeList)
				{
				//if (result.ContainsKey (item))
					{
					_ = result.TryRemove (item, out _);
					}
				}

			return result;
			}

		protected static IDictionary<string, object> RemoveScheduleElements (IDictionary<string, object>? scheduleData)
			{
			var result = new Dictionary<string, object> (scheduleData);
			foreach (var item in _removeList)
				{
				if (result.ContainsKey (item))
					{
					_ = result.Remove (item);
					}
				}

			return result;
			}

		protected abstract IDictionary<string, object>? ConvertFromWiserSchedule (IDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData);
		protected abstract List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false);
		protected abstract object ConvertYamlToWiserDay (List<IDictionary<string, object>> daySchedule);

		protected async Task<bool> SendScheduleCommandAsync (string action, IDictionary<string, object> scheduleData, int id = 0, CancellationToken cancellationToken = default)
			{
			try
				{
				var result = await WiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, Type, cancellationToken).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		public List<int> DeviceIds => DeviceIds1;

		public List<IDictionary<string, object>> Assignments => Assignments1;

		public List<int> AssignmentIds => [.. Assignments1.Select (a => Convert.ToInt32 (a["id"], CultureInfo.InvariantCulture))];

		public List<string> AssignmentNames => [.. Assignments1.Select (a => a["name"].ToString ())];

		public object? CurrentSetting =>
			Type switch
				{
					nameof (WiserScheduleType.Heating) =>
						 WiserTemperatureFunctions.FromWiserTemp (
							  ScheduleData1.TryGetValue ("CurrentSetpoint", out var setpoint) ? setpoint : Constants.TempMinimum),

					nameof (WiserScheduleType.OnOff) =>
						 ScheduleData1.TryGetValue ("CurrentState", out var state) ? state : Constants.TextUnknown,

					nameof (WiserScheduleType.Level) =>
						 ScheduleData1.TryGetValue ("CurrentLevel", out var level) ? level : Constants.TextUnknown,

					_ => null
					};

		public int Id => ScheduleData1.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string? Name => ScheduleData1.TryGetValue ("Name", out var name) ? name.ToString () : null;

		public WiserScheduleNext? Next =>
			ScheduleData1.TryGetValue ("Next", out var next) && next is Dictionary<string, object> nextDict
					? new WiserScheduleNext (Type, nextDict)
					: null;

		public IDictionary<string, object> ScheduleData => RemoveScheduleElements (ScheduleData1);

		public IDictionary<string, object> WsScheduleData
			{
			get
				{
				IDictionary<string, object> s = RemoveScheduleElements (ConvertFromWiserSchedule (ScheduleData, genericSetpoint: true));
				return new Dictionary<string, object>
					 {
						  { "Id", Id },
						  { "Name", Name ?? "No Name" },
						  { "Type", Type },
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

		public string ScheduleType => Type;

		protected static ILog LOGGER { get; set; } = log4net.LogManager.GetLogger (typeof (WiserSchedule));

		protected WiserRestController WiserRestController { get; } = wiserRestController;

		protected string Type { get; } = scheduleType;

		protected ConcurrentDictionary<string, object> ScheduleData1 { get; } = new ConcurrentDictionary<string, object> (scheduleData);

		protected IDictionary<string, string> Sunrises { get; } = sunrises;

		protected IDictionary<string, string> Sunsets { get; } = sunsets;

		protected List<IDictionary<string, object>> Assignments1 { get; } = [];

		protected List<int> DeviceIds1 { get; } = [];

		public async Task<bool> CopyScheduleAsync (int toId, CancellationToken cancellationToken = default)
			{
			try
				{
				_ = await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (ScheduleData1), toId, cancellationToken).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error copying schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> DeleteScheduleAsync (CancellationToken cancellationToken = default)
			{
			try
				{
				if (Id != 1000)
					{
					_ = await SendScheduleCommandAsync ("DELETE", new Dictionary<string, object> (), cancellationToken: cancellationToken).ConfigureAwait (false);
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
				LOGGER.Error ($"Error deleting schedule: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToFile (string scheduleFile)
			{
			try
				{
				File.WriteAllText (scheduleFile, JsonConvert.SerializeObject (EnsureType (ScheduleData1), Newtonsoft.Json.Formatting.Indented));
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error saving schedule to file: {ex.Message}");
				return false;
				}
			}

		public bool SaveScheduleToYamlFile (string scheduleYamlFile)
			{
			try
				{
				var serializer = new YamlDotNet.Serialization.Serializer ();
				File.WriteAllText (scheduleYamlFile, serializer.Serialize (ConvertFromWiserSchedule (ScheduleData1)));
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error saving schedule to yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleAsync (IDictionary<string, object>? scheduleData, CancellationToken cancellationToken = default)
			{
			try
				{
				_ = await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData), cancellationToken: cancellationToken).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error setting schedule: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromFileAsync (string scheduleFile, CancellationToken cancellationToken = default)
			{
			try
				{
				IDictionary<string, object>? scheduleData = JsonConvert.DeserializeObject<IDictionary<string, object>> (File.ReadAllText (scheduleFile));
				if (ValidateScheduleType (scheduleData))
					{
					_ = await SetScheduleAsync (RemoveScheduleElements (scheduleData!), cancellationToken).ConfigureAwait (false);
					return true;
					}
				else
					{
					if (scheduleData != null)
						{
						Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TextUnknown)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
						}
					else
						{
						Console.WriteLine ("The schedule data is null or invalid.");
						}

					return false;
					}
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"Error setting schedule from file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromYamlFileAsync (string scheduleYamlFile, CancellationToken cancellationToken = default)
			{
			try
				{
				var deserializer = new YamlDotNet.Serialization.Deserializer ();
				IDictionary<string, object> scheduleData = deserializer.Deserialize<IDictionary<string, object>> (File.ReadAllText (scheduleYamlFile));

				if (ValidateScheduleType (scheduleData))
					{
					IDictionary<string, object>? schedule = ConvertToWiserSchedule (scheduleData);
					_ = await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
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
				LOGGER.Error ($"Error setting schedule from yaml file: {ex.Message}");
				return false;
				}
			}

		public async Task<bool> SetScheduleFromWsDataAsync (IDictionary<string, object> scheduleData, CancellationToken cancellationToken = default)
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
							if (entry is IDictionary<string, object> entryDict)
								{
								scheduleJson[entryDict["day"].ToString ()] = entryDict["slots"];
								}
							}
						}

					IDictionary<string, object>? schedule = ConvertToWiserSchedule (scheduleJson);
					_ = await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
					return true;
					}
				else
					{
					Console.WriteLine ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TextUnknown)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
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

	public class WiserHeatingSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
		{
		public async Task<bool> AssignScheduleAsync (List<int> roomIds, bool includeCurrent = true, CancellationToken cancellationToken = default)
			{
			roomIds ??= [];

			if (includeCurrent)
				{
				roomIds = [.. roomIds, .. AssignmentIds];
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", roomIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name ?? "No Name" }
						  }
					 }
				};

			try
				{
				_ = await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public Task<bool> UnassignScheduleAsync (List<int> roomIds, CancellationToken cancellationToken = default)
			{
			roomIds ??= [];

			var remainingRoomIds = new List<int> ();
			if (roomIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingRoomIds = [.. AssignmentIds.Where (id => !roomIds.Contains (id))];
				}

			return AssignScheduleAsync (remainingRoomIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();

			if (daySchedule is Dictionary<string, object> dayDict &&
				 dayDict.TryGetValue (Constants.TextTime, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TextDegreesC, out var tempObj) && tempObj is List<object> temps)
				{
				for (var i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i], CultureInfo.InvariantCulture).ToString ("D4", CultureInfo.InvariantCulture);
					var time = DateTime.ParseExact (timeValue, "HHmm", null).ToString ("HH:mm", CultureInfo.InvariantCulture);

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TextTime, time },
								{ genericSetpoint ? Constants.TextSetpoint : Constants.TextTemp,
								  WiserTemperatureFunctions.FromWiserTemp(temps[i]) }
						  });
					}
				}

			return [.. scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ())];
			}

		protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>>? daySchedule)
			{
			var times = new List<string> ();
			var temps = new List<int> ();

			if (daySchedule == null || daySchedule.Count == 0)
				{
				return new Dictionary<string, object>
					{
						 { Constants.TextTime, times },
						 { Constants.TextDegreesC, temps }
					};
				}

			foreach (IDictionary<string, object> item in daySchedule)
				{
				if (item.TryGetValue (Constants.TextTime, out var timeValue))
					{
					var time = timeValue.ToString ().Replace (":", "");
					times.Add (time);
					}

				if (item.TryGetValue (Constants.TextTemp, out var tempValue) || item.TryGetValue (Constants.TextSetpoint, out tempValue))
					{
					var temp = tempValue.ToString ().Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase)
						? Constants.TempOff
						: Convert.ToDouble (tempValue, CultureInfo.InvariantCulture);

					temps.Add (WiserTemperatureFunctions.ToWiserTemp (temp));
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TextTime, times },
					 { Constants.TextDegreesC, temps }
				};
			}

		protected override IDictionary<string, object>? ConvertFromWiserSchedule (IDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name ?? "No Name" },
					 { "Description", $"{ScheduleType} schedule for {Name}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}

				return scheduleOutput;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData)
			{
			var scheduleOutput = new ConcurrentDictionary<string, object> ();

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<IDictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SpecialDays.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TextWeekdays)
								{
								foreach (var weekday in Constants.Weekdays)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}

							if (day.Title () == Constants.TextWeekends)
								{
								foreach (var weekendDay in Constants.Weekends)
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
				LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserOnOffSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
		{
		public List<int> DeviceTypeIds { get; } = [];

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true, CancellationToken cancellationToken = default)
			{
			deviceIds ??= [];

			if (includeCurrent)
				{
				deviceIds = [.. deviceIds, .. AssignmentIds];
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { ScheduleType, new Dictionary<string, object>
						  {
								{ "id", Id },
								{ "Name", Name ?? "No Name" }
						  }
					 }
				};

			try
				{
				_ = await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public Task<bool> UnassignScheduleAsync (List<int> deviceIds, CancellationToken cancellationToken = default)
			{
			deviceIds ??= [];

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingDeviceIds = [.. AssignmentIds.Where (id => !deviceIds.Contains (id))];
				}

			return AssignScheduleAsync (remainingDeviceIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();

			if (daySchedule is List<object> dayList)
				{
				foreach (var item in dayList)
					{
					var timeValue = Convert.ToInt32 (item, CultureInfo.InvariantCulture);
					var absTime = Math.Abs (timeValue);

					if (absTime > 2400)
						{
						absTime = 0;
						}

					var time = absTime.ToString ("D4", CultureInfo.InvariantCulture);
					var formattedTime = DateTime.ParseExact (time, "HHmm", null).ToString ("HH:mm", CultureInfo.InvariantCulture);

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TextTime, formattedTime },
								{ genericSetpoint ? Constants.TextSetpoint : Constants.TextState,
								  timeValue == absTime ? Constants.TextOn : Constants.TextOff }
						  });
					}
				}

			return [.. scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ())];
			}

		protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>>? daySchedule)
			{
			var times = new List<int> ();

			if (daySchedule == null || daySchedule.Count == 0)
				{
				return times;
				}

			foreach (IDictionary<string, object> entry in daySchedule)
				{
				try
					{
					var time = 0;

					if (entry.TryGetValue ("Time", out var timeValue) && IsValidTime (timeValue.ToString ()))
						{
						time = int.Parse (timeValue.ToString ().Replace (":", ""), CultureInfo.InvariantCulture);
						time = time != 0 ? time : 2400;
						}

					if ((entry.TryGetValue ("State", out var stateValue) || entry.TryGetValue (Constants.TextSetpoint, out stateValue)) &&
						 stateValue.ToString ().Title () == Constants.TextOff)
						{
						time = time != 0 ? -Math.Abs (time) : -2400;
						}

					times.Add (time);
					}
				catch (Exception ex)
					{
					LOGGER.Error ($"Error in ConvertYamlToWiserDay: {ex.Message}");
					times.Add (0);
					}
				}

			return times;
			}

		// Fix for CS0029: Adjusting the return type to match the expected type in the method.

		protected override IDictionary<string, object>? ConvertFromWiserSchedule (IDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
			  {
					{ "Name", Name ?? "No Name" },
					{ "Description", $"{ScheduleType} schedule for {Name ?? "No Name"}" },
					{ "Type", ScheduleType }
			  };

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}

				return scheduleOutput;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData)
			{
			var scheduleOutput = new ConcurrentDictionary<string, object> ();

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<IDictionary<string, object>>);

						// If using special days, convert to one entry for each weekday
						if (Constants.SpecialDays.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TextWeekdays)
								{
								foreach (var weekday in Constants.Weekdays)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}

							if (day.Title () == Constants.TextWeekends)
								{
								foreach (var weekendDay in Constants.Weekends)
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
				LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserLevelSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
		{
		public string LevelType => ScheduleData1.TryGetValue ("Type", out var type) ? type.ToString () : Constants.TextUnknown;

		public int LevelTypeId => LevelType == WiserScheduleType.Shutters.ToString () ? 2 : 1;

		public new WiserScheduleNext? Next => ScheduleData1.TryGetValue ("Next", out var next)
					? next is Dictionary<string, object> nextDict
						? new WiserScheduleNext (Type, nextDict)
						: new WiserScheduleNext (Type, new Dictionary<string, object> { { "Day", "" }, { "Time", 0 }, { "Level", 0 } })
					: null;

		public new IDictionary<string, object>? ScheduleData
			{
			get
				{
				IDictionary<string, object> scheduleData = RemoveScheduleElements (ScheduleData1);
				if (scheduleData.Count > 0)
					{
					return scheduleData;
					}
				// Fix: Flattening the nested dictionary to match the expected return type.
				return ConvertToWiserSchedule (Constants.DefaultLevelSchedule.ToDictionary (kvp => kvp.Key, kvp => (object)kvp.Value));
				}
			}

		public new string ScheduleType => LevelType;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true, CancellationToken cancellationToken = default)
			{
			deviceIds ??= [];

			if (includeCurrent)
				{
				deviceIds = [.. deviceIds, .. AssignmentIds];
				}

			var typeData = new Dictionary<string, object>
				{
					 { "id", Id },
					 { "Name", Name ?? "No Name" },
					 { "Type", LevelTypeId }
				};

			if (ScheduleData != null)
				{
				foreach (KeyValuePair<string, object> kvp in ScheduleData)
					{
					typeData[kvp.Key] = kvp.Value;
					}
				}

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", deviceIds.Distinct().ToList() },
					 { Type, typeData }
				};

			try
				{
				_ = await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
				return true;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error assigning schedule: {ex.Message}");
				return false;
				}
			}

		public Task<bool> UnassignScheduleAsync (List<int> deviceIds, CancellationToken cancellationToken = default)
			{
			deviceIds ??= [];

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingDeviceIds = [.. AssignmentIds.Where (id => !deviceIds.Contains (id))];
				}

			return AssignScheduleAsync (remainingDeviceIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();

			if (daySchedule is IDictionary<string, object> dayDict &&
				 dayDict.TryGetValue (Constants.TextTime, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TextLevel, out var levelObj) && levelObj is List<object> levels)
				{
				for (var i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i], CultureInfo.InvariantCulture);
					string timeStr;

					timeStr = Constants.SpecialTimes.ContainsValue (timeValue)
						? replaceSpecialTimes
							? timeValue == Constants.SpecialTimes["Sunrise"]
								 ? Sunrises[day]
								 : Sunsets[day]
							: Constants.SpecialTimes.FirstOrDefault (x => x.Value == timeValue).Key
						: DateTime.ParseExact (timeValue.ToString ("D4", CultureInfo.InvariantCulture), "HHmm", null).ToString ("HH:mm", CultureInfo.InvariantCulture);

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TextTime, timeStr },
								{ genericSetpoint ? Constants.TextSetpoint : Constants.TextLevel, levels[i] }
						  });
					}
				}

			return [.. scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ())];
			}

		protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();
			var levels = new List<int> ();

			foreach (IDictionary<string, object> entry in daySchedule)
				{
				foreach (KeyValuePair<string, object> kvp in entry)
					{
					if (kvp.Key.Title () == Constants.TextTime)
						{
						var time = Constants.SpecialTimes.ContainsKey (kvp.Value.ToString ().Title ())
							? Constants.SpecialTimes[kvp.Value.ToString ().Title ()]
							: IsValidTime (kvp.Value.ToString ()) ? int.Parse (kvp.Value.ToString ().Replace (":", ""), CultureInfo.InvariantCulture) : 0;
						times.Add (time);
						}

					if (kvp.Key.Title () is Constants.TextLevel or Constants.TextSetpoint)
						{
						levels.Add (Convert.ToInt32 (kvp.Value, CultureInfo.InvariantCulture));
						}
					}
				}

			return new Dictionary<string, object>
				{
					 { Constants.TextTime, times },
					 { Constants.TextLevel, levels }
				};
			}

		protected override IDictionary<string, object>? ConvertFromWiserSchedule (IDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleOutput = new Dictionary<string, object>
				{
					 { "Name", Name ?? "No Name" },
					 { "Description", $"{ScheduleType} schedule for {Name ?? "No Name"}" },
					 { "Type", ScheduleType }
				};

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Weekdays.Contains (day.Title ()) || Weekends.Contains (day.Title ()) || SpecialDays.Contains (day.Title ()))
						{
						List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
						scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
						}
					}

				return scheduleOutput;
				}
			catch (Exception ex)
				{
				LOGGER.Error ($"Error converting from Wiser schedule: {ex.Message}");
				return null;
				}
			}

		protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object>? scheduleData)
			{
			var scheduleOutput = new Dictionary<string, object> ();
			if (scheduleData == null)
				{
				return scheduleOutput;
				}

			try
				{
				foreach (KeyValuePair<string, object> kvp in scheduleData)
					{
					var day = kvp.Key;
					if (Weekdays.Contains (day.Title ()) || Weekends.Contains (day.Title ()) || SpecialDays.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<IDictionary<string, object>> ?? []);

						// If using special days, convert to one entry for each weekday
						if (Constants.SpecialDays.Contains (day.Title ()))
							{
							if (day.Title () == Constants.TextWeekdays)
								{
								foreach (var weekday in Constants.Weekdays)
									{
									scheduleOutput[weekday] = scheduleDay;
									}
								}

							if (day.Title () == Constants.TextWeekends)
								{
								foreach (var weekendDay in Constants.Weekends)
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
				LOGGER.Error ($"Error converting to Wiser schedule: {ex.Message}");
				return null;
				}
			}
		}

	public class WiserSchedules
		{
		private static readonly ILog _lOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedules));
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, string> _sunrises;
		private readonly IDictionary<string, string> _sunsets;

		public WiserSchedules (WiserRestController wiserRestController, IDictionary<string, object> scheduleData,
												  IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			{
			_wiserRestController = wiserRestController;
			_sunrises = sunrises;
			_sunsets = sunsets;
			Build (scheduleData);
			}

		private void Build (IDictionary<string, object> scheduleData)
			{
			foreach (var scheduleType in scheduleData.Keys)
				{
				if (scheduleData[scheduleType] is not List<Dictionary<string, object>> schedules)
					continue;

				foreach (Dictionary<string, object> schedule in schedules)
					{
					switch (scheduleType)
						{
						case nameof (WiserScheduleType.Heating):
							HeatingSchedules.Add (new WiserHeatingSchedule (_wiserRestController, scheduleType, schedule, _sunrises, _sunsets));
							break;

						case nameof (WiserScheduleType.OnOff):
							OnoffSchedules.Add (new WiserOnOffSchedule (_wiserRestController, scheduleType, schedule, _sunrises, _sunsets));
							break;

#if LIGHT
						case nameof (WiserScheduleType.Level):
							LevelSchedules.Add (new WiserLevelSchedule (_wiserRestController, scheduleType, schedule, _sunrises, _sunsets));
							break;
#endif
						}
					}
				}
			}

		public void Update (IDictionary<string, object> scheduleData, IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			{
			if (scheduleData != null)
				{
				HeatingSchedules.Clear ();
				OnoffSchedules.Clear ();
#if LIGHT
				LevelSchedules.Clear ();
#endif
				Build (scheduleData);
				}

			if (sunrises != null)
				{
				_sunrises.Clear ();
				foreach (KeyValuePair<string, string> kv in sunrises)
					_sunrises[kv.Key] = kv.Value;
				}

			if (sunsets != null)
				{
				_sunsets.Clear ();
				foreach (KeyValuePair<string, string> kv in sunsets)
					_sunsets[kv.Key] = kv.Value;
				}
			}

		private async Task<bool> SendScheduleCommandAsync (string action, IDictionary<string, object> scheduleData, int id = 0, CancellationToken cancellationToken = default)
			{
			try
				{
				var result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id, cancellationToken: cancellationToken).ConfigureAwait (false);
				return result;
				}
			catch (Exception ex)
				{
				_lOGGER.Error ($"Error in SendScheduleCommand: {ex.Message}");
				throw;
				}
			}

		public List<WiserSchedule> All => [.. HeatingSchedules.Cast<WiserSchedule> (),
			 .. OnoffSchedules.Cast<WiserSchedule> (), .. LevelSchedules.Cast<WiserSchedule> ()];

		public int Count => All.Count;

		public List<WiserHeatingSchedule> HeatingSchedules { get; } = [];

#if LIGHT
		public List<WiserLevelSchedule> LevelSchedules { get; } = [];
#endif

		public List<WiserOnOffSchedule> OnoffSchedules { get; } = [];

		public WiserSchedule? GetById (WiserScheduleType scheduleType, int id)
			{
#if LIGHT || SHUTTER
			// Adjust schedule type for lighting and shutters
			if (scheduleType is WiserScheduleType.Lighting or WiserScheduleType.Shutters)
				{
				scheduleType = WiserScheduleType.Level;
				}
#endif

			try
				{
#if LIGHT
				if (scheduleType == WiserScheduleType.Level)
					{
					return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Id == id);
					}
#endif
				return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Id == id);
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserHeatingSchedule? GetByRoomId (int roomId)
			{
			try
				{
				return HeatingSchedules.FirstOrDefault (s => s.AssignmentIds.Contains (roomId));
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserSchedule? GetByDeviceId (int deviceId)
			{
			try
				{
#if LIGHT
				return OnoffSchedules.Concat<WiserSchedule> (LevelSchedules)
#else
				return _onoffSchedules
#endif
					 .FirstOrDefault (s => s.DeviceIds.Contains (deviceId));
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public WiserSchedule? GetByName (WiserScheduleType scheduleType, string name)
			{
			try
				{
#if LIGHT
				if (scheduleType == WiserScheduleType.Level)
					{
					return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Name == name);
					}
#endif
				return All.FirstOrDefault (s => s.ScheduleType == scheduleType.ToString () && s.Name == name);
				}
			catch (IndexOutOfRangeException)
				{
				return null;
				}
			}

		public List<WiserSchedule> GetByType (WiserScheduleType scheduleType) =>
			scheduleType switch
				{
					WiserScheduleType.Heating => [.. HeatingSchedules.Cast<WiserSchedule> ()],
					WiserScheduleType.OnOff => [.. OnoffSchedules.Cast<WiserSchedule> ()],
#if LIGHT
					WiserScheduleType.Level => [.. LevelSchedules.Cast<WiserSchedule> ()],
#endif
					_ => [.. All.Where (s => s.ScheduleType == scheduleType.ToString ())]
					};

		public async Task<bool> CopyScheduleAsync (WiserScheduleType scheduleType, int fromId, int toId, CancellationToken cancellationToken = default)
			{
			WiserSchedule? fromSchedule = GetById (scheduleType, fromId);
			WiserSchedule? toSchedule = GetById (scheduleType, toId);

			if (fromSchedule != null && toSchedule != null)
				{
				if (fromSchedule.ScheduleType == toSchedule.ScheduleType)
					{
					return await fromSchedule.CopyScheduleAsync (toId, cancellationToken).ConfigureAwait (false);
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

		public Task<bool> CreateScheduleAsync (WiserScheduleType scheduleType, string name, List<int>? assignments = null, CancellationToken cancellationToken = default)
			{
			assignments ??= [];

			var typeData = new Dictionary<string, object> { { "Name", name } };

#if LIGHT
			if (scheduleType is WiserScheduleType.Lighting or WiserScheduleType.Level)
				{
				typeData["Type"] = 1;
				foreach (KeyValuePair<string, Dictionary<string, object>> kvp in Constants.DefaultLevelSchedule)
					{
					typeData[kvp.Key] = kvp.Value;
					}

				scheduleType = WiserScheduleType.Level;
				}
#endif

#if SHUTTER
			if (scheduleType == WiserScheduleType.Shutters)
				{
				typeData["Type"] = 2;
				foreach (KeyValuePair<string, Dictionary<string, object>> kvp in Constants.DefaultLevelSchedule)
					{
					typeData[kvp.Key] = kvp.Value;
					}

				scheduleType = WiserScheduleType.Level;
				}
#endif

			var scheduleData = new Dictionary<string, object>
				{
					 { "Assignments", assignments },
					 { scheduleType.ToString(), typeData }
				};

			return SendScheduleCommandAsync ("CREATE", scheduleData, cancellationToken: cancellationToken);
			}
		}
	}
