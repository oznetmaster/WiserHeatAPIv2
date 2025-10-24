// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using static WiserHeatApiV2.RestConstants;

namespace WiserHeatApiV2;

#if LIGHT
/// <summary>
/// Represents a light device with level control and schedules.
/// </summary>
public class WiserLight : WiserElectricalLevelDevice
	{
	private string _mode;
	private string _currentState;
	//private string _name;

	/// <summary>Initializes a new instance of the <see cref="WiserLight"/> class.</summary>
	public WiserLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
		 : base (wiserRestController, data, deviceTypeData)
		{
		Schedule1 = schedule;
		AwayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
		_currentState = deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : Constants.TEXT_OFF;
		_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
		//_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		// Add device id to schedule
		if (Schedule1 != null)
			{
			Schedule1.Assignments.Add (new Dictionary<string, object> { { "id", LightId }, { "name", Name } });
			Schedule1.DeviceIds.Add (Id);
			}
		}

	/// <summary>Synchronously sends a command to the light.</summary>
	protected void SendCommand (object cmd) => _ = SendCommandAsync (cmd).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a command to the light.</summary>
	protected async Task<bool> SendCommandAsync (object cmd)
		{
		var url = WISER_REST_LIGHT.FormatInvariant (LightId);

		var result = await WiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
		return result;
		}

	/// <summary>Validates a light operating mode value.</summary>
	protected static bool ValidateMode (string mode) =>
		AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

	/// <summary>Validates a light away-mode action value.</summary>
	protected static bool ValidateAwayAction (string action) =>
		AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));

	/// <summary>Gets the list of allowed modes.</summary>
	public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserLightMode))
		 .Cast<WiserLightMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>Gets the list of allowed away-mode actions.</summary>
	public static List<string> AvailableAwayModeActions => [.. Enum.GetValues (typeof (WiserAwayAction))
		 .Cast<WiserAwayAction> ()
		 .Where (a => a is WiserAwayAction.Off or WiserAwayAction.NoChange)
		 .Select (a => a.ToString ())];

	/// <summary>Gets or sets the away-mode action for the light.</summary>
	public string AwayModeAction
		{
		get => AwayAction;
		set
			{
			if (ValidateAwayAction (value))
				{
				if (SendCommandAsync (new { AwayAction = value }).Result)
					{
					AwayAction = value;
					}
				}
			else
				{
				throw new ArgumentException ($"{value} is not a valid Light away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
				}
			}
		}

	/// <summary>Gets the light control source.</summary>
	public string ControlSource => DeviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the current state reported by the hub.</summary>
	public string CurrentState => DeviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : "0";

	/// <summary>Gets a value indicating whether the light supports dimming.</summary>
	public bool IsDimmable => DeviceTypeData.TryGetValue ("IsDimmable", out var dimmable) && ConvertInvariant.ToBoolean (dimmable);

	/// <summary>Gets a value indicating whether the light is currently on.</summary>
	public bool IsOn => _currentState == Constants.TEXT_ON;

	/// <summary>Gets the light device type id.</summary>
	public int LightId => DeviceTypeId;

	/// <summary>Gets or sets the operating mode.</summary>
	public string Mode
		{
		get => _mode;
		set
			{
			// Check if the mode is already set to the desired value
			if (_mode == value)
				{
				return; // No change needed
				}

			if (ValidateMode (value))
				{
				if (SendCommandAsync (new { Mode = value }).Result)
					{
					_mode = value;
					}
				}
			else
				{
				throw new ArgumentException ($"{value} is not a valid Light mode. Valid modes are {string.Join (", ", AvailableModes)}");
				}
			}
		}

	/*
	override public string Name
		{
		get => _name;
		set
			{
			// Check if the name is already set to the desired value
			if (_name == value)
				{
				return; // No change needed
				}
			// Validate name length and content
			if (string.IsNullOrWhiteSpace (value))
				{
				throw new ArgumentException ("Name cannot be null or empty");
				}
			else if (value.Length > 50)
				{
				throw new ArgumentException ("Name cannot exceed 50 characters");
				}
			// Send command to update name
			if (SendCommandAsync (new { Name = value }).Result)
				{
				_name = value;
				}
			}
		}
	*/

	/// <summary>Gets the assigned schedule for the light, if any.</summary>
	public WiserSchedule? Schedule => Schedule1;

	/// <summary>Gets the schedule id assigned to the light.</summary>
	public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the target on/off state (0/1).</summary>
	public int TargetState => DeviceTypeData.TryGetValue ("TargetState", out var state) ? ConvertInvariant.ToInt32 (state) : 0;

	/// <summary>Gets the internal schedule reference.</summary>
	protected WiserSchedule? Schedule1 { get; }

	/// <summary>Gets or sets the away action backing value.</summary>
	protected string AwayAction { get; set; }

	/// <summary>Turns the light on.</summary>
	public async Task<bool> TurnOnAsync ()
		{
		var result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_ON } }).ConfigureAwait (false);
		if (result)
			{
			_currentState = Constants.TEXT_ON;
			}

		return result;
		}

	/// <summary>Turns the light off.</summary>
	public async Task<bool> TurnOffAsync ()
		{
		var result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_OFF } }).ConfigureAwait (false);
		if (result)
			{
			_currentState = Constants.TEXT_OFF;
			}

		return result;
		}
	}

