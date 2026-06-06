// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;
using log4net.Repository.Hierarchy;

using Newtonsoft.Json;

using static WiserHeatApiV2.Constants;

namespace WiserHeatApiV2;

/// <summary>
/// Represents a base class for Wiser schedules, providing common schedule management and conversion functionality.
/// </summary>
/// <param name="wiserRestController">The REST controller used to communicate with the Wiser hub.</param>
/// <param name="scheduleType">The type of schedule (e.g., Heating, OnOff, Level).</param>
/// <param name="scheduleData">The raw schedule data dictionary.</param>
/// <param name="sunrises">Dictionary of sunrise times by day.</param>
/// <param name="sunsets">Dictionary of sunset times by day.</param>
public abstract class WiserSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
	 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets)
	{
	// Update the logger initialization to ensure the `AddConsole` method is recognized.

	/// <summary>
	/// Validates the schedule type against the provided schedule data.
	/// </summary>
	/// <remarks>
	/// Validates whether the provided schedule data matches the type of this schedule instance. The method checks for a 'Type' or 'SubType' key in the dictionary and compares its value to the current schedule's type. This is used to ensure that schedule operations are performed on the correct type of schedule and to prevent accidental assignment or modification of schedules with mismatched types.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to validate. Should contain a 'Type' or 'SubType' key matching this schedule's type.</param>
	/// <returns><see langword="true"/> if the schedule type matches; otherwise, <see langword="false"/>.</returns>
	protected bool ValidateScheduleType (IDictionary<string, object>? scheduleData) =>
			 scheduleData != null && ((scheduleData.TryGetValue ("Type", out var type) && type?.ToString () == ScheduleType) ||
					 (scheduleData.TryGetValue ("SubType", out var subType) && subType?.ToString () == ScheduleType));

	/// <summary>
	/// Determines whether the given time value is valid.
	/// </summary>
	/// <remarks>
	/// Determines whether the given time value is valid. The method attempts to parse the string as a time in HH:mm format. This is used for validating time strings in schedule slots to ensure they conform to the expected format before processing or transmission to the hub.
	/// </remarks>
	/// <param name="timeValue">The time value to validate, in HH:mm format.</param>
	/// <returns><see langword="true"/> if the time value is valid; otherwise, <see langword="false"/>.</returns>
	protected static bool IsValidTime (string timeValue) =>
			 DateTime.TryParseExact (timeValue, "HH:mm", null, System.Globalization.DateTimeStyles.None, out _);

	/// <summary>
	/// Ensures the schedule data includes the correct type information.
	/// </summary>
	/// <remarks>
	/// Ensures the schedule data includes the correct type information. If the 'Type' key is missing from the dictionary, it is added and set to this schedule's type. This is important for serialization and transmission to the hub, as the hub expects the type to be present in the schedule data.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to ensure type for.</param>
	/// <returns>The updated schedule data with type information ensured.</returns>
	protected IDictionary<string, object> EnsureType (IDictionary<string, object> scheduleData)
		{
		if (!scheduleData.ContainsKey ("Type"))
			{
			scheduleData["Type"] = ScheduleType;
			}

		return scheduleData;
		}

	// Case-sensitive (matches default Dictionary/ConcurrentDictionary behavior)
	private static readonly HashSet<string> _removeSet =
		  new (StringComparer.Ordinal)
		  {
		 "id", "CurrentSetpoint", "CurrentState", "Description",
		 "CurrentLevel", "Name", "Next", "Type"
		  };

	/// <summary>
	/// Removes non-essential elements from the schedule data using a concurrent dictionary.
	/// </summary>
	/// <remarks>
	/// Removes non-essential elements from the schedule data using a concurrent dictionary. Keys listed in the internal _removeSet are excluded from the result. This method is thread-safe and optimized for concurrent access, making it suitable for scenarios where schedule data is processed in parallel.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to process. Keys in the internal _removeSet will be excluded.</param>
	/// <returns>A concurrent dictionary containing the cleaned schedule data.</returns>
	protected static ConcurrentDictionary<string, object> ConcurrentRemoveScheduleElements (
		  IDictionary<string, object> scheduleData)
		{
		var cd = new ConcurrentDictionary<string, object> (
			  concurrencyLevel: Environment.ProcessorCount,
			  capacity: scheduleData.Count); // no resizes, ever, at this scale
		foreach (KeyValuePair<string, object> kv in scheduleData)
			{
			if (!_removeSet.Contains (kv.Key))
				cd[kv.Key] = kv.Value;
			}

		return cd;
		}

	/// <summary>
	/// Removes non-essential elements from the schedule data.
	/// </summary>
	/// <remarks>
	/// Removes non-essential elements from the schedule data. Keys listed in the internal _removeSet are excluded from the result. This is used to prepare schedule data for transmission or serialization, ensuring only relevant information is sent to the hub or saved to disk.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to process. Keys in the internal _removeSet will be excluded.</param>
	/// <returns>A dictionary containing the cleaned schedule data.</returns>
	protected static IDictionary<string, object> RemoveScheduleElements (IDictionary<string, object> scheduleData)
		{
		var result = new Dictionary<string, object> (scheduleData); // pre-sized clone
		foreach (var k in _removeSet)
			_ = result.Remove (k);
		return result;
		}

	/// <summary>
	/// Converts the Wiser schedule data to a generic format for processing.
	/// </summary>
	/// <remarks>
	/// Converts the Wiser schedule data to a generic format for processing. This method is abstract and must be implemented by derived schedule types. It is used to convert the schedule data from the hub's native format to a format suitable for YAML or other generic representations, optionally replacing special times and using generic setpoints.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to convert. Should be in Wiser's native format.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times (e.g., sunrise/sunset) with actual times.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint value instead of a specific temperature or state.</param>
	/// <returns>A dictionary containing the converted schedule data, suitable for YAML or other generic formats.</returns>
	protected abstract IDictionary<string, object>? ConvertFromWiserSchedule (IDictionary<string, object> scheduleData, bool replaceSpecialTimes = false, bool genericSetpoint = false);

	/// <summary>
	/// Converts generic schedule data back to the Wiser format.
	/// </summary>
	/// <remarks>
	/// Converts generic schedule data back to the Wiser format. This method is abstract and must be implemented by derived schedule types. It is used to convert schedule data from YAML or other generic formats back to the hub's expected format for transmission or processing.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to convert, typically from YAML or other generic formats.</param>
	/// <returns>A dictionary containing the Wiser-formatted schedule data.</returns>
	protected abstract IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData);

	/// <summary>
	/// Converts a day's schedule from Wiser format to a YAML-compatible format.
	/// </summary>
	/// <remarks>
	/// Converts a day's schedule from Wiser format to a YAML-compatible format. This method is abstract and must be implemented by derived schedule types. It is used to convert a single day's schedule data from the hub's format to a list of dictionaries suitable for YAML serialization, optionally replacing special times and using generic setpoints.
	/// </remarks>
	/// <param name="day">The day of the week (e.g., "Monday").</param>
	/// <param name="daySchedule">The day's schedule data in Wiser format.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual times.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint value.</param>
	/// <returns>A list of dictionaries representing the day's schedule in YAML format.</returns>
	protected abstract List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false);

	/// <summary>
	/// Converts a day's schedule from YAML format back to Wiser format.
	/// </summary>
	/// <remarks>
	/// Converts a day's schedule from YAML format back to Wiser format. This method is abstract and must be implemented by derived schedule types. It is used to convert a list of dictionaries representing a day's schedule in YAML format into the hub's expected format for transmission or processing.
	/// </remarks>
	/// <param name="daySchedule">The day's schedule in YAML format. May be <see langword="null"/>.</param>
	/// <returns>An object representing the day's schedule in Wiser format.</returns>
	protected abstract object ConvertYamlToWiserDay (List<IDictionary<string, object>> daySchedule);

	/// <summary>
	/// Sends a schedule command to the Wiser system asynchronously.
	/// </summary>
	/// <remarks>
	/// Sends a schedule command to the Wiser system asynchronously. This method wraps the REST controller's schedule command API and logs errors. It is used to perform actions such as UPDATE, DELETE, or ASSIGN on schedules, sending the appropriate data to the hub and handling any exceptions that occur.
	/// </remarks>
	/// <param name="action">The action to perform (e.g., "UPDATE", "DELETE", "ASSIGN").</param>
	/// <param name="scheduleData">The schedule data to send to the hub.</param>
	/// <param name="id">The ID of the schedule to target. If 0, uses this schedule's Id.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the command was successful; otherwise, <see langword="false"/>.</returns>
	protected async Task<bool> SendScheduleCommandAsync (string action, IDictionary<string, object> scheduleData, int id = 0, CancellationToken cancellationToken = default)
		{
		try
			{
			var result = await WiserRestController.SendScheduleCommandAsync (action, scheduleData, id != 0 ? id : Id, Type, cancellationToken).ConfigureAwait (false);
			return result;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error in SendScheduleCommand: {ex.Message}");
			throw;
			}
		}

	/// <summary>
	/// Gets the list of device IDs assigned to this schedule.
	/// </summary>
	/// <value>A list of device IDs.</value>
	/// <remarks>
	/// Gets the list of device IDs assigned to this schedule. This property returns the device IDs from the internal assignment list, which is used for device-based schedules such as OnOff and Level schedules. The list may be empty if no devices are assigned.
	/// </remarks>
	public List<int> DeviceIds => DeviceIds1;

	/// <summary>
	/// Gets the list of assignment dictionaries for this schedule.
	/// </summary>
	/// <value>A list of assignment dictionaries.</value>
	/// <remarks>
	/// Gets the list of assignment dictionaries for this schedule. Each dictionary contains assignment details such as id and name. This property is used to access the full assignment information for display, processing, or transmission.
	/// </remarks>
	public List<IDictionary<string, object>> Assignments => Assignments1;

	/// <summary>
	/// Gets the list of assignment IDs for this schedule.
	/// </summary>
	/// <value>A list of assignment IDs.</value>
	/// <remarks>
	/// Gets the list of assignment IDs for this schedule. The IDs are extracted from the assignment dictionaries and are used to identify which rooms or devices are assigned to this schedule. The list may be empty if no assignments exist.
	/// </remarks>
	public List<int> AssignmentIds => [.. Assignments1.Select (a => ConvertInvariant.ToInt32 (a["id"]))];

	/// <summary>
	/// Gets the list of assignment names for this schedule.
	/// </summary>
	/// <value>A list of assignment names.</value>
	/// <remarks>
	/// Gets the list of assignment names for this schedule. The names are extracted from the assignment dictionaries and are used for display and identification purposes. The list may be empty if no assignments exist.
	/// </remarks>
	public List<string> AssignmentNames =>
		 [.. Assignments1.Select(a =>
		  (a.TryGetValue("name", out var n) ? n?.ToString() : null) ?? TEXT_UNKNOWN)];
	/// <summary>
	/// Gets the current setting for the schedule, which may be temperature, state, or level depending on the schedule type.
	/// </summary>
	/// <value>The current setting (temperature, state, or level), or <see langword="null"/> if not recognized.</value>
	/// <remarks>
	/// Gets the current setting for the schedule, which may be temperature, state, or level depending on the schedule type. The value returned depends on the schedule type and may be <see langword="null"/> if not recognized. This property is used to access the current value being controlled by the schedule.
	/// </remarks>
	public object? CurrentSetting =>
			 Type switch
				 {
					 nameof (WiserScheduleType.Heating) =>
							  WiserTemperatureFunctions.FromWiserTemp (
									 ScheduleData1.TryGetValue ("CurrentSetpoint", out var setpoint) ? setpoint : Constants.TEMP_MINIMUM),

					 nameof (WiserScheduleType.OnOff) =>
							  ScheduleData1.TryGetValue ("CurrentState", out var state) ? state : Constants.TEXT_UNKNOWN,

					 nameof (WiserScheduleType.Level) =>
							  ScheduleData1.TryGetValue ("CurrentLevel", out var level) ? level : Constants.TEXT_UNKNOWN,

					 _ => null
					 };

	/// <summary>
	/// Gets the unique identifier for this schedule.
	/// </summary>
	/// <value>The schedule ID.</value>
	/// <remarks>
	/// Gets the unique identifier for this schedule. This is used to reference the schedule in hub operations and is extracted from the schedule data dictionary. If the ID is not present, 0 is returned.
	/// </remarks>
	public int Id => ScheduleData1.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>
	/// Gets the name of the schedule, or <see langword="null"/> if not set.
	/// </summary>
	/// <value>The schedule name.</value>
	/// <remarks>
	/// Gets the name of the schedule, or <see langword="null"/> if not set. The name is used for display and identification purposes and is extracted from the schedule data dictionary.
	/// </remarks>
	public string? Name => ScheduleData1.TryGetValue ("Name", out var name) ? name.ToString () : null;

	/// <summary>
	/// Gets the next scheduled change for this schedule, or <see langword="null"/> if not available.
	/// </summary>
	/// <value>The next scheduled change as a <see cref="WiserScheduleNext"/> object.</value>
	/// <remarks>
	/// Gets the next scheduled change for this schedule, or <see langword="null"/> if not available. The next scheduled change is represented as a WiserScheduleNext object and provides information about the next event in the schedule.
	/// </remarks>
	public WiserScheduleNext? Next =>
			 ScheduleData1.TryGetValue ("Next", out var next) && next is Dictionary<string, object> nextDict
					? new WiserScheduleNext (Type, nextDict)
					: null;

	/// <summary>
	/// Gets the schedule data with non-essential elements removed.
	/// </summary>
	/// <value>A dictionary containing the cleaned schedule data.</value>
	/// <remarks>
	/// Gets the schedule data with non-essential elements removed. This property returns a dictionary containing the cleaned schedule data, which is used for serialization and transmission to the hub.
	/// </remarks>
	public IDictionary<string, object> ScheduleData => RemoveScheduleElements (ScheduleData1);

	/// <summary>
	/// Gets the schedule data formatted for websocket transmission.
	/// </summary>
	/// <value>A dictionary containing the schedule data in websocket format.</value>
	/// <remarks>
	/// Gets the schedule data formatted for websocket transmission. This property prepares the schedule data for use in websocket APIs, including assignments and slot data. The returned dictionary contains all relevant information for websocket communication.
	/// </remarks>
	public IDictionary<string, object> WsScheduleData
		{
		get
			{
			IDictionary<string, object>? converted = ConvertFromWiserSchedule (ScheduleData, genericSetpoint: true);
			if (converted == null)
				{
				return new Dictionary<string, object> ();
				}

			IDictionary<string, object> s = RemoveScheduleElements (converted);
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

	/// <summary>
	/// Gets the type of schedule (e.g., Heating, OnOff, Level).
	/// </summary>
	/// <value>The schedule type as a string.</value>
	/// <remarks>
	/// Gets the type of schedule (e.g., Heating, OnOff, Level). This property is used to distinguish between different schedule categories and is set during construction.
	/// </remarks>
	public string ScheduleType => Type;

	/// <summary>
	/// Logger instance for schedule operations.
	/// </summary>
	/// <remarks>
	/// Logger instance for schedule operations. This logger is used for logging errors and information related to schedule actions and is initialized for the WiserSchedule type.
	/// </remarks>
	protected static ILog Logger { get; set; } = log4net.LogManager.GetLogger (typeof (WiserSchedule));

	/// <summary>
	/// Gets the REST controller used to communicate with the Wiser hub.
	/// </summary>
	/// <remarks>
	/// Gets the REST controller used to communicate with the Wiser hub. This controller is used for all RESTful interactions with the hub and is set during construction.
	/// </remarks>
	protected WiserRestController WiserRestController { get; } = wiserRestController;

	/// <summary>
	/// Gets the type of the schedule (Heating, OnOff, Level).
	/// </summary>
	/// <remarks>
	/// Gets the type of the schedule (Heating, OnOff, Level). This property is set during construction and determines the schedule's category.
	/// </remarks>
	protected string Type { get; } = scheduleType;

	/// <summary>
	/// Gets the raw schedule data as a concurrent dictionary.
	/// </summary>
	/// <remarks>
	/// Gets the raw schedule data as a concurrent dictionary. This contains all schedule data before any filtering or conversion and is initialized from the provided schedule data dictionary.
	/// </remarks>
	protected ConcurrentDictionary<string, object> ScheduleData1 { get; } = new ConcurrentDictionary<string, object> (scheduleData);

	/// <summary>
	/// Gets the dictionary of sunrise times by day.
	/// </summary>
	/// <remarks>
	/// Gets the dictionary of sunrise times by day. This property is used for schedules that reference sunrise events and is set during construction.
	/// </remarks>
	protected IDictionary<string, string> Sunrises { get; } = sunrises;

	/// <summary>
	/// Gets the dictionary of sunset times by day.
	/// </summary>
	/// <remarks>
	/// Gets the dictionary of sunset times by day. This property is used for schedules that reference sunset events and is set during construction.
	/// </remarks>
	protected IDictionary<string, string> Sunsets { get; } = sunsets;

	/// <summary>
	/// Gets the list of assignment dictionaries for this schedule.
	/// </summary>
	/// <remarks>
	/// Gets the list of assignment dictionaries for this schedule. Each assignment dictionary contains details about the assignment (e.g., room or device) and is used for processing assignments.
	/// </remarks>
	protected List<IDictionary<string, object>> Assignments1 { get; } = [];

	/// <summary>
	/// Gets the list of device IDs assigned to this schedule.
	/// </summary>
	/// <remarks>
	/// Gets the list of device IDs assigned to this schedule. This property is used for device-based schedules (e.g., OnOff, Level) and is initialized as an empty list.
	/// </remarks>
	protected List<int> DeviceIds1 { get; } = [];

	/// <summary>
	/// Asynchronously copies the schedule to a specified ID.
	/// </summary>
	/// <remarks>
	/// Asynchronously copies the schedule to a specified ID. This method sends an UPDATE command to the hub to copy the schedule data to another schedule ID. If the operation is successful, <see langword="true"/> is returned; otherwise, <see langword="false"/> is returned and the error is logged.
	/// </remarks>
	/// <param name="toId">The ID to copy the schedule to.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the copy operation was successful; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> CopyScheduleAsync (int toId, CancellationToken cancellationToken = default)
		{
		try
			{
			_ = await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (ScheduleData1), toId, cancellationToken).ConfigureAwait (false);
			return true;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error copying schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously deletes the schedule.
	/// </summary>
	/// <remarks>
	/// HotWater schedules (ID 1000) cannot be deleted. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the delete operation was successful; otherwise, <see langword="false"/>.</returns>
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
				Logger.Error ("You cannot delete the schedule for HotWater");
				return false;
				}
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error deleting schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Saves the schedule to a file in JSON format.
	/// </summary>
	/// <remarks>
	/// Serializes the schedule data and writes it to the specified file path. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleFile">The file path to save the schedule to.</param>
	/// <returns><see langword="true"/> if the save operation was successful; otherwise, <see langword="false"/>.</returns>
	public bool SaveScheduleToFile (string scheduleFile)
		{
		try
			{
			File.WriteAllText (scheduleFile, JsonConvert.SerializeObject (EnsureType (ScheduleData1), Newtonsoft.Json.Formatting.Indented));
			return true;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error saving schedule to file: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Saves the schedule to a YAML file.
	/// </summary>
	/// <remarks>
	/// Serializes the schedule data in YAML format and writes it to the specified file path. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleYamlFile">The file path to save the schedule to in YAML format.</param>
	/// <returns><see langword="true"/> if the save operation was successful; otherwise, <see langword="false"/>.</returns>
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
			Logger.Error ($"Error saving schedule to yaml file: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously sets the schedule using the provided data.
	/// </summary>
	/// <remarks>
	/// Sends an UPDATE command to the hub with the provided schedule data. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleData">The schedule data to set.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the set operation was successful; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> SetScheduleAsync (IDictionary<string, object> scheduleData, CancellationToken cancellationToken = default)
		{
		try
			{
			_ = await SendScheduleCommandAsync ("UPDATE", RemoveScheduleElements (scheduleData), cancellationToken: cancellationToken).ConfigureAwait (false);
			return true;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error setting schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously sets the schedule from a JSON file.
	/// </summary>
	/// <remarks>
	/// Reads schedule data from the specified file, validates, and sends it to the hub. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleFile">The file path to the JSON file containing the schedule data.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the set operation was successful; otherwise, <see langword="false"/>.</returns>
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
					Logger.Error ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
					}
				else
					{
					Logger.Error ("The schedule data is null or invalid.");
					}

				return false;
				}
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error setting schedule from file: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously sets the schedule from a YAML file.
	/// </summary>
	/// <remarks>
	/// Reads schedule data from the specified YAML file, validates, and sends it to the hub. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleYamlFile">The file path to the YAML file containing the schedule data.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the set operation was successful; otherwise, <see langword="false"/>.</returns>
	public async Task<bool> SetScheduleFromYamlFileAsync (string scheduleYamlFile, CancellationToken cancellationToken = default)
		{
		try
			{
			var deserializer = new YamlDotNet.Serialization.Deserializer ();
			IDictionary<string, object> scheduleData = deserializer.Deserialize<IDictionary<string, object>> (File.ReadAllText (scheduleYamlFile));
			if (ValidateScheduleType (scheduleData))
				{
				IDictionary<string, object>? schedule = ConvertToWiserSchedule (scheduleData);
				if (schedule == null)
					{
					Logger.Error ("The converted schedule data is null or invalid.");
					return false;
					}

				_ = await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
				return true;
				}
			else
				{
				Logger.Error ($"This is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
				return false;
				}
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error setting schedule from yaml file: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously sets the schedule using data received from a websocket.
	/// </summary>
	/// <remarks>
	/// Validates and sends websocket schedule data to the hub. Returns <see langword="true"/> if the operation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleData">The schedule data received from the websocket.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the set operation was successful; otherwise, <see langword="false"/>.</returns>
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
							var day = entryDict["day"].ToString ();
							if (!string.IsNullOrEmpty (day))
								{
								scheduleJson[day] = entryDict["slots"];
								}
							}
						}
					}

				IDictionary<string, object>? schedule = ConvertToWiserSchedule (scheduleJson);
				if (schedule == null)
					{
					Logger.Error ("The converted schedule data is null or invalid.");
					return false;
					}

				_ = await SetScheduleAsync (schedule, cancellationToken).ConfigureAwait (false);
				return true;
				}
			else
				{
				Logger.Error ($"{(scheduleData.TryGetValue ("Type", out var type) ? type : Constants.TEXT_UNKNOWN)} is an incorrect schedule type for this device. It should be a {ScheduleType} schedule.");
				return false;
				}
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error setting schedule from websocket data: {ex.Message}");
			return false;
			}
		}
	}

/// <summary>
/// Represents a heating schedule for Wiser devices.
/// </summary>
/// <param name="wiserRestController">The REST controller used to communicate with the Wiser hub.</param>
/// <param name="scheduleType">The type of schedule (e.g., Heating).</param>
/// <param name="scheduleData">The raw schedule data dictionary.</param>
/// <param name="sunrises">Dictionary of sunrise times by day.</param>
/// <param name="sunsets">Dictionary of sunset times by day.</param>
public class WiserHeatingSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
										IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
	{
	/// <summary>
	/// Asynchronously assigns the schedule to a list of room IDs.
	/// </summary>
	/// <remarks>
	/// Asynchronously assigns the schedule to a list of room IDs. This method sends an ASSIGN command to the hub, updating the assignments for the schedule. If <paramref name="includeCurrent"/> is <see langword="true"/>, currently assigned rooms are included in the assignment list. The operation is asynchronous and returns <see langword="true"/> if the assignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="roomIds">The list of room IDs to assign the schedule to.</param>
	/// <param name="includeCurrent">Whether to include currently assigned rooms.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the assignment operation was successful; otherwise, <see langword="false"/>.</returns>
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
			Logger.Error ($"Error assigning schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously unassigns the schedule from a list of room IDs.
	/// </summary>
	/// <remarks>
	/// Asynchronously unassigns the schedule from a list of room IDs. This method updates the assignments by removing the specified room IDs from the current assignment list and sends an ASSIGN command to the hub. The operation is asynchronous and returns <see langword="true"/> if the unassignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="roomIds">The list of room IDs to unassign the schedule from.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the unassignment operation was successful; otherwise, <see langword="false"/>.</returns>
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

	/// <summary>
	/// Converts a day's heating schedule from Wiser format to a YAML-compatible list.
	/// </summary>
	/// <param name="day">The day name (e.g., Monday).</param>
	/// <param name="daySchedule">The Wiser-formatted day schedule payload.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times (e.g., sunrise/sunset) with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key instead of temperature.</param>
	/// <returns>A list of dictionaries representing the day's schedule.</returns>
	protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
		{
		var scheduleSetPoints = new List<IDictionary<string, object>> ();

		if (daySchedule is Dictionary<string, object> dayDict &&
			 dayDict.TryGetValue (TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
			 dayDict.TryGetValue (TEXT_DEGREES_C, out var tempObj) && tempObj is List<object> temps)
			{
			for (var i = 0; i < times.Count; i++)
				{
				var timeValue = ConvertInvariant.ToInt32 (times[i]).ToStringInvariant ("D4");
				var time = DateTime.ParseExact (timeValue, "HHmm", null).ToStringInvariant ("HH:mm");

				scheduleSetPoints.Add (new Dictionary<string, object>
					  {
							{ TEXT_TIME, time },
							{ genericSetpoint ? TEXT_SETPOINT : TEXT_TEMP,
							  WiserTemperatureFunctions.FromWiserTemp(temps[i]) }
					  });
				}
			}

		return [.. scheduleSetPoints.OrderBy (t => t[TEXT_TIME].ToString ())];
		}

	/// <summary>
	/// Converts a day's heating schedule from YAML format back to Wiser format.
	/// </summary>
	/// <param name="daySchedule">The day's schedule in YAML format. May be <see langword="null"/>.</param>
	/// <returns>An object containing arrays for times and temperatures in Wiser format.</returns>
	protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>>? daySchedule)
		{
		var times = new List<string> ();
		var temps = new List<int> ();

		if (daySchedule == null || daySchedule.Count == 0)
			{
			return new Dictionary<string, object>
				{
					 { TEXT_TIME, times },
					 { TEXT_DEGREES_C, temps }
				};
			}

		foreach (IDictionary<string, object> item in daySchedule)
			{
			if (item.TryGetValue (TEXT_TIME, out var timeValue) && timeValue is not null)
				{
				var time = timeValue.ToString ()!.Replace (":", "");
				times.Add (time);
				}

			if ((item.TryGetValue (TEXT_TEMP, out var tempValue) || item.TryGetValue (TEXT_SETPOINT, out tempValue)) && tempValue is not null)
				{
				var temp = tempValue.ToString ()!.Equals (TEXT_OFF, StringComparison.OrdinalIgnoreCase)
					? TEMP_OFF
					: ConvertInvariant.ToDouble (tempValue);

				temps.Add (WiserTemperatureFunctions.ToWiserTemp (temp));
				}
			}

		return new Dictionary<string, object>
			{
				 { TEXT_TIME, times },
				 { TEXT_DEGREES_C, temps }
			};
		}

	/// <summary>
	/// Converts heating schedule data from Wiser format to a generic dictionary.
	/// </summary>
	/// <param name="scheduleData">Wiser-formatted schedule data.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key instead of temperature.</param>
	/// <returns>A generic dictionary of schedule data, or <see langword="null"/> on error.</returns>
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
				if (Weekdays.Contains (day.TitleCase ()) || Weekends.Contains (day.TitleCase ()) || SpecialDays.Contains (day.TitleCase ()))
					{
					List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
					scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
					}
				}

			return scheduleOutput;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error converting from Wiser schedule: {ex.Message}");
			return null;
			}
		}

	/// <summary>
	/// Converts heating schedule data from a generic dictionary to Wiser format.
	/// </summary>
	/// <param name="scheduleData">Generic schedule data.</param>
	/// <returns>A Wiser-formatted schedule dictionary, or <see langword="null"/> on error.</returns>
	protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData)
		{
		var scheduleOutput = new ConcurrentDictionary<string, object> ();

		try
			{
			foreach (KeyValuePair<string, object> kvp in scheduleData)
				{
				var day = kvp.Key;
				if (Constants.Weekdays.Contains (day.TitleCase ()) || Constants.Weekends.Contains (day.TitleCase ()) || Constants.SpecialDays.Contains (day.TitleCase ()))
					{
					var scheduleDay = ConvertYamlToWiserDay ((List<IDictionary<string, object>>)kvp.Value);

					// If using special days, convert to one entry for each weekday
					if (Constants.SpecialDays.Contains (day.TitleCase ()))
						{
						if (day.TitleCase () == Constants.TEXT_WEEKDAYS)
							{
							foreach (var weekday in Constants.Weekdays)
								{
								scheduleOutput[weekday] = scheduleDay;
								}
							}

						if (day.TitleCase () == Constants.TEXT_WEEKENDS)
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
			Logger.Error ($"Error converting to Wiser schedule: {ex.Message}");
			return null;
			}
		}
	}

/// <summary>
/// Represents an on/off schedule for Wiser devices.
/// </summary>
/// <param name="wiserRestController">The REST controller used to communicate with the Wiser hub.</param>
/// <param name="scheduleType">The type of schedule (e.g., OnOff).</param>
/// <param name="scheduleData">The raw schedule data dictionary.</param>
/// <param name="sunrises">Dictionary of sunrise times by day.</param>
/// <param name="sunsets">Dictionary of sunset times by day.</param>
public class WiserOnOffSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
	{
	/// <summary>
	/// Gets the list of device type IDs assigned to this on/off schedule.
	/// </summary>
	/// <value>A list of device type IDs.</value>
	public List<int> DeviceTypeIds { get; } = [];

	/// <summary>
	/// Asynchronously assigns the schedule to a list of device IDs.
	/// </summary>
	/// <remarks>
	/// Asynchronously assigns the schedule to a list of device IDs. This method sends an ASSIGN command to the hub, updating the assignments for the schedule. If <paramref name="includeCurrent"/> is <see langword="true"/>, currently assigned devices are included in the assignment list. The operation is asynchronous and returns <see langword="true"/> if the assignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="deviceIds">The list of device IDs to assign the schedule to.</param>
	/// <param name="includeCurrent">Whether to include currently assigned devices.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the assignment operation was successful; otherwise, <see langword="false"/>.</returns>
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
			Logger.Error ($"Error assigning schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously unassigns the schedule from a list of device IDs.
	/// </summary>
	/// <remarks>
	/// Asynchronously unassigns the schedule from a list of device IDs. This method updates the assignments by removing the specified device IDs from the current assignment list and sends an ASSIGN command to the hub. The operation is asynchronous and returns <see langword="true"/> if the unassignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="deviceIds">The list of device IDs to unassign the schedule from.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns><see langword="true"/> if the unassignment operation was successful; otherwise, <see langword="false"/>.</returns>
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

	/// <summary>
	/// Converts a day's on/off schedule from Wiser format to a YAML-compatible list.
	/// </summary>
	/// <param name="day">The day name (e.g., Monday).</param>
	/// <param name="daySchedule">The Wiser-formatted day schedule list.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key instead of state.</param>
	/// <returns>A list of dictionaries representing the day's schedule.</returns>
	protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
		{
		var scheduleSetPoints = new List<IDictionary<string, object>> ();

		if (daySchedule is List<object> dayList)
			{
			foreach (var item in dayList)
				{
				var timeValue = ConvertInvariant.ToInt32 (item);
				var absTime = Math.Abs (timeValue);

				if (absTime > 2400)
					{
					absTime = 0;
					}

				var time = absTime.ToStringInvariant ("D4");
				var formattedTime = DateTime.ParseExact (time, "HHmm", null).ToStringInvariant ("HH:mm");

				scheduleSetPoints.Add (new Dictionary<string, object>
					  {
							{ Constants.TEXT_TIME, formattedTime },
							{ genericSetpoint ? Constants.TEXT_SETPOINT : Constants.TEXT_STATE,
							  timeValue == absTime ? Constants.TEXT_ON : Constants.TEXT_OFF }
					  });
				}
			}

		return [.. scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ())];
		}

	/// <summary>
	/// Converts a day's on/off schedule from YAML format back to Wiser format.
	/// </summary>
	/// <param name="daySchedule">The day's schedule in YAML format. May be <see langword="null"/>.</param>
	/// <returns>A list of times (signed to represent on/off state) in Wiser format.</returns>
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

				if (entry.TryGetValue ("Time", out var timeValue) && timeValue is not null && IsValidTime (timeValue.ToString ()!))
					{
					time = timeValue.ToString ()!.Replace (":", "").ParseIntInvariant ();
					time = time != 0 ? time : 2400;
					}

				if ((entry.TryGetValue ("State", out var stateValue) || entry.TryGetValue (TEXT_SETPOINT, out stateValue)) && stateValue is not null &&
					 stateValue.ToString ()!.TitleCase () == TEXT_OFF)
					{
					time = time != 0 ? -Math.Abs (time) : -2400;
					}

				times.Add (time);
				}
			catch (Exception ex)
				{
				Logger.Error ($"Error in ConvertYamlToWiserDay: {ex.Message}");
				times.Add (0);
				}
			}

		return times;
		}

	/// <summary>
	/// Converts on/off schedule data from Wiser format to a generic dictionary.
	/// </summary>
	/// <param name="scheduleData">Wiser-formatted schedule data.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key.</param>
	/// <returns>A generic dictionary of schedule data, or <see langword="null"/> on error.</returns>
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
				if (Weekdays.Contains (day.TitleCase ()) || Weekends.Contains (day.TitleCase ()) || SpecialDays.Contains (day.TitleCase ()))
					{
					List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
					scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
					}
				}

			return scheduleOutput;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error converting from Wiser schedule: {ex.Message}");
			return null;
			}
		}

	/// <summary>
	/// Converts on/off schedule data from a generic dictionary to Wiser format.
	/// </summary>
	/// <param name="scheduleData">Generic schedule data.</param>
	/// <returns>A Wiser-formatted schedule dictionary, or <see langword="null"/> on error.</returns>
	protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData)
		{
		var scheduleOutput = new ConcurrentDictionary<string, object> ();

		try
			{
			foreach (KeyValuePair<string, object> kvp in scheduleData)
				{
				var day = kvp.Key;
				if (Weekdays.Contains (day.TitleCase ()) || Weekends.Contains (day.TitleCase ()) || SpecialDays.Contains (day.TitleCase ()))
					{
					var scheduleDay = ConvertYamlToWiserDay (kvp.Value as List<IDictionary<string, object>>);

					// If using special days, convert to one entry for each weekday
					if (SpecialDays.Contains (day.TitleCase ()))
						{
						if (day.TitleCase () == TEXT_WEEKDAYS)
							{
							foreach (var weekday in Weekdays)
								{
								scheduleOutput[weekday] = scheduleDay;
								}
							}

						if (day.TitleCase () == TEXT_WEEKENDS)
							{
							foreach (var weekendDay in Weekends)
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
			Logger.Error ($"Error converting to Wiser schedule: {ex.Message}");
			return null;
			}
		}
	}

/// <summary>
/// Represents a level schedule for Wiser devices (e.g., shutters, lights).
/// </summary>
/// <param name="wiserRestController">The REST controller used to communicate with the Wiser hub.</param>
/// <param name="scheduleType">The type of schedule (e.g., Level).</param>
/// <param name="scheduleData">The raw schedule data dictionary.</param>
/// <param name="sunrises">Dictionary of sunrise times by day.</param>
/// <param name="sunsets">Dictionary of sunset times by day.</param>
public class WiserLevelSchedule (WiserRestController wiserRestController, string scheduleType, IDictionary<string, object> scheduleData,
 IDictionary<string, string> sunrises, IDictionary<string, string> sunsets) : WiserSchedule (wiserRestController, scheduleType, scheduleData, sunrises, sunsets)
	{
	/// <summary>
	/// Gets the type of the level (e.g., Shutters, Lights).
	/// </summary>
	/// <value>The level type as a string.</value>
	public string LevelType => ScheduleData1.GetStringOr ("Type");

	/// <summary>
	/// Gets the ID associated with the level type.
	/// </summary>
	/// <value>The level type ID.</value>
	public int LevelTypeId => LevelType == WiserScheduleType.Shutters.ToString () ? 2 : 1;

	/// <summary>
	/// Gets the next scheduled change for this level schedule, or a default if not available.
	/// </summary>
	/// <value>The next scheduled change as a <see cref="WiserScheduleNext"/> object.</value>
	public new WiserScheduleNext? Next => ScheduleData1.TryGetValue ("Next", out var next)
			? next is Dictionary<string, object> nextDict
				 ? new WiserScheduleNext (Type, nextDict)
				 : new WiserScheduleNext (Type, new Dictionary<string, object> { { "Day", "" }, { "Time", 0 }, { "Level", 0 } })
			: null;

	/// <summary>
	/// Gets the schedule data for this level schedule, or a default if not available.
	/// </summary>
	/// <value>The schedule data as a dictionary.</value>
	public new IDictionary<string, object>? ScheduleData
		{
		get
			{
			IDictionary<string, object> scheduleData = RemoveScheduleElements (ScheduleData1);
			return scheduleData.Count > 0
				? scheduleData
				: ConvertToWiserSchedule (DefaultLevelSchedule.ToDictionary (kvp => kvp.Key, kvp => (object)kvp.Value));
			}
		}

	/// <summary>
	/// Gets the type of this level schedule.
	/// </summary>
	/// <value>The schedule type as a string.</value>
	public new string ScheduleType => LevelType;

	/// <summary>
	/// Asynchronously assigns the schedule to a list of device IDs.
	/// </summary>
	/// <remarks>
	/// Asynchronously assigns the schedule to a list of device IDs. This method sends an ASSIGN command to the hub, updating the assignments for the schedule. If <paramref name="includeCurrent"/> is <see langword="true"/>, currently assigned devices are included in the assignment list. The operation is asynchronous and returns <see langword="true"/> if the assignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="deviceIds">A list of device IDs to assign the schedule to. If <see langword="null"/>, an empty list is used.</param>
	/// <param name="includeCurrent">If <see langword="true"/>, includes currently assigned devices in the assignment.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns><see langword="true"/> if the assignment operation was successful; otherwise, <see langword="false"/>.</returns>
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
			Logger.Error ($"Error assigning schedule: {ex.Message}");
			return false;
			}
		}

	/// <summary>
	/// Asynchronously unassigns the schedule from a list of device IDs.
	/// </summary>
	/// <remarks>
	/// Unassigns this schedule from the specified device IDs. This method updates the assignments by removing the specified device IDs from the current assignment list and sends an ASSIGN command to the hub. The operation is asynchronous and returns <see langword="true"/> if the unassignment succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="deviceIds">A list of device IDs to unassign the schedule from. If <see langword="null"/>, an empty list is used.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns><see langword="true"/> if the unassignment operation was successful; otherwise, <see langword="false"/>.</returns>
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

	/// <summary>
	/// Converts a day's level schedule from Wiser format to a YAML-compatible list.
	/// </summary>
	/// <param name="day">The day name (e.g., Monday).</param>
	/// <param name="daySchedule">The Wiser-formatted day schedule payload.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key instead of the level key.</param>
	/// <returns>A list of dictionaries representing the day's schedule.</returns>
	protected override List<IDictionary<string, object>> ConvertWiserToYamlDay (string day, object daySchedule, bool replaceSpecialTimes = false, bool genericSetpoint = false)
		{
		var scheduleSetPoints = new List<IDictionary<string, object>> ();

		if (daySchedule is IDictionary<string, object> dayDict &&
			 dayDict.TryGetValue (TEXT_TIME, out var timeObj) && timeObj is List<object> times &&
			 dayDict.TryGetValue (TEXT_LEVEL, out var levelObj) && levelObj is List<object> levels)
			{
			for (var i = 0; i < times.Count; i++)
				{
				var timeValue = ConvertInvariant.ToInt32 (times[i]);
				string timeStr;

				timeStr = Constants.SpecialTimes.ContainsValue (timeValue)
					? replaceSpecialTimes
						? timeValue == Constants.SpecialTimes["Sunrise"]
								 ? Sunrises[day]
								 : Sunsets[day]
						: Constants.SpecialTimes.FirstOrDefault (x => x.Value == timeValue).Key
					: DateTime.ParseExact (timeValue.ToStringInvariant ("D4"), "HHmm", null).ToStringInvariant ("D4");

				scheduleSetPoints.Add (new Dictionary<string, object>
					  {
							{ TEXT_TIME, timeStr },
							{ genericSetpoint ? TEXT_SETPOINT : TEXT_LEVEL, levels[i] }
					  });
				}
			}

		return [.. scheduleSetPoints.OrderBy (t => t[Constants.TEXT_TIME].ToString ())];
		}

	/// <summary>
	/// Converts a day's level schedule from YAML format back to Wiser format.
	/// </summary>
	/// <param name="daySchedule">The day's schedule in YAML format.</param>
	/// <returns>An object with Wiser-formatted arrays for times and levels.</returns>
	protected override object ConvertYamlToWiserDay (List<IDictionary<string, object>> daySchedule)
		{
		var times = new List<int> ();
		var levels = new List<int> ();

		foreach (IDictionary<string, object> entry in daySchedule)
			{
			foreach (KeyValuePair<string, object> kvp in entry)
				{
				var titleKey = kvp.Key.TitleCase ();
				if (titleKey == TEXT_TIME)
					{
					var yamlTime = kvp.Value.ToString ()!;
					var specialTime = yamlTime.TitleCase ();
					var time = Constants.SpecialTimes.TryGetValue (specialTime, out var value)
						? value : IsValidTime (yamlTime) ? yamlTime.Replace (":", "").ParseIntInvariant () : 0;
					times.Add (time);
					}

				if (titleKey is TEXT_LEVEL or TEXT_SETPOINT)
					{
					levels.Add (ConvertInvariant.ToInt32 (kvp.Value));
					}
				}
			}

		return new Dictionary<string, object>
			{
				 { TEXT_TIME, times },
				 { TEXT_LEVEL, levels }
			};
		}

	/// <summary>
	/// Converts level schedule data from Wiser format to a generic dictionary.
	/// </summary>
	/// <param name="scheduleData">Wiser-formatted schedule data.</param>
	/// <param name="replaceSpecialTimes">If <see langword="true"/>, replaces special times with actual values.</param>
	/// <param name="genericSetpoint">If <see langword="true"/>, uses a generic setpoint key.</param>
	/// <returns>A generic dictionary of schedule data, or <see langword="null"/> on error.</returns>
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
				if (Weekdays.Contains (day.TitleCase ()) || Weekends.Contains (day.TitleCase ()) || SpecialDays.Contains (day.TitleCase ()))
					{
					List<IDictionary<string, object>> scheduleSetPoints = ConvertWiserToYamlDay (day, kvp.Value, replaceSpecialTimes, genericSetpoint);
					scheduleOutput[day.Capitalize ()] = scheduleSetPoints;
					}
				}

			return scheduleOutput;
			}
		catch (Exception ex)
			{
			Logger.Error ($"Error converting from Wiser schedule: {ex.Message}");
			return null;
			}
		}

	/// <summary>
	/// Converts level schedule data from a generic dictionary to Wiser format.
	/// </summary>
	/// <param name="scheduleData">Generic schedule data.</param>
	/// <returns>A Wiser-formatted schedule dictionary, or <see langword="null"/> on error.</returns>
	protected override IDictionary<string, object>? ConvertToWiserSchedule (IDictionary<string, object> scheduleData)
		{
		var scheduleOutput = new ConcurrentDictionary<string, object> ();

		try
			{
			foreach (KeyValuePair<string, object> kvp in scheduleData)
				{
				var day = kvp.Key;
				if (Weekdays.Contains (day.TitleCase ()) || Weekends.Contains (day.TitleCase ()) || SpecialDays.Contains (day.TitleCase ()))
					{
					var scheduleDay = ConvertYamlToWiserDay ((List<IDictionary<string, object>>)kvp.Value);

					// If using special days, convert to one entry for each weekday
					if (SpecialDays.Contains (day.TitleCase ()))
						{
						if (day.TitleCase () == TEXT_WEEKDAYS)
							{
							foreach (var weekday in Weekdays)
								{
								scheduleOutput[weekday] = scheduleDay;
								}
							}

						if (day.TitleCase () == TEXT_WEEKENDS)
							{
							foreach (var weekendDay in Weekends)
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
			Logger.Error ($"Error converting to Wiser schedule: {ex.Message}");
			return null;
			}
		}
	}

/// <summary>
/// Represents the collection of all schedules managed by the Wiser system.
/// </summary>
public class WiserSchedules
	{
	private static readonly ILog _lOGGER = log4net.LogManager.GetLogger (typeof (WiserSchedules));
	private readonly WiserRestController _wiserRestController;
	private readonly IDictionary<string, string> _sunrises;
	private readonly IDictionary<string, string> _sunsets;

	/// <summary>
	/// Initializes a new instance of the WiserSchedules container and builds the schedule collections
	/// from the provided payloads.
	/// </summary>
	/// <param name="wiserRestController">The REST controller used to communicate with the Wiser hub.</param>
	/// <param name="scheduleData">The raw schedules payload keyed by schedule type.</param>
	/// <param name="sunrises">Dictionary of sunrise times by day.</param>
	/// <param name="sunsets">Dictionary of sunset times by day.</param>
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

	/// <summary>
	/// Updates in-memory schedules and sunrise/sunset tables with new data.
	/// </summary>
	/// <param name="scheduleData">The latest raw schedules payload to rebuild from.</param>
	/// <param name="sunrises">Updated sunrise times by day.</param>
	/// <param name="sunsets">Updated sunset times by day.</param>
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

	/// <summary>
	/// Gets all schedules in the system.
	/// </summary>
	public List<WiserSchedule> All => [.. HeatingSchedules.Cast<WiserSchedule> (),
		 .. OnoffSchedules.Cast<WiserSchedule> (),
#if LIGHT
		.. LevelSchedules.Cast<WiserSchedule> ()
#endif
		];

	/// <summary>
	/// Gets the total number of schedules.
	/// </summary>
	public int Count => All.Count;

	/// <summary>
	/// Gets the list of heating schedules.
	/// </summary>
	public List<WiserHeatingSchedule> HeatingSchedules { get; } = [];

#if LIGHT
	/// <summary>
	/// Gets the list of level schedules.
	/// </summary>
	public List<WiserLevelSchedule> LevelSchedules { get; } = [];
#endif

	/// <summary>
	/// Gets the list of on/off schedules.
	/// </summary>
	public List<WiserOnOffSchedule> OnoffSchedules { get; } = [];

	/// <summary>
	/// Gets a schedule by type and id.
	/// </summary>
	/// <param name="scheduleType">The type of schedule.</param>
	/// <param name="id">The schedule id.</param>
	/// <returns>The matching schedule, or <see langword="null"/> if not found.</returns>
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
#if LIGHT || SHUTTER
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

	/// <summary>
	/// Gets a heating schedule assigned to a specific room ID.
	/// </summary>
	/// <param name="roomId">The room ID to search for.</param>
	/// <returns>The matching heating schedule, or <see langword="null"/> if not found.</returns>
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

	/// <summary>
	/// Gets a schedule by its associated device ID.
	/// </summary>
	/// <param name="deviceId">The device ID to search for.</param>
	/// <returns>The matching schedule, or <see langword="null"/> if not found.</returns>
	public WiserSchedule? GetByDeviceId (int deviceId)
		{
		try
			{
#if LIGHT
			return OnoffSchedules.Concat<WiserSchedule> (LevelSchedules)
#else
			return OnoffSchedules
#endif
				 .FirstOrDefault (s => s.DeviceIds.Contains (deviceId));
			}
		catch (IndexOutOfRangeException)
			{
			return null;
			}
		}

	/// <summary>
	/// Gets a schedule by its name and type.
	/// </summary>
	/// <param name="scheduleType">The type of the schedule.</param>
	/// <param name="name">The name of the schedule.</param>
	/// <returns>The matching schedule, or <see langword="null"/> if not found.</returns>
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

	/// <summary>
	/// Gets all schedules of the specified type.
	/// </summary>
	/// <param name="scheduleType">The type of schedule.</param>
	/// <returns>A list of schedules of the specified type.</returns>
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

	/// <summary>
	/// Copies a schedule from one id to another.
	/// </summary>
	/// <remarks>
	/// The source and destination schedules must be of the same type. Returns <see langword="true"/> if the copy succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleType">The type of schedule.</param>
	/// <param name="fromId">The source schedule id.</param>
	/// <param name="toId">The destination schedule id.</param>
	/// <param name="cancellationToken">A cancellation token for the async operation.</param>
	/// <returns><see langword="true"/> if the copy succeeded; otherwise, <see langword="false"/>.</returns>
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
				_lOGGER.Error ($"You cannot copy from {fromSchedule.ScheduleType} to {toSchedule.ScheduleType} schedules. They must be of the same type");
				}
			}
		else
			{
			_lOGGER.Error ($"Invalid schedule id for {(fromSchedule == null ? "from_id" : "to_id")} ");
			}

		return false;
		}

	/// <summary>
	/// Asynchronously creates a new schedule.
	/// </summary>
	/// <remarks>
	/// Sends a CREATE command to the hub with the specified schedule type, name, and optional assignments. Returns <see langword="true"/> if creation succeeds, or <see langword="false"/> if an error occurs.
	/// </remarks>
	/// <param name="scheduleType">The type of the schedule.</param>
	/// <param name="name">The name of the schedule.</param>
	/// <param name="assignments">Optional list of assignments.</param>
	/// <param name="cancellationToken">A cancellation token for the async operation.</param>
	/// <returns><see langword="true"/> if the creation succeeded; otherwise, <see langword="false"/>.</returns>
	public Task<bool> CreateScheduleAsync (WiserScheduleType scheduleType, string name, List<int>? assignments = null, CancellationToken cancellationToken = default)
		{
		assignments ??= [];
		var typeData = new Dictionary<string, object> { { "Name", name } };
#if LIGHT
		if (scheduleType is WiserScheduleType.Lighting or WiserScheduleType.Level)
			{
			typeData["Type"] = 1;
			foreach (KeyValuePair<string, Dictionary<string, object>> kvp in DefaultLevelSchedule)
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
			foreach (KeyValuePair<string, Dictionary<string, object>> kvp in DefaultLevelSchedule)
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
