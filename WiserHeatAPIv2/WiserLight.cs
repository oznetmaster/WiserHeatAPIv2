// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if LIGHT
	public class WiserLight : WiserElectricalLevelDevice
		{
		private readonly WiserSchedule? _schedule1;
		private string _awayAction;
		private string _mode;
		private string _currentState;

		public WiserLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_schedule1 = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TextUnknown;
			_currentState = deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : Constants.TextOff;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextUnknown;
			base.Name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

			// Add device id to schedule
			if (_schedule1 != null)
				{
				_schedule1.Assignments.Add (new Dictionary<string, object> { { "id", LightId }, { "name", Name } });
				_schedule1.DeviceIds.Add (Id);
				}
			}

		protected void SendCommand (object cmd)
			{
			SendCommandAsync (cmd).GetAwaiter ().GetResult ();
			}

		protected async Task<bool> SendCommandAsync (object cmd)
			{
			string url = string.Format (System.Globalization.CultureInfo.InvariantCulture, RestConstants.WiserLight, LightId);

			bool result = await WiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		protected static bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		protected static bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserLightMode))
			 .Cast<WiserLightMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public static List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayAction))
			 .Cast<WiserAwayAction> ()
			 .Where (a => a == WiserAwayAction.Off || a == WiserAwayAction.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

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

		override public string Name
			{
			get => base.Name;
			set
				{
				// Check if the name is already set to the desired value
				if (base.Name == value)
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
					base.Name = value;
					}
				}
			}

		public WiserSchedule? Schedule => Schedule1;

		public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public int TargetState => DeviceTypeData.TryGetValue ("TargetState", out var state) ? Convert.ToInt32 (state, CultureInfo.InvariantCulture) : 0;

		protected WiserSchedule? Schedule1 => _schedule1;

		protected string AwayAction { get => _awayAction; set => _awayAction = value; }

		public async Task<bool> TurnOnAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TextOn } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TextOn;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TextOff } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TextOff;
				}
			return result;
			}
		}

	public class WiserDimmableLight : WiserLight
		{
		public class WiserOutputRange
			{
			private readonly Dictionary<string, object>? _data;

			public WiserOutputRange (Dictionary<string, object>? data)
				{
				_data = data;
				}

			public int? Minimum => _data?.TryGetValue ("Minimum", out var min) == true ? (int?)Convert.ToInt32 (min, CultureInfo.InvariantCulture) : null;

			public int? Maximum => _data?.TryGetValue ("Maximum", out var max) == true ? (int?)Convert.ToInt32 (max, CultureInfo.InvariantCulture) : null;
			}

		public WiserDimmableLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData, schedule)
			{
			}

		public int CurrentLevel => DeviceTypeData.TryGetValue ("CurrentLevel", out var level) ? Convert.ToInt32 (level, CultureInfo.InvariantCulture) : 0;

		public int CurrentPercentage
			{
			get => DeviceTypeData.TryGetValue ("CurrentPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;
			set
				{
				if (value >= 0 && value <= 100)
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

		public WiserOutputRange OutputRange
			{
			get
				{
				if (DeviceTypeData.TryGetValue ("OutputRange", out var range) && range is Dictionary<string, object> rangeDict)
					{
					return new WiserOutputRange (rangeDict);
					}
				return new WiserOutputRange (null);
				}
			}

		public int ScheduledPercentage => Data.TryGetValue ("ScheduledPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;

		public int TargetPercentage => DeviceTypeData.TryGetValue ("TargetPercentage", out var percentage) ? Convert.ToInt32 (percentage, CultureInfo.InvariantCulture) : 0;
		}

	public class WiserLights
		{
		private readonly List<WiserLight> _lights = new List<WiserLight> ();

		public List<WiserLight> All => _lights;

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserLightMode))
			 .Cast<WiserLightMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _lights.Count;

		public List<WiserDimmableLight> DimmableLights => _lights.OfType<WiserDimmableLight> ().ToList ();

		public List<WiserLight> OnOffLights => _lights.Where (light => !light.IsDimmable).ToList ();

		public WiserLight GetById (int id)
			{
			return _lights.FirstOrDefault (light => light.Id == id);
			}

		public WiserLight GetByLightId (int lightId)
			{
			return _lights.FirstOrDefault (light => light.LightId == lightId);
			}

		public List<WiserLight> GetByRoomId (int roomId)
			{
			return _lights.Where (light => light.RoomId == roomId).ToList ();
			}
		}
#endif

	}
