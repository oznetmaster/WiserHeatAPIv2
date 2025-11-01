// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.RestConstants;
using static WiserHeatApiV2.Constants;
using static WiserHeatApiV2.EnumValues;

namespace WiserHeatApiV2;

/// <summary>
/// Represents hub Hot Water control with mode, state, overrides and schedule integration.
/// </summary>
public class WiserHotwater
	{
	private readonly WiserRestController _wiserRestController;
	private readonly IDictionary<string, object> _data;
	private string _mode;

	/// <summary>
	/// Initializes a new instance of the <see cref="WiserHotwater"/> class from hub data.
	/// </summary>
	/// <param name="wiserRestController">REST controller used to send commands to the hub.</param>
	/// <param name="hwData">Raw hot water data payload from the hub.</param>
	/// <param name="schedule">Optional schedule associated with this hot water controller.</param>
	public WiserHotwater (WiserRestController wiserRestController, IDictionary<string, object> hwData, WiserSchedule? schedule)
		{
		_wiserRestController = wiserRestController;
		_data = hwData;
		Schedule = schedule;
		_mode = _data.GetStringOr ("Mode", TEXT_AUTO);

		// Add device id to schedule
		Schedule?.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
		}

	/// <summary>
	/// Refreshes internal data and updates schedule assignments based on new hub payload.
	/// </summary>
	/// <param name="hwData">Updated hot water data from the hub.</param>
	/// <param name="schedule">Updated schedule reference (may be null).</param>
	public void Update (IDictionary<string, object> hwData, WiserSchedule? schedule)
		{
		var oldId = Id;
		var oldName = Name;

		if (hwData != null)
			{
			_data.Clear ();
			foreach (KeyValuePair<string, object> kv in hwData)
				_data[kv.Key] = kv.Value;
			}

		Schedule = schedule; // Uncomment if you want to update the schedule reference
		_mode = _data.GetStringOr ("Mode", TEXT_AUTO);
		if (Schedule != null)
			{
			if (oldId != Id || oldName != Name)
				{
				// Remove old assignment if the id or name has changed
				_ = Schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
				Schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				}
			}
		}

	private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default) =>
		_wiserRestController.SendCommandAsync (
			 WISER_REST_HOT_WATER.FormatInvariant (Id),
			 cmd,
			 cancellationToken: cancellationToken
		);

	private static bool ValidateMode (string mode) =>
		AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Gets the set of allowed Hot Water operating modes.
	/// </summary>
	/// <value>A list containing the valid mode strings derived from <see cref="WiserHotWaterMode"/>.</value>
#if NETFRAMEWORK
	public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserHotWaterMode))
		 .Cast<WiserHotWaterMode> ()
		 .Select (m => m.ToString ())];
#else
	public static List<string> AvailableModes => [.. GetValues<WiserHotWaterMode> ()
		 .Select (m => m.ToString ())];
