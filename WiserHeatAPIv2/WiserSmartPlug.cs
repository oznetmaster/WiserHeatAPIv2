// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Wiser smart plug with schedule, mode and on/off control.
/// </summary>
public class WiserSmartPlug : WiserDevice
	{
	private string _awayAction;
	private string _mode;
	private string _outputState;
	//private string _name;

	/// <summary>
	/// Initializes a new instance of the <see cref="WiserSmartPlug"/> class from hub-provided data.
	/// </summary>
	/// <param name="wiserRestController">REST controller used to send commands to the hub.</param>
	/// <param name="data">Raw device data for the underlying device.</param>
	/// <param name="deviceTypeData">Smart plug specific data (mode, state, schedule id).</param>
	/// <param name="schedule">The schedule assigned to this plug (may be null).</param>
	public WiserSmartPlug (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData, WiserSchedule schedule)
		 : base (wiserRestController, data, deviceTypeData)
		{
		Schedule = schedule;
		_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
		_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
		//_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;
		_outputState = deviceTypeData.TryGetValue ("OutputState", out var state) ? state.ToString () : Constants.TEXT_OFF;

		// Add device id to schedule
		if (Schedule != null)
			{
			Schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
			Schedule.DeviceIds.Add (Id);
			}
		}

	private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default) =>
		WiserRestController.SendCommandAsync (
			 RestConstants.WISER_REST_SMART_PLUG.FormatInvariant (Id),
			 cmd,
			 cancellationToken: cancellationToken);

	private static bool ValidateMode (string mode) => AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

	private static bool ValidateAwayAction (string action) =>
		AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// Gets the supported operating modes for a smart plug.
	/// </summary>
	/// <remarks>Values are derived from <see cref="WiserSmartPlugMode"/> (e.g., Auto, Manual).</remarks>
	public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserSmartPlugMode))
		 .Cast<WiserSmartPlugMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>
	/// Gets the supported Away mode actions for smart plugs.
	/// </summary>
	/// <remarks>Permitted values are <see cref="WiserAwayAction.Off"/> and <see cref="WiserAwayAction.NoChange"/>.</remarks>
	public static List<string> AvailableAwayModeActions => [.. Enum.GetValues (typeof (WiserAwayAction))
		 .Cast<WiserAwayAction> ()
		 .Where (a => a is WiserAwayAction.Off or WiserAwayAction.NoChange)
		 .Select (a => a.ToString ())];

	/// <summary>
	/// Asynchronously sets the plug operating mode.
	/// </summary>
	/// <param name="value">Target mode. Must be one of <see cref="AvailableModes"/>.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>True if the hub accepted the change or if the mode was unchanged; otherwise false.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a valid mode.</exception>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetModeAsync (string value, CancellationToken cancellationToken = default)
		{
		if (_mode == value)
			return true; // No change needed
		if (!ValidateMode (value))
			throw new ArgumentException ($"{value} is not a valid Smart Plug mode. Valid modes are {string.Join (", ", AvailableModes)}");
		if (await SendCommandAsync (new { Mode = value }, cancellationToken: cancellationToken).ConfigureAwait (false))
			{
			_mode = value;
			return true;
			}

		return false;
		}

	/*
	public async Task<bool> SetNameAsync (string value, CancellationToken cancellationToken = default)
		{
		if (_name == value)
			return true; // No change needed
		if (value == null)
			throw new ArgumentNullException (nameof (value), "Name cannot be null.");
		if (string.IsNullOrWhiteSpace (value))
			throw new ArgumentException ("Name cannot be empty or whitespace.", nameof (value));
		if (value.Length > 50)
			throw new ArgumentException ("Name cannot exceed 50 characters.", nameof (value));
		// Check if the name is already set to the desired value
		if (await SendCommandAsync (new { Name = value }, cancellationToken: cancellationToken).ConfigureAwait (false))
			{
			_name = value;
			return true;
			}

		return false;
		}
	*/

	/// <summary>
	/// Asynchronously sets the Away mode action.
	/// </summary>
	/// <param name="value">Desired action. Must be one of <see cref="AvailableAwayModeActions"/>.</param>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>True if the hub accepted the change; otherwise false.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a permitted action.</exception>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> SetAwayModeActionAsync (string value, CancellationToken cancellationToken = default)
		{
		if (!ValidateAwayAction (value))
			throw new ArgumentException ($"{value} is not a valid Smart Plug away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
		if (await SendCommandAsync (new { AwayAction = value }, cancellationToken: cancellationToken).ConfigureAwait (false))
			{
			_awayAction = value;
			return true;
			}

		return false;
		}

	/// <summary>
	/// Gets or sets the Away mode action.
	/// </summary>
	/// <value>One of <see cref="AvailableAwayModeActions"/>.</value>
	/// <exception cref="ArgumentException">Setter throws if the value is invalid for the hub.</exception>
	public string AwayModeAction
		{
		get => _awayAction;
		set
			{
			if (_awayAction == value)
				return; // No change needed
			_ = SetAwayModeActionAsync (value).GetAwaiter ().GetResult ();
			}
		}

	/// <summary>
	/// Gets the control source description reported by the hub (e.g., Auto, Manual, Schedule).
	/// </summary>
	public string ControlSource => DeviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets the cumulative delivered energy value, if the device reports it.
	/// </summary>
	/// <value>Delivered power in the hub’s reported unit; -1 if unsupported.</value>
	public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? ConvertInvariant.ToInt32 (power) : -1;

	/// <summary>
	/// Gets the instantaneous power value, if the device reports it.
	/// </summary>
	/// <value>Instantaneous demand in the hub’s reported unit; -1 if unsupported.</value>
	public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? ConvertInvariant.ToInt32 (power) : -1;

	/// <summary>
	/// Gets the manual state string when the plug is under manual control.
	/// </summary>
	public string ManualState => DeviceTypeData.TryGetValue ("ManualState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Gets or sets the plug operating mode.
	/// </summary>
	/// <value>One of <see cref="AvailableModes"/>.</value>
	/// <exception cref="ArgumentException">Setter throws if the value is invalid.</exception>
	public string Mode
		{
		get => _mode;
		set
			{
			if (_mode == value)
				return; // No change needed
			_ = SetModeAsync (value).GetAwaiter ().GetResult ();
			}
		}

	/*
	override public string Name
		{
		get => _name;
		set
			{
			if (_name == value)
				return;
			if (value == null)
				throw new ArgumentNullException (nameof (value), "Name cannot be null.");
			if (string.IsNullOrWhiteSpace (value))
				throw new ArgumentException ("Name cannot be empty or whitespace.", nameof (value));
			if (value.Length > 50)
				throw new ArgumentException ("Name cannot exceed 50 characters.", nameof (value));
			_ = SetNameAsync (value).GetAwaiter ().GetResult ();
			}
		}
	*/

	/// <summary>
	/// Gets whether the plug output is currently on.
	/// </summary>
	public bool IsOn => _outputState == Constants.TEXT_ON;

	/// <summary>
	/// Gets the schedule assigned to this plug, if any.
	/// </summary>
	public WiserSchedule? Schedule { get; }

	/// <summary>
	/// Gets the schedule identifier associated with this plug, if any.
	/// </summary>
	public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>
	/// Gets the current scheduled state reported by the hub.
	/// </summary>
	public string ScheduledState => DeviceTypeData.TryGetValue ("ScheduledState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>
	/// Turns the plug on.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>True if the hub accepted the request or it was already on; otherwise false.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> TurnOnAsync (CancellationToken cancellationToken = default)
		{
		if (_outputState == Constants.TEXT_ON)
			return true; // No change needed
		var result = await SendCommandAsync (new
			{
			RequestOutput = Constants.TEXT_ON
			}, cancellationToken: cancellationToken).ConfigureAwait (false);
		if (result)
			{
			_outputState = Constants.TEXT_ON;
			}

		return result;
		}

	/// <summary>
	/// Turns the plug off.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token to observe.</param>
	/// <returns>True if the hub accepted the request or it was already off; otherwise false.</returns>
	/// <exception cref="WiserHubAuthenticationException">Authentication failed at the hub.</exception>
	/// <exception cref="WiserHubConnectionException">A connection or timeout error occurred.</exception>
	/// <exception cref="WiserHubRESTException">The hub returned a non-success status.</exception>
	public async Task<bool> TurnOffAsync (CancellationToken cancellationToken = default)
		{
		if (_outputState == Constants.TEXT_OFF)
			return true; // No change needed
		var result = await SendCommandAsync (new
			{
			RequestOutput = Constants.TEXT_OFF
			}, cancellationToken: cancellationToken).ConfigureAwait (false);
		if (result)
			{
			_outputState = Constants.TEXT_OFF;
			}

		return result;
		}
	}

/// <summary>
/// Collection helper for smart plugs, providing lookup and counts.
/// </summary>
public class WiserSmartPlugs
	{
	/// <summary>Gets all smart plugs.</summary>
	public List<WiserSmartPlug> All { get; } = [];

	/// <summary>Gets supported smart plug modes.</summary>
	/// <remarks>Values are derived from <see cref="WiserSmartPlugMode"/>.</remarks>
	public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserSmartPlugMode))
		 .Cast<WiserSmartPlugMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>Gets the number of smart plugs.</summary>
	public int Count => All.Count;

	/// <summary>
	/// Finds a smart plug by its device identifier.
	/// </summary>
	/// <param name="id">Device id to search for.</param>
	/// <returns>The matching <see cref="WiserSmartPlug"/>, or null if none found.</returns>
	public WiserSmartPlug GetById (int id) => All.FirstOrDefault (plug => plug.Id == id);
	}

