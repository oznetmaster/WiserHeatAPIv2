//-----------------------------------------------------------------------
// <copyright file="WiserRoom.cs" company="">
//     Author:  
//     Copyright (c) . All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.Constants;
using static WiserHeatApiV2.RestConstants;
using static WiserHeatApiV2.EnumValues;

namespace WiserHeatApiV2;

/// <summary>
/// Represents a room in the Wiser system, exposing state, temperatures, devices and room-level commands.
/// </summary>
/// <remarks>
/// Temperatures are exposed in user units and converted via WiserTemperatureFunctions.
/// Many setters/methods send commands to the hub and may have side effects (e.g., cancelling overrides).
/// </remarks>
public class WiserRoom
	{
	private readonly WiserRestController _wiserRestController;
	private readonly ConcurrentDictionary<string, object> _data;
	private string _mode;
	private string _name;
	private bool _windowDetectionActive;

	/// <summary>
	/// Initializes a new instance of the <see cref="WiserRoom"/> class.
	/// </summary>
	/// <param name="wiserRestController">REST controller used to send hub commands.</param>
	/// <param name="room">Raw room data payload from the hub.</param>
	/// <param name="schedule">Heating schedule assigned to this room, if any.</param>
	/// <param name="devices">Devices currently associated with the room.</param>
	/// <remarks>
	/// The <paramref name="devices"/> list is used as the backing list for <see cref="Devices"/> and is updated over time.
	/// </remarks>
	public WiserRoom (WiserRestController wiserRestController, IDictionary<string, object> room, WiserSchedule? schedule, List<WiserDevice> devices)
		{
		_wiserRestController = wiserRestController;
		_data = new ConcurrentDictionary<string, object> (room);
		Schedule = schedule;
		Devices = devices;
		// Initialize properties from the room data
		Id = _data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
		_mode = EffectiveHeatingMode (
			 _data.GetStringOr ("Mode", string.Empty),
			 CurrentTargetTemperature
		);
		_name = room.GetStringOr ("Name", string.Empty);
		_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && ConvertInvariant.ToBoolean (detection);

		// Add device id to schedule
		Schedule?.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
		}

#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _updateLock = new();
#else
	private readonly object _lockUpdate = new ();
#endif

	/// <summary>
	/// Updates the room with latest data, schedule and devices, maintaining assignments.
	/// </summary>
	/// <param name="room">New room data payload.</param>
	/// <param name="schedule">New schedule reference (may be <see langword="null"/>).</param>
	/// <param name="devices">Latest device collection for this room.</param>
	/// <remarks>
	/// Thread-safe: the update is guarded by an internal lock. Keys missing from the new payload are removed.
	/// Devices removed from or added to the room are reconciled. If the room id or name changes, schedule
	/// assignments are updated to reflect the new values.
	/// </remarks>
	public void Update (IDictionary<string, object> room, WiserSchedule? schedule, List<WiserDevice> devices)
		{
#if NET9_0_OR_GREATER
		using (_updateLock.EnterScope())
#else
		lock (_lockUpdate)
#endif			
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
				 _data.GetStringOr ("Mode", string.Empty),
				 CurrentTargetTemperature
				);

			Id = room.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;
			_name = room.GetStringOr ("Name", string.Empty);
			_windowDetectionActive = room.TryGetValue ("WindowDetectionActive", out var detection) && ConvertInvariant.ToBoolean (detection);

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
		if (mode.Equals (TEXT_MANUAL, StringComparison.OrdinalIgnoreCase) && temp == Constants.TEMP_OFF)
			{
			return WiserHeatingMode.Off.ToString ();
			}
		else if (mode.Equals (TEXT_MANUAL, StringComparison.OrdinalIgnoreCase))
			{
			return WiserHeatingMode.Manual.ToString ();
			}

		return WiserHeatingMode.Auto.ToString ();
		}

	private Task<bool> SendCommandAsync (object? cmd, WiserRestAction method = WiserRestAction.PATCH, CancellationToken cancellationToken = default) =>
		_wiserRestController.SendCommandAsync (
			WISER_REST_ROOM.FormatInvariant (Id),
			cmd,
			method,
			cancellationToken
		);

	/// <summary>
	/// Gets the list of supported heating modes for room temperature control.
	/// </summary>
	/// <value>A list containing "Off", "Auto", and "Manual" mode strings.</value>
	public static List<string> AvailableModes => [.. GetValues<WiserHeatingMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>
	/// Gets a value indicating whether Away mode is currently suppressed for this room.
	/// </summary>
	/// <value><c>true</c> if Away mode is suppressed; otherwise, <c>false</c>.</value>
	public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && ConvertInvariant.ToBoolean (suppressed);

	/// <summary>
	/// Gets the end time of the current Boost override in UTC.
	/// </summary>
	/// <value>The UTC DateTime when the current Boost ends, or <see cref="DateTime.MinValue"/> if no Boost is active.</value>
	public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && ConvertInvariant.ToInt32 (time) > 0
		 ? DateTimeOffset.FromUnixTimeSeconds (ConvertInvariant.ToInt32 (time)).DateTime
		 : DateTime.MinValue;

	/// <summary>
	/// Gets the end time of the current Boost override in local time.
	/// </summary>
	/// <value>The local DateTime when the current Boost ends, or <see cref="DateTime.MinValue"/> if no Boost is active.</value>
	public DateTime BoostEndTimeLocal => BoostEndTime.ToLocalTime ();

	/// <summary>
	/// Gets the remaining time for the current Boost override in seconds.
	/// </summary>
	/// <value>The number of seconds remaining for the Boost, or 0 if no Boost is active or it has already expired.</value>
	public double BoostTimeRemaining => IsBoost
		 ? (BoostEndTimeLocal - DateTime.Now).TotalSeconds
		 : 0;

	/// <summary>
	/// Gets the comfort mode score (0-100) for this room if provided by the hub.
	/// </summary>
	/// <value>An integer from 0 to 100 representing the comfort score, or 0 if not available.</value>
	public int ComfortModeScore => _data.TryGetValue ("ComfortModeScore", out var score) ? ConvertInvariant.ToInt32 (score) : 0;

	/// <summary>
	/// Gets the current control direction for the heating system as reported by the hub.
	/// </summary>
	/// <value>A string indicating the control direction (e.g., "Heating", "NoChange").</value>
	public string ControlDirection => _data.GetStringOr ("ControlDirection");

	/// <summary>
	/// Gets the current target (setpoint) temperature for this room in user units.
	/// </summary>
	/// <value>The target temperature as a double value in the configured unit system.</value>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("CurrentSetPoint", out var setPoint) ? setPoint : TEMP_MINIMUM);

	/// <summary>
	/// Gets the current measured room temperature in user units.
	/// </summary>
	/// <value>The measured temperature as a double value in the configured unit system.</value>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("CalculatedTemperature", out var temp) ? temp : TEMP_MINIMUM, "current");

	/// <summary>
	/// Gets the current measured room humidity from an attached room stat device.
	/// </summary>
	/// <value>The humidity percentage as an integer (0-100), or <see langword="null"/> if no room stat with humidity sensor is available.</value>
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

	/// <summary>
	/// Gets the demand type category for this room as reported by the hub.
	/// </summary>
	/// <value>A string indicating the demand type (typically "Heating").</value>
	public string DemandType => _data.GetStringOr ("DemandType");

	/// <summary>
	/// Gets the collection of devices currently associated with this room.
	/// </summary>
	/// <value>A live list of <see cref="WiserDevice"/> instances. The list is updated as hub data changes.</value>
	/// <remarks>
	/// This collection is not thread-safe for external writes. Prefer treating it as read-only.
	/// </remarks>
	public List<WiserDevice> Devices
		{
		get;
		}

	/// <summary>
	/// Gets the setpoint temperature currently displayed by the hub in user units.
	/// </summary>
	/// <value>The displayed setpoint as a double value in the configured unit system.</value>
	public double DisplayedSetpoint => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("DisplayedSetPoint", out var setPoint) ? setPoint : TEMP_MINIMUM, "current");