/// <summary>
/// Represents a dimmable light with level control.
/// </summary>
public class WiserDimmableLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule) : WiserLight(wiserRestController, data, deviceTypeData, schedule)
	{
	/// <summary>Represents output range limits for a dimmable light.</summary>
	public class WiserOutputRange (Dictionary<string, object>? data)
		{
		/// <summary>Gets the minimum output value, if provided.</summary>
		public int? Minimum => data?.TryGetValue ("Minimum", out var min) == true ? (int?)ConvertInvariant.ToInt32 (min) : null;

		/// <summary>Gets the maximum output value, if provided.</summary>
		public int? Maximum => data?.TryGetValue ("Maximum", out var max) == true ? (int?)ConvertInvariant.ToInt32 (max) : null;
		}

	/// <summary>Gets the current level value reported by the hub.</summary>
	public int CurrentLevel => DeviceTypeData.TryGetValue ("CurrentLevel", out var level) ? ConvertInvariant.ToInt32 (level) : 0;

	/// <summary>Gets or sets the current brightness percentage (0..100).</summary>
	public int CurrentPercentage
		{
		get => DeviceTypeData.TryGetValue ("CurrentPercentage", out var percentage) ? ConvertInvariant.ToInt32 (percentage) : 0;
		set
			{
			if (value is >= 0 and <= 100)
				{
				SendCommand (new { RequestOverride = new { State = Constants.TEXT_ON, Percentage = value } });
				}
			else
				{
				throw new ArgumentException ("Brightness level percentage must be between 0 and 100");
				}
			}
		}

	/// <summary>Gets the last manual level.</summary>
	public int ManualLevel => DeviceTypeData.TryGetValue ("ManualLevel", out var level) ? ConvertInvariant.ToInt32 (level) : 0;

	/// <summary>Gets the current override level.</summary>
	public int OverrideLevel => DeviceTypeData.TryGetValue ("OverrideLevel", out var level) ? ConvertInvariant.ToInt32 (level) : 0;

	/// <summary>Gets the configured output range for the device.</summary>
	public WiserOutputRange OutputRange => DeviceTypeData.TryGetValue ("OutputRange", out var range) && range is Dictionary<string, object> rangeDict
				? new WiserOutputRange (rangeDict)
				: new WiserOutputRange (null);

	/// <summary>Gets the scheduled brightness percentage.</summary>
	public int ScheduledPercentage => Data.TryGetValue ("ScheduledPercentage", out var percentage) ? ConvertInvariant.ToInt32 (percentage) : 0;

	/// <summary>Gets the target brightness percentage.</summary>
	public int TargetPercentage => DeviceTypeData.TryGetValue ("TargetPercentage", out var percentage) ? ConvertInvariant.ToInt32 (percentage) : 0;
	}

/// <summary>
/// Collection of Light devices with lookup helpers.
/// </summary>
public class WiserLights
	{
	/// <summary>Gets all light devices.</summary>
	public List<WiserLight> All { get; } = [];

	/// <summary>Gets the list of allowed modes.</summary>
	public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserLightMode))
		 .Cast<WiserLightMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>Gets the number of lights.</summary>
	public int Count => All.Count;

	/// <summary>Gets all dimmable lights.</summary>
	public List<WiserDimmableLight> DimmableLights => [.. All.OfType<WiserDimmableLight> ()];

	/// <summary>Gets all on/off-only lights.</summary>
	public List<WiserLight> OnOffLights => [.. All.Where (light => !light.IsDimmable)];

	/// <summary>Finds a light by its device id.</summary>
	public WiserLight GetById (int id) => All.FirstOrDefault (light => light.Id == id);

	/// <summary>Finds a light by its light id.</summary>
	public WiserLight GetByLightId (int lightId) => All.FirstOrDefault (light => light.LightId == lightId);

	/// <summary>Gets all lights assigned to a room id.</summary>
	public List<WiserLight> GetByRoomId (int roomId) => [.. All.Where (light => light.RoomId == roomId)];
		}
#endif
	
