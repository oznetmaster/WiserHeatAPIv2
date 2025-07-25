// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

namespace WiserHeatApiV2
	{

	public abstract class WiserSchedule
		{
		private static ILog lOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedule));

		private readonly WiserRestController _wiserRestController;
		private readonly string _type;
		private readonly ConcurrentDictionary<string, object> _scheduleData1;
		private readonly IDictionary<string, string> _sunrises;
		private readonly IDictionary<string, string> _sunsets;
		private readonly List<IDictionary<string, object>> _assignments1 = new List<IDictionary<string, object>> ();
		private readonly List<int> _deviceIds1 = new List<int> ();

		protected WiserSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
									  IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			{
			_wiserRestController = wiserRestController;
			_type = scheduleType;
			_scheduleData1 = new ConcurrentDictionary<string, object> (scheduleData);
			_sunrises = sunrises;
			_sunsets = sunsets;
			}

		protected bool ValidateScheduleType (IDictionary<string, object>? scheduleData)
			{
			if (scheduleData == null)
				{
				return false;
				}

			return (scheduleData.TryGetValue ("Type", out var type) && type?.ToString () == ScheduleType) ||
					 (scheduleData.TryGetValue ("SubType", out var subType) && subType?.ToString () == ScheduleType);
			}

		protected static bool IsValidTime (string timeValue)
			{
			return DateTime.TryParseExact (timeValue, "HH:mm", null, System.Globalization.DateTimeStyles.None, out _);
			}

		protected IDictionary<string, object> EnsureType (IDictionary<string, object> scheduleData)
			{
			if (!scheduleData.ContainsKey ("Type"))
				{
				scheduleData["Type"] = ScheduleType;
				}
			return scheduleData;
			}

		private static string[] removeList = new[] { "id", "CurrentSetpoint", "CurrentState", "Description", "CurrentLevel", "Name", "Next", "Type" };
		
		protected static ConcurrentDictionary<string, object> ConcurrentRemoveScheduleElements (IDictionary<string, object> scheduleData)
			{
			var result = new ConcurrentDictionary<string, object> (scheduleData);
			foreach (var item in removeList)
				{
				//if (result.ContainsKey (item))
					{
					result.TryRemove (item, out _);
					}
				}
			return result;
			}

		protected static IDictionary<string, object> RemoveScheduleElements (IDictionary<string, object>? scheduleData)
			{
			var result = new Dictionary<string, object> (scheduleData);
			foreach (var item in removeList)
				{
				if (result.ContainsKey (item))
					{
					result.Remove (item);
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
				bool result = await WiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, Type, cancellationToken).ConfigureAwait (false);
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

		public List<int> AssignmentIds => Assignments1.Select (a => Convert.ToInt32 (a["id"], CultureInfo.InvariantCulture)).ToList ();

		public List<string> AssignmentNames => Assignments1.Select (a => a["name"].ToString ()).ToList ();

		public object? CurrentSetting
			{
			get
				{
				if (Type == WiserScheduleType.Heating.ToString ())
					{
					return WiserTemperatureFunctions.FromWiserTemp (
						 ScheduleData1.TryGetValue ("CurrentSetpoint", out var setpoint) ? setpoint : Constants.TempMinimum);
					}
				if (Type == WiserScheduleType.OnOff.ToString ())
					{
					return ScheduleData1.TryGetValue ("CurrentState", out var state) ? state : Constants.TextUnknown;
					}
				if (Type == WiserScheduleType.Level.ToString ())
					{
					return ScheduleData1.TryGetValue ("CurrentLevel", out var level) ? level : Constants.TextUnknown;
					}
				return null;
				}
			}

		public int Id => ScheduleData1.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string? Name => ScheduleData1.TryGetValue ("Name", out var name) ? name.ToString () : null;

		public WiserScheduleNext? Next
			{
			get
				{
				if (ScheduleData1.TryGetValue ("Next", out var next) && next is Dictionary<string, object> nextDict)
					{
					return new WiserScheduleNext (Type, nextDict);
					}
				return null;
				}
			}

		public IDictionary<string, object> ScheduleData => RemoveScheduleElements (ScheduleData1);

		public IDictionary<string, object> WsScheduleData
			{
			get
				{
				var s = RemoveScheduleElements (ConvertFromWiserSchedule (ScheduleData, genericSetpoint: true));
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

		protected static ILog LOGGER { get => lOGGER; set => lOGGER = value; }

		protected WiserRestController WiserRestController => _wiserRestController;

		protected string Type => _type;

		protected ConcurrentDictionary<string, object> ScheduleData1 => _scheduleData1;

		protected IDictionary<string, string> Sunrises => _sunrises;

		protected IDictionary<string, string> Sunsets => _sunsets;

		protected List<IDictionary<string, object>> Assignments1 => _assignments1;

		protected List<int> DeviceIds1 => _deviceIds1;

		public async Task<bool> CopyScheduleAsync (int toId, CancellationToken cancellationToken = default)
			{
			try
				{
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (ScheduleData1), toId, cancellationToken).ConfigureAwait (false);
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
					await SendScheduleCommandAsync ("DELETE", new Dictionary<string, object> (), cancellationToken: cancellationToken).ConfigureAwait (false);
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
				await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData), cancellationToken: cancellationToken).ConfigureAwait (false);
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
				var scheduleData = JsonConvert.DeserializeObject<IDictionary<string, object>> (File.ReadAllText (scheduleFile));
				if (ValidateScheduleType (scheduleData))
					{
					await SetScheduleAsync (RemoveScheduleElements (scheduleData!), cancellationToken).ConfigureAwait (false);
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
						Console.WriteLine ("The schedule _data is null or invalid.");
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
				var scheduleData = deserializer.Deserialize<IDictionary<string, object>> (File.ReadAllText (scheduleYamlFile));

				if (ValidateScheduleType (scheduleData))
					{
					var schedule = ConvertToWiserSchedule (scheduleData);
					await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
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

					var schedule = ConvertToWiserSchedule (scheduleJson);
					await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
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
				Console.WriteLine ($"Error setting schedule from websocket _data: {ex.Message}");
				return false;
				}
			}
		}

	public class WiserHeatingSchedule : WiserSchedule
		{
		public WiserHeatingSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
											IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public async Task<bool> AssignScheduleAsync (List<int> roomIds, bool includeCurrent = true, CancellationToken cancellationToken = default)
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
								{ "Name", Name ?? "No Name" }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
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
			if (roomIds == null)
				{
				roomIds = new List<int> { };
				}

			var remainingRoomIds = new List<int> ();
			if (roomIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingRoomIds = AssignmentIds.Where (id => !roomIds.Contains (id)).ToList ();
				}

			return AssignScheduleAsync (remainingRoomIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();
			var dayDict = daySchedule as Dictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TextTime, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TextDegreesC, out var tempObj) && tempObj is List<object> temps)
				{
				for (int i = 0; i < times.Count; i++)
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

			return scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ()).ToList ();
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

			foreach (var item in daySchedule)
				{
				if (item.TryGetValue (Constants.TextTime, out var timeValue))
					{
					string time = timeValue.ToString ().Replace (":", "");
					times.Add (time);
					}

				if (item.TryGetValue (Constants.TextTemp, out var tempValue) || item.TryGetValue (Constants.TextSetpoint, out tempValue))
					{
					double temp;
					if (tempValue.ToString ().Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase))
						{
						temp = Constants.TempOff;
						}
					else
						{
						temp = Convert.ToDouble (tempValue, CultureInfo.InvariantCulture);
						}

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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
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

	public class WiserOnOffSchedule : WiserSchedule
		{
		private readonly List<int> _deviceTypeIds = new List<int> ();

		public WiserOnOffSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public List<int> DeviceTypeIds => _deviceTypeIds;

		public async Task<bool> AssignScheduleAsync (List<int> deviceIds, bool includeCurrent = true, CancellationToken cancellationToken = default)
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
								{ "Name", Name ?? "No Name" }
						  }
					 }
				};

			try
				{
				await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
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
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return AssignScheduleAsync (remainingDeviceIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();
			var dayList = daySchedule as List<object>;

			if (dayList != null)
				{
				foreach (var item in dayList)
					{
					int timeValue = Convert.ToInt32 (item, CultureInfo.InvariantCulture);
					int absTime = Math.Abs (timeValue);

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

			return scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>>? daySchedule)
			{
			var times = new List<int> ();

			if (daySchedule == null || daySchedule.Count == 0)
				{
				return times;
				}

			foreach (var entry in daySchedule)
				{
				try
					{
					int time = 0;

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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
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

	public class WiserLevelSchedule : WiserSchedule
		{
		public WiserLevelSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			 : base (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
			{
			}

		public string LevelType => ScheduleData1.TryGetValue ("Type", out var type) ? type.ToString () : Constants.TextUnknown;

		public int LevelTypeId => LevelType == WiserScheduleType.Shutters.ToString () ? 2 : 1;

		public new WiserScheduleNext? Next
			{
			get
				{
				if (ScheduleData1.TryGetValue ("Next", out var next))
					{
					if (next is Dictionary<string, object> nextDict)
						{
						return new WiserScheduleNext (Type, nextDict);
						}
					return new WiserScheduleNext (Type, new Dictionary<string, object> { { "Day", "" }, { "Time", 0 }, { "Level", 0 } });
					}
				return null;
				}
			}

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
					 { "Name", Name ?? "No Name" },
					 { "Type", LevelTypeId }
				};

			if (ScheduleData != null)
				{
				foreach (var kvp in ScheduleData)
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
				await SendScheduleCommandAsync ("ASSIGN", scheduleData, cancellationToken: cancellationToken).ConfigureAwait (false);
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
			if (deviceIds == null)
				{
				deviceIds = new List<int> { };
				}

			var remainingDeviceIds = new List<int> ();
			if (deviceIds.Count != 0 && AssignmentIds.Count != 0)
				{
				remainingDeviceIds = AssignmentIds.Where (id => !deviceIds.Contains (id)).ToList ();
				}

			return AssignScheduleAsync (remainingDeviceIds, false, cancellationToken);
			}

		protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
			{
			var scheduleSetPoints = new List<IDictionary<string, object>> ();
			var dayDict = daySchedule as IDictionary<string, object>;

			if (dayDict != null &&
				 dayDict.TryGetValue (Constants.TextTime, out var timeObj) && timeObj is List<object> times &&
				 dayDict.TryGetValue (Constants.TextLevel, out var levelObj) && levelObj is List<object> levels)
				{
				for (int i = 0; i < times.Count; i++)
					{
					var timeValue = Convert.ToInt32 (times[i], CultureInfo.InvariantCulture);
					string timeStr;

					if (Constants.SpecialTimes.ContainsValue (timeValue))
						{
						if (replaceSpecialTimes)
							{
							timeStr = timeValue == Constants.SpecialTimes["Sunrise"]
								 ? Sunrises[day]
								 : Sunsets[day];
							}
						else
							{
							timeStr = Constants.SpecialTimes.FirstOrDefault (x => x.Value == timeValue).Key;
							}
						}
					else
						{
						timeStr = DateTime.ParseExact (timeValue.ToString ("D4", CultureInfo.InvariantCulture), "HHmm", null).ToString ("HH:mm", CultureInfo.InvariantCulture);
						}

					scheduleSetPoints.Add (new Dictionary<string, object>
						  {
								{ Constants.TextTime, timeStr },
								{ genericSetpoint ? Constants.TextSetpoint : Constants.TextLevel, levels[i] }
						  });
					}
				}

			return scheduleSetPoints.OrderBy (t => t[Constants.TextTime].ToString ()).ToList ();
			}

		protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>> daySchedule)
			{
			var times = new List<int> ();
			var levels = new List<int> ();

			foreach (var entry in daySchedule)
				{
				foreach (var kvp in entry)
					{
					if (kvp.Key.Title () == Constants.TextTime)
						{
						int time;
						if (Constants.SpecialTimes.ContainsKey (kvp.Value.ToString ().Title ()))
							{
							time = Constants.SpecialTimes[kvp.Value.ToString ().Title ()];
							}
						else
							{
							if (IsValidTime (kvp.Value.ToString ()))
								{
								time = int.Parse (kvp.Value.ToString ().Replace (":", ""), CultureInfo.InvariantCulture);
								}
							else
								{
								time = 0;
								}
							}
						times.Add (time);
						}
					if (kvp.Key.Title () == Constants.TextLevel || kvp.Key.Title () == Constants.TextSetpoint)
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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
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
				foreach (var kvp in scheduleData)
					{
					string day = kvp.Key;
					if (Constants.Weekdays.Contains (day.Title ()) || Constants.Weekends.Contains (day.Title ()) || Constants.SpecialDays.Contains (day.Title ()))
						{
						var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<IDictionary<string, object>> ?? new List<IDictionary<string, object>> ());

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
		private static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedules));
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, string> _sunrises;
		private readonly IDictionary<string, string> _sunsets;
		private readonly List<WiserHeatingSchedule> _heatingSchedules = new List<WiserHeatingSchedule> ();
		private readonly List<WiserOnOffSchedule> _onoffSchedules = new List<WiserOnOffSchedule> ();
#if LIGHT
		private readonly List<WiserLevelSchedule> _levelSchedules = new List<WiserLevelSchedule> ();
#endif

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
				if (scheduleData[scheduleType] is List<Dictionary<string, object>> schedules)
					{
					foreach (var schedule in schedules)
						{
						if (schedule is IDictionary<string, object> scheduleDict)
							{
							if (scheduleType == WiserScheduleType.Heating.ToString ())
								{
								_heatingSchedules.Add (new WiserHeatingSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
							if (scheduleType == WiserScheduleType.OnOff.ToString ())
								{
								_onoffSchedules.Add (new WiserOnOffSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
#if LIGHT
							if (scheduleType == WiserScheduleType.Level.ToString ())
								{
								_levelSchedules.Add (new WiserLevelSchedule (_wiserRestController, scheduleType, scheduleDict, _sunrises, _sunsets));
								}
#endif
							}
						}
					}
				}
			}

		public void Update (IDictionary<string, object> scheduleData, IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
			{
			if (scheduleData != null)
				{
				_heatingSchedules.Clear ();
				_onoffSchedules.Clear ();
#if LIGHT
				_levelSchedules.Clear ();
#endif
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

		private async Task<bool> SendScheduleCommandAsync (string action, IDictionary<string, object> scheduleData, int id = 0, CancellationToken cancellationToken = default)
			{
			try
				{
				bool result = await _wiserRestController.SendScheduleCommandAsync (action, scheduleData, id, cancellationToken: cancellationToken).ConfigureAwait (false);
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
#if LIGHT
			 .Concat (_levelSchedules.Cast<WiserSchedule> ())
#endif
			 .ToList ();

		public int Count => All.Count;

		public List<WiserHeatingSchedule> HeatingSchedules => _heatingSchedules;

#if LIGHT
		public List<WiserLevelSchedule> LevelSchedules => _levelSchedules;
#endif

		public List<WiserOnOffSchedule> OnoffSchedules => _onoffSchedules;

		public WiserSchedule? GetById (WiserScheduleType scheduleType, int id)
			{
#if LIGHT || SHUTTER
			// Adjust schedule type for lighting and shutters
			if (scheduleType == WiserScheduleType.Lighting || scheduleType == WiserScheduleType.Shutters)
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
				return _heatingSchedules.FirstOrDefault (s => s.AssignmentIds.Contains (roomId));
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
				return _onoffSchedules.Concat<WiserSchedule> (_levelSchedules)
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

		public List<WiserSchedule> GetByType (WiserScheduleType scheduleType)
			{
			if (scheduleType == WiserScheduleType.Heating)
				{
				return _heatingSchedules.Cast<WiserSchedule> ().ToList ();
				}
			if (scheduleType == WiserScheduleType.OnOff)
				{
				return _onoffSchedules.Cast<WiserSchedule> ().ToList ();
				}
#if LIGHT
			if (scheduleType == WiserScheduleType.Level)
				{
				return _levelSchedules.Cast<WiserSchedule> ().ToList ();
				}
#endif
			return All.Where (s => s.ScheduleType == scheduleType.ToString ()).ToList ();
			}

		public async Task<bool> CopyScheduleAsync (WiserScheduleType scheduleType, int fromId, int toId, CancellationToken cancellationToken = default)
			{
			var fromSchedule = GetById (scheduleType, fromId);
			var toSchedule = GetById (scheduleType, toId);

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
			if (assignments == null)
				{
				assignments = new List<int> ();
				}

			var typeData = new Dictionary<string, object> { { "Name", name } };

#if LIGHT
			if (scheduleType == WiserScheduleType.Lighting || scheduleType == WiserScheduleType.Level)
				{
				typeData["Type"] = 1;
				foreach (var kvp in Constants.DefaultLevelSchedule)
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
				foreach (var kvp in Constants.DefaultLevelSchedule)
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