#if HEATACTUATOR
	/// <summary>
	/// Gets the list of heating actuator device identifiers associated with this room.
	/// </summary>
	/// <value>A sorted list of heating actuator device IDs.</value>
	public List<int> HeatingActuatorIds => _data.TryGetValue ("HeatingActuatorIds", out var ids) && ids is List<object> idsList
		 ? [.. idsList.Select (ConvertInvariant.ToInt32).OrderBy (id => id)]
		 : new List<int> ();
#endif
	/// <summary>
	/// Gets the heating rate category for this room as reported by the hub.
	/// </summary>
	/// <value>A string indicating the heating rate (e.g., "VeryQuick", "Quick", "Medium", "Slow").</value>
	public string HeatingRate => _data.GetStringOr ("HeatingRate");

	/// <summary>
	/// Gets the heating system type for this room as reported by the hub.
	/// </summary>
	/// <value>A string indicating the heating type (e.g., "Radiator", "Underfloor").</value>
	public string HeatingType => _data.GetStringOr ("HeatingType");

	/// <summary>
	/// Gets the unique identifier for this room.
	/// </summary>
	/// <value>The room ID as an integer.</value>
	public int Id { get; private set; }

	/// <summary>
	/// Gets a value indicating whether this room is currently controlled by Away mode.
	/// </summary>
	/// <value><see langword="true"/> if the hub indicates an Away-origin setpoint; otherwise, <see langword="false"/>.</value>
	public bool IsAwayMode => (_data.GetNullableStringOr ("SetpointOrigin")?.Contains ("Away") == true) ||
				 (_data.GetNullableStringOr ("SetPointOrigin")?.Contains ("Away") == true);

	/// <summary>
	/// Gets a value indicating whether a Boost override is currently active for this room.
	/// </summary>
	/// <value><see langword="true"/> if the hub indicates a Boost-origin setpoint; otherwise, <see langword="false"/>.</value>
	public bool IsBoost => (_data.GetNullableStringOr ("SetpointOrigin")?.Contains ("Boost") == true) ||
				 (_data.GetNullableStringOr ("SetPointOrigin")?.Contains ("Boost") == true);

	/// <summary>
	/// Gets a value indicating whether any manual override is currently active for this room.
	/// </summary>
	/// <value><see langword="true"/> if an override type other than <c>None</c> is present; otherwise, <see langword="false"/>.</value>
	public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
		type.ToString () != TEXT_UNKNOWN && type.ToString () != TEXT_NONE;

	/// <summary>
	/// Gets a value indicating whether this room is currently calling for heat.
	/// </summary>
	/// <value><c>true</c> if the room is actively heating; otherwise, <c>false</c>.</value>
	public bool IsHeating => _data.TryGetValue ("ControlOutputState", out var state) && state.ToString () == TEXT_ON;

	/// <summary>
	/// Gets the manual target temperature in user units, if a manual setpoint has been set.
	/// </summary>
	/// <value>The manual setpoint as a double value in the configured unit system.</value>
	public double ManualTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("ManualSetPoint", out var setPoint) ? setPoint : TEMP_MINIMUM);

	/// <summary>
	/// Gets 
	/// </summary>
	/// <value></value>
	/// <remarks>
	/// Setter is synchronous and delegates to <see cref="SetModeAsync(string, CancellationToken)"/>.
	/// Setting the mode may cancel active overrides and can change the target temperature (e.g., setting Off applies a special off setpoint).
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown by the setter if the value cannot be parsed to <see cref="WiserHeatingMode"/>.</exception>
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

	/// <summary>
	/// Sets the room heating mode.
	/// </summary>
	/// <param name="value">Target mode string (Auto, Manual, Off).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the hub accepted the change; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// A manual override is cancelled before applying the new mode.
	/// When switching to Off, a special off setpoint is applied. When switching to Manual from Off and the
	/// current target equals the off setpoint, the scheduled target is restored.
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown if the value is not a valid <see cref="WiserHeatingMode"/>.</exception>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetModeAsync (string value, CancellationToken cancellationToken = default)
		{
		try
			{
			WiserHeatingMode mode = ParseOrThrow<WiserHeatingMode> (value, ignoreCase: true);

			// Cancel any overrides on mode change
			if (IsOverride)
				{
				_ = await CancelOverridesAsync (cancellationToken).ConfigureAwait (false);
				}

			if (mode == WiserHeatingMode.Off)
				{
				_ = await SetManualTemperatureAsync (TEMP_OFF, cancellationToken).ConfigureAwait (false);
				}
			else if (mode == WiserHeatingMode.Manual)
				{
				if (await SendCommandAsync (new
					{
					Mode = WiserHeatingMode.Manual.ToString ()
					}, cancellationToken: cancellationToken).ConfigureAwait (false))
					{
					if (CurrentTargetTemperature == TEMP_OFF)
						{
						_ = await SetTargetTemperatureAsync (ScheduledTargetTemperature, cancellationToken).ConfigureAwait (false);
						}
					}
				}
			else if (mode == WiserHeatingMode.Auto)
				{
				_ = await SendCommandAsync (new
					{
					Mode = WiserHeatingMode.Auto.ToString ()
					}, cancellationToken: cancellationToken).ConfigureAwait (false);
				}

			_mode = mode.ToString ();
			return true;
			}
		catch (ArgumentException)
			{
			throw new ArgumentException ($"{value} is not a valid Heating mode. Valid modes are {string.Join (", ", AvailableModes)}");
			}
		}

	/// <summary>
	/// Gets or sets the room name.
	/// </summary>
	/// <remarks>
	/// Setter is synchronous and delegates to <see cref="SetNameAsync(string, CancellationToken)"/>.
	/// The hub may normalize the name (e.g., title-case).
	/// </remarks>
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

	/// <summary>
	/// Sets the room name (title-cased by the hub).
	/// </summary>
	/// <param name="value">Desired name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the hub accepted the change; otherwise, <see langword="false"/>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetNameAsync (string value, CancellationToken cancellationToken = default)
		{
		if (await SendCommandAsync (new
			{
			Name = value.TitleCase ()
			}, cancellationToken: cancellationToken).ConfigureAwait (false))
			{
			_name = value.TitleCase ();
			return true;
			}

		return false;
		}