#endif

	/// <summary>
	/// Gets a value indicating whether Away mode is currently suppressed for hot water.
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
	/// Gets the remaining time for the current Boost override in seconds.
	/// </summary>
	/// <value>The number of seconds remaining for the Boost, or 0 if no Boost is active.</value>
	public double BoostTimeRemaining => IsBoost
		 ? (BoostEndTime - DateTime.Now).TotalSeconds
		 : 0;

	/// <summary>
	/// Gets a textual description of what is currently controlling the hot water state.
	/// </summary>
	/// <value>A string describing the control source (e.g., "FromSchedule", "FromBoost", "FromAwayMode").</value>
	public string? CurrentControlSource => _data.TryGetValue ("HotWaterDescription", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets the current state of the hot water relay as reported by the hub.
	/// </summary>
	/// <value>The relay state, typically "On" or "Off".</value>
	public string? CurrentState => _data.TryGetValue ("HotWaterRelayState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets the unique identifier for this hot water controller.
	/// </summary>
	/// <value>The hot water controller identifier.</value>
	public int Id => _data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>
	/// Gets a value indicating whether the hot water is currently controlled by Away mode.
	/// </summary>
	/// <value><c>true</c> if Away mode is driving the current state; otherwise, <c>false</c>.</value>
	public bool IsAwayMode => CurrentControlSource == "FromAwayMode";

	/// <summary>
	/// Gets a value indicating whether a Boost override is currently active.
	/// </summary>
	/// <value><c>true</c> if a Boost override is active; otherwise, <c>false</c>.</value>
	public bool IsBoost => CurrentControlSource?.Contains ("Boost") ?? false;

	/// <summary>
	/// Gets a value indicating whether the hot water heating element is currently active.
	/// </summary>
	/// <value><c>true</c> if hot water heating is on; otherwise, <c>false</c>.</value>
	public bool IsHeating => _data.TryGetValue ("WaterHeatingState", out var state) && state.ToString () == Constants.TEXT_ON;

	/// <summary>
	/// Gets a value indicating whether any manual override is currently active.
	/// </summary>
	/// <value><c>true</c> if a manual override is active; otherwise, <c>false</c>.</value>
	public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
															type.ToString () != Constants.TEXT_UNKNOWN &&
															type.ToString () != Constants.TEXT_NONE;

	/// <summary>
	/// Gets or sets the hot water operating mode.
	/// </summary>
	/// <value>One of the values from <see cref="AvailableModes"/> (typically "Auto" or "Manual").</value>
	/// <exception cref="ArgumentException">Thrown when setting an invalid mode value.</exception>
	/// <remarks>
	/// The setter is synchronous and may block. For async operations, consider using a separate async method.
	/// </remarks>
	public string Mode
		{
		get => _mode;
		set
			{
			if (ValidateMode (value))
				{
				if (SendCommandAsync (new
					{
					Mode = value
					}).Result)
					{
					_mode = value;
					}
				}
			else
				{
				throw new ArgumentException ($"{value} is not a valid Hot Water mode. Valid modes are {string.Join (", ", AvailableModes)}");
				}
			}
		}

	/// <summary>
	/// Gets the constant display name for hot water entities.
	/// </summary>
	/// <value>Always returns "HotWater".</value>
	public static string Name => "HotWater";

	/// <summary>
	/// Gets the product type identifier for hot water entities.
	/// </summary>
	/// <value>Always returns "HotWater".</value>
	public static string ProductType => "HotWater";

	/// <summary>
	/// Gets the schedule assigned to this hot water controller, if any.
	/// </summary>
	/// <value>The associated <see cref="WiserSchedule"/> instance, or <c>null</c> if no schedule is assigned.</value>
	public WiserSchedule? Schedule
		{
		get; private set;
		}

	/// <summary>
	/// Gets the schedule identifier associated with this hot water controller.
	/// </summary>
	/// <value>The schedule ID, or 0 if no schedule is assigned.</value>
	public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>
	/// Enables a Boost override for the specified duration, turning hot water on.
	/// </summary>
	/// <param name="duration">Duration in minutes for the Boost.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the Boost request; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public Task<bool> BoostAsync (int duration, CancellationToken cancellationToken = default) =>
		OverrideStateForDurationAsync (Constants.TEXT_ON, duration, cancellationToken);

	/// <summary>
	/// Cancels any active Boost override. This is a no-op if no Boost is currently active.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the operation completed successfully; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public Task<bool> CancelBoostAsync (CancellationToken cancellationToken = default) =>
		IsBoost ? CancelOverridesAsync (cancellationToken) : Task.FromResult (true);

	/// <summary>
	/// Clears any manual overrides currently applied to the hot water controller.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the request; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public Task<bool> CancelOverridesAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestOverride = new
				{
				Type = Constants.TEXT_NONE
				}
			}, cancellationToken);

	/// <summary>
	/// Overrides the hot water state immediately, cancelling any active Boost first.
	/// </summary>
	/// <param name="state">The desired state ("On" or "Off").</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the override; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="state"/> is not "On" or "Off".</exception>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> OverrideStateAsync (string state, CancellationToken cancellationToken = default)
		{
		if (await CancelBoostAsync (cancellationToken).ConfigureAwait (false))
			{
			if (state.Equals (Constants.TEXT_ON, StringComparison.OrdinalIgnoreCase))
				{
				return await SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TEXT_MANUAL,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_ON, "hotwater")
						}
					}, cancellationToken).ConfigureAwait (false);
				}
			else
				{
				return state.Equals (Constants.TEXT_OFF, StringComparison.OrdinalIgnoreCase)
					? await SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TEXT_MANUAL,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_OFF, "hotwater")
							}
						}, cancellationToken).ConfigureAwait (false)
					: throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TEXT_ON} or {Constants.TEXT_OFF}");
				}
			}

		return false;
		}

	/// <summary>
	/// Overrides the hot water state for a specified duration in minutes.
	/// </summary>
	/// <param name="state">The desired state ("On" or "Off").</param>
	/// <param name="duration">Duration in minutes for the override.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the hub accepted the override; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="state"/> is not "On" or "Off".</exception>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public Task<bool> OverrideStateForDurationAsync (string state, int duration, CancellationToken cancellationToken = default)
		{
		if (state.Equals (Constants.TEXT_ON, StringComparison.OrdinalIgnoreCase))
			{
			return SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_MANUAL,
					DurationMinutes = duration,
					SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_ON, "hotwater")
					}
				}, cancellationToken);
			}
		else
			{
			return state.Equals (Constants.TEXT_OFF, StringComparison.OrdinalIgnoreCase)
				? SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TEXT_MANUAL,
						DurationMinutes = duration,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_OFF)
						}
					}, cancellationToken)
				: throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TEXT_ON} or {Constants.TEXT_OFF}");
			}
		}

	/// <summary>
	/// Advances the hot water to the next scheduled state, if a schedule is available and has a next event.
	/// Any active Boost is cancelled first.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>A task that represents the asynchronous operation. The task result is <c>true</c> if the advance operation succeeded; otherwise, <c>false</c>.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	/// <remarks>
	/// This method requires both a valid schedule assignment and a next scheduled event to be available.
	/// </remarks>
	public async Task<bool> ScheduleAdvanceAsync (CancellationToken cancellationToken = default)
		{
		if (Schedule != null && Schedule.Next != null)
			{
			if (await CancelBoostAsync (cancellationToken).ConfigureAwait (false))
				{
				var settingObj = Schedule.Next.Setting;
				string? state = settingObj switch
					{
						string s when s.Equals (TEXT_ON, StringComparison.OrdinalIgnoreCase) || s.Equals (TEXT_OFF, StringComparison.OrdinalIgnoreCase) => s,
						int i => i == 0 ? TEXT_OFF : TEXT_ON,
						long l => l == 0 ? TEXT_OFF : TEXT_ON,
						bool b => b ? TEXT_ON : TEXT_OFF,
						_ => settingObj?.ToString ()
						};

				if (!string.IsNullOrEmpty (state))
					{
					return await OverrideStateAsync (state!, cancellationToken).ConfigureAwait (false);
					}
				}
			}

		return false;
		}
	}