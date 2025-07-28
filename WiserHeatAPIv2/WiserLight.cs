// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if LIGHT
	public class WiserLight : WiserElectricalLevelDevice
		{
		private string _mode;
		private string _currentState;
		//private string _name;

		public WiserLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			Schedule1 = schedule;
			AwayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TextUnknown;
			_currentState = deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : Constants.TextOff;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextUnknown;
			//_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

			// Add device id to schedule
			if (Schedule1 != null)
				{
				Schedule1.Assignments.Add (new Dictionary<string, object> { { "id", LightId }, { "name", Name } });
				Schedule1.DeviceIds.Add (Id);
				}
			}

		protected void SendCommand (object cmd) => _ = SendCommandAsync (cmd).GetAwaiter ().GetResult ();

		protected async Task<bool> SendCommandAsync (object cmd)
			{
			var url = string.Format (System.Globalization.CultureInfo.InvariantCulture, RestConstants.WiserLight, LightId);

			var result = await WiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		protected static bool ValidateMode (string mode) =>
			AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

		protected static bool ValidateAwayAction (string action) =>
			AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));

		public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserLightMode))
			 .Cast<WiserLightMode> ()
			 .Select (m => m.ToString ())];

		public static List<string> AvailableAwayModeActions => [.. Enum.GetValues (typeof (WiserAwayAction))
			 .Cast<WiserAwayAction> ()
			 .Where (a => a is WiserAwayAction.Off or WiserAwayAction.NoChange)
			 .Select (a => a.ToString ())];

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

		public string ControlSource => DeviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TextUnknown;

		public string CurrentState => DeviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : "0";

		public bool IsDimmable => DeviceTypeData.TryGetValue ("IsDimmable", out var dimmable) && Convert.ToBoolean (dimmable, CultureInfo.InvariantCulture);

		public bool IsOn => _currentState == Constants.TextOn;

		public int LightId => DeviceTypeId;

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

		public WiserSchedule? Schedule => Schedule1;

		public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public int TargetState => DeviceTypeData.TryGetValue ("TargetState", out var state) ? Convert.ToInt32 (state, CultureInfo.InvariantCulture) : 0;

		protected WiserSchedule? Schedule1 { get; }

		protected string AwayAction { get; set; }

		public async Task<bool> TurnOnAsync ()
			{
			var result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TextOn } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TextOn;
				}

			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			var result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TextOff } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TextOff;
				}

			return result;
			}
		}

	public class WiserDimmableLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule) : WiserLight(wiserRestController, data, deviceTypeData, schedule)
		{
		public class WiserOutputRange (Dictionary<string, object>? data)
			{
			public int? Minimum => data?.TryGetValue ("Minimum", out var min) == true ? (int?)Convert.ToInt32 (min, CultureInfo.InvariantCulture) : null;

			public int? Maximum => data?.TryGetValue ("Maximum", out var max) == true ? (int?)Convert.ToInt32 (max, CultureInfo.InvariantCulture) : null;
			}

		public int CurrentLevel => DeviceTypeData.TryGetValue ("CurrentLevel", out var level) ? Convert.ToInt32 (level, CultureInfo.InvariantCulture) : 0;

		public int CurrentPercentage
			{
			get => DeviceTypeData.TryGetValue ("CurrentPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;
			set
				{
				if (value is >= 0 and <= 100)
					{
					SendCommand (new { RequestOverride = new { State = Constants.TextOn, Percentage = value } });
					}
				else
					{
					throw new ArgumentException ("Brightness level percentage must be between 0 and 100");
					}
				}
			}

		public int ManualLevel => DeviceTypeData.TryGetValue ("ManualLevel", out var level) ? Convert.ToInt32 (level, CultureInfo.InvariantCulture) : 0;

		public int OverrideLevel => DeviceTypeData.TryGetValue ("OverrideLevel", out var level) ? Convert.ToInt32 (level, CultureInfo.InvariantCulture) : 0;

		public WiserOutputRange OutputRange => DeviceTypeData.TryGetValue ("OutputRange", out var range) && range is Dictionary<string, object> rangeDict
					? new WiserOutputRange (rangeDict)
					: new WiserOutputRange (null);

		public int ScheduledPercentage => Data.TryGetValue ("ScheduledPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;

		public int TargetPercentage => DeviceTypeData.TryGetValue ("TargetPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;
		}

	public class WiserLights
		{
		public List<WiserLight> All { get; } = [];

		public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserLightMode))
			 .Cast<WiserLightMode> ()
			 .Select (m => m.ToString ())];

		public int Count => All.Count;

		public List<WiserDimmableLight> DimmableLights => [.. All.OfType<WiserDimmableLight> ()];

		public List<WiserLight> OnOffLights => [.. All.Where (light => !light.IsDimmable)];

		public WiserLight GetById (int id) => All.FirstOrDefault (light => light.Id == id);

		public WiserLight GetByLightId (int lightId) => All.FirstOrDefault (light => light.LightId == lightId);

		public List<WiserLight> GetByRoomId (int roomId) => [.. All.Where (light => light.RoomId == roomId)];
			}
		}
#endif
	}