#if HEATACTUATOR
	/// <summary>Gets the number of heating actuators associated with this room.</summary>
	public int NumberOfHeatingActuators => HeatingActuatorIds.Count;
#endif

	/// <summary>Gets the number of smart valves associated with this room.</summary>
	public int NumberOfSmartvalves => SmartvalveIds.Count;

	/// <summary>
	/// Gets the override target temperature (if an override is active) in user units.
	/// </summary>
	/// <value>The override setpoint as a double value, or 0 if no override is active.</value>
	public double OverrideTargetTemperature => _data.TryGetValue ("OverrideSetpoint", out var setPoint) ? ConvertInvariant.ToDouble (setPoint) / 10 : 0;

	/// <summary>
	/// Gets the type of override currently applied to this room.
	/// </summary>
	/// <value>A string indicating the override type (e.g., "Manual", "Boost"), or "None" if no override is active.</value>
	public string OverrideType => _data.GetStringOr ("OverrideType", TEXT_NONE);

	/// <summary>
	/// Gets the current heating demand percentage for this room.
	/// </summary>
	/// <value>An integer from 0 to 100 representing the demand percentage.</value>
	public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

	/// <summary>
	/// Gets the device identifier of the room thermostat assigned to this room.
	/// </summary>
	/// <value>The room stat device ID, or <c>null</c> if no room stat is assigned.</value>
	public int? RoomstatId => _data.TryGetValue ("RoomStatId", out var id) ? (int?)ConvertInvariant.ToInt32 (id) : null;

	/// <summary>
	/// Gets the heating schedule assigned to this room.
	/// </summary>
	/// <value>The associated <see cref="WiserSchedule"/> instance, or <c>null</c> if no schedule is assigned.</value>
	public WiserSchedule? Schedule
		{
		get;
		}

	/// <summary>
	/// Gets the schedule identifier associated with this room.
	/// </summary>
	/// <value>The schedule ID, or 0 if no schedule is assigned.</value>
	public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>
	/// Gets the next scheduled target temperature for this room in user units.
	/// </summary>
	/// <value>The scheduled setpoint as a double value in the configured unit system.</value>
	public double ScheduledTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 _data.TryGetValue ("ScheduledSetPoint", out var setPoint) ? setPoint : TEMP_MINIMUM);

	/// <summary>
	/// Gets the list of smart valve device identifiers associated with this room.
	/// </summary>
	/// <value>A sorted list of smart valve device IDs.</value>
	public List<int> SmartvalveIds => _data.TryGetValue ("SmartValveIds", out var ids) && ids is List<object> idsList
		 ? [.. idsList.Select (ConvertInvariant.ToInt32).OrderBy (id => id)]
		 : new List<int> ();

	/// <summary>
	/// Gets the origin/source of the current target temperature setting.
	/// </summary>
	/// <value>A string describing the source (e.g., "FromSchedule", "FromManualOverride", "FromBoost", "FromAwayMode").</value>
	public string TargetTemperatureOrigin => _data.GetNullableStringOr ("SetpointOrigin")
		 ?? _data.GetNullableStringOr ("SetPointOrigin")
		 ?? TEXT_UNKNOWN;

	/// <summary>
	/// Gets the last reported window state for this room (if window detection is enabled).
	/// </summary>
	/// <value>A string indicating the window state ("Open", "Closed", or "Unknown").</value>
	public string WindowState => _data.GetStringOr ("WindowState");
	/// <summary>
	/// Permanently removes this room from the hub configuration.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the room was deleted successfully; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// This operation cannot be undone. All devices assigned to this room will become unassigned.
	/// </remarks>
	public Task<bool> DeleteAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (null, WiserRestAction.DELETE, cancellationToken);

	/// <summary>
	/// Applies a Boost override to increase the room temperature for a specified duration.
	/// </summary>
	/// <param name="incTemp">Temperature increase in user units (degrees).</param>
	/// <param name="duration">Duration in minutes for the Boost (0 cancels any active Boost).</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the Boost request; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// The Boost increases the current setpoint by the specified amount for the given duration.
	/// Setting duration to 0 cancels any active Boost.
	/// </remarks>
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

	/// <summary>
	/// Cancels any active Boost override for this room.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the operation completed successfully; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// This is a no-op if no Boost is currently active.
	/// </remarks>
	public Task<bool> CancelBoostAsync (CancellationToken cancellationToken = default) =>
		IsBoost ? CancelOverridesAsync (cancellationToken) : Task.FromResult (true);

	/// <summary>
	/// Sets a manual target temperature override that remains until manually changed or cancelled.
	/// </summary>
	/// <param name="temp">Target temperature in user units.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the override; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// This override persists until explicitly cancelled or a new override is applied.
	/// </remarks>
	public Task<bool> SetTargetTemperatureAsync (double temp, CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = TEXT_MANUAL,
				SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
				}
			}, cancellationToken: cancellationToken);

	/// <summary>
	/// Sets a manual target temperature override for a specific duration.
	/// </summary>
	/// <param name="temp">Target temperature in user units.</param>
	/// <param name="duration">Duration in minutes for the override.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the override; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// The override automatically expires after the specified duration.
	/// </remarks>
	public Task<bool> SetTargetTemperatureForDurationAsync (double temp, int duration, CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = TEXT_MANUAL,
				DurationMinutes = duration,
				SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
				}
			}, cancellationToken: cancellationToken);

	/// <summary>
	/// Sets a manual target temperature override that expires at the next scheduled change for this room.
	/// </summary>
	/// <param name="temp">Target temperature in user units.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the override; otherwise, <c>false</c>.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no schedule is assigned to this room or no upcoming schedule event exists.</exception>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// The override duration is calculated automatically based on the next scheduled event.
	/// </remarks>
	public Task<bool> SetTargetTemperatureForDurationOfScheduleAsync (double temp, CancellationToken cancellationToken = default) =>
		Schedule == null || Schedule.Next == null
			? throw new InvalidOperationException ("No next schedule available to set duration.")
			: SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = TEXT_MANUAL,
					DurationMinutes = (int)Math.Ceiling ((Schedule.Next.DateTime - DateTime.Now).TotalMinutes),
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (temp)
					}
				}, cancellationToken: cancellationToken);

	/// <summary>
	/// Sets the room to Manual mode and applies a specific target temperature.
	/// </summary>
	/// <param name="temp">Target temperature in user units.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the request; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// This operation switches the room to Manual mode if it's not already in that mode,
	/// then applies the specified temperature as a manual override.
	/// </remarks>
	public Task<bool> SetManualTemperatureAsync (double temp, CancellationToken cancellationToken = default)
		{
		if (Mode != WiserHeatingMode.Manual.ToString ())
			{
			Mode = WiserHeatingMode.Manual.ToString ();
			}

		return SetTargetTemperatureAsync (temp, cancellationToken);
		}

	/// <summary>
	/// Advances the room to the next scheduled setpoint, cancelling any active Boost first.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the advance operation succeeded; otherwise, <c>false</c>.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no schedule is assigned to this room or no upcoming schedule event exists.</exception>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// This method requires both a valid schedule assignment and a next scheduled event to be available.
	/// </remarks>
	public async Task<bool> ScheduleAdvanceAsync (CancellationToken cancellationToken = default) =>
		Schedule == null || Schedule.Next == null
			? throw new InvalidOperationException ("No next schedule available to advance.")
			: await CancelBoostAsync (cancellationToken).ConfigureAwait (false) && await SetTargetTemperatureAsync (ConvertInvariant.ToDouble (Schedule!.Next.Setting), cancellationToken).ConfigureAwait (false);

	/// <summary>
	/// Removes any manual overrides currently applied to this room, returning control to the schedule.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the request; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// After cancelling overrides, the room will follow its assigned schedule (if any).
	/// </remarks>
	public Task<bool> CancelOverridesAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = TEXT_NONE
				}
			}, cancellationToken: cancellationToken);

	/// <summary>
	/// Gets or sets whether window detection is enabled for this room.
	/// </summary>
	/// <remarks>Setter is synchronous and delegates to <see cref="SetWindowDetectionActiveAsync(bool, CancellationToken)"/>.</remarks>
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

	/// <summary>
	/// Enables or disables window detection for this room.
	/// </summary>
	/// <param name="value">True to enable, false to disable.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the hub accepted the change.</returns>
	public async Task<bool> SetWindowDetectionActiveAsync (bool value, CancellationToken cancellationToken = default)
		{
		if (await SendCommandAsync (new
			{
			WindowDetectionActive = value
			}, cancellationToken: cancellationToken).ConfigureAwait (false))
			{
			_windowDetectionActive = value;
			return true;
			}

		return false;
		}
	}

/// <summary>
/// Collection and management helper for rooms, supporting update and search operations.
/// Provides methods to find rooms by various criteria and create new rooms on the hub.
/// </summary>
public class WiserRooms
	{
	private readonly WiserRestController _wiserRestController;

	/// <summary>
	/// Initializes a new rooms collection from hub room payloads and related entities.
	/// </summary>
	/// <param name="wiserRestController">REST controller used to send hub commands.</param>
	/// <param name="roomData">List of room data payloads from the hub.</param>
	/// <param name="schedules">Schedules service for linking heating schedules to rooms.</param>
	/// <param name="devices">Devices service for associating devices with rooms.</param>
	public WiserRooms (WiserRestController wiserRestController, List<Dictionary<string, object>> roomData,
					  WiserSchedules schedules, WiserDevices devices)
		{
		_wiserRestController = wiserRestController;
		// Add room objects
		foreach (Dictionary<string, object> room in roomData)
			{
			WiserSchedule? schedule = schedules.GetByType (WiserScheduleType.Heating)
				  .FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0));
			List<WiserDevice> roomDevices = devices.GetByRoomId (room.TryGetValue ("id", out var roomId) ? ConvertInvariant.ToInt32 (roomId) : 0);
			All.Add (new WiserRoom (
				  wiserRestController,
				  room,
				  schedule,
				  roomDevices
			));
			}
		}

	/// <summary>
	/// Updates the rooms collection with fresh hub data, reconciling additions, removals, and changes.
	/// </summary>
	/// <param name="roomData">Latest list of room payloads from the hub.</param>
	/// <param name="schedules">Updated schedules service.</param>
	/// <param name="devices">Updated devices service.</param>
	/// <remarks>
	/// This method efficiently updates existing room instances rather than recreating them,
	/// preserving object references while synchronizing with the latest hub state.
	/// </remarks>
	public void Update (List<Dictionary<string, object>> roomData, WiserSchedules schedules, WiserDevices devices)
		{
		// For simplicity, just rebuild the collection
		// (You can optimize this if needed)
		// This assumes you have a Build method or similar
		// Build(roomData, schedules, devices);

		// Remove rooms that are not in the new _data
		var newRoomIds = new HashSet<int> (roomData.Select (r => r.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0));
		_ = All.RemoveAll (room => !newRoomIds.Contains (room.Id));

		// Update existing rooms or add new ones
		foreach (Dictionary<string, object> room in roomData)
			{
			WiserSchedule? schedule = schedules.GetByType (WiserScheduleType.Heating)
				.FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0));
			var idroom = room.TryGetValue ("id", out var roomId) ? ConvertInvariant.ToInt32 (roomId) : 0;
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

	/// <summary>
	/// Gets the complete list of all rooms configured on the hub.
	/// </summary>
	/// <value>A list of <see cref="WiserRoom"/> instances representing all rooms.</value>
	public List<WiserRoom> All { get; } = [];

	/// <summary>
	/// Gets the total number of rooms configured on the hub.
	/// </summary>
	/// <value>The count of rooms in the collection.</value>
	public int Count => All.Count;

	/// <summary>
	/// Creates a new room on the hub with the specified name.
	/// </summary>
	/// <param name="name">The name for the new room.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub created the room successfully; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
	/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
	/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
	/// <remarks>
	/// After creating a room, call <see cref="WiserAPI.ReadHubDataAsync"/> to refresh the collection
	/// and access the newly created room instance.
	/// </remarks>
	public Task<bool> AddAsync (string name, CancellationToken cancellationToken = default) =>
		_wiserRestController.SendCommandAsync (RestConstants.WISER_REST_ROOM, new
			{
			name
			}, WiserRestAction.POST, cancellationToken);

	/// <summary>
	/// Finds a room by its unique identifier.
	/// </summary>
	/// <param name="id">The room identifier to search for.</param>
	/// <returns>The matching <see cref="WiserRoom"/> instance, or <c>null</c> if no room with the specified ID exists.</returns>
	public WiserRoom? GetById (int id) => All.FirstOrDefault (room => room.Id == id);

	/// <summary>
	/// Finds a room by its name using case-insensitive comparison.
	/// </summary>
	/// <param name="name">The room name to search for.</param>
	/// <returns>The matching <see cref="WiserRoom"/> instance, or <c>null</c> if no room with the specified name exists.</returns>
	public WiserRoom? GetByName (string name) => All.FirstOrDefault (room => room.Name.Equals (name, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Finds the first room that uses the specified heating schedule.
	/// </summary>
	/// <param name="scheduleId">The schedule identifier to search for.</param>
	/// <returns>The matching <see cref="WiserRoom"/> instance, or <c>null</c> if no room uses the specified schedule.</returns>
	/// <remarks>
	/// If multiple rooms use the same schedule, only the first match is returned.
	/// </remarks>
	public WiserRoom? GetByScheduleId (int scheduleId) => All.FirstOrDefault (room => room.ScheduleId == scheduleId);

	/// <summary>
	/// Finds the room that contains a device with the specified identifier.
	/// </summary>
	/// <param name="deviceId">The device identifier to search for.</param>
	/// <returns>The <see cref="WiserRoom"/> instance containing the device, or <c>null</c> if the device is not assigned to any room.</returns>
	/// <remarks>
	/// This method searches through all devices in all rooms to find the specified device.
	/// </remarks>
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