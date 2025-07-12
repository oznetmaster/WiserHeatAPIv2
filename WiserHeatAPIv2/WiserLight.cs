// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if LIGHT
	public class WiserLight : WiserElectricalLevelDevice
		{
		protected readonly WiserSchedule _schedule;
		protected string _awayAction;
		protected string _mode;
		protected string _currentState;

		public WiserLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_currentState = deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : Constants.TEXT_OFF;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", LightId }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		protected void SendCommand (object cmd)
			{
			SendCommandAsync (cmd).GetAwaiter ().GetResult ();
			}

		protected async Task<bool> SendCommandAsync (object cmd)
			{
			string url = string.Format (RestConstants.WISERLIGHT, LightId);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		protected bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		protected bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserLightModeEnum))
			 .Cast<WiserLightModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayActionEnum))
			 .Cast<WiserAwayActionEnum> ()
			 .Where (a => a == WiserAwayActionEnum.Off || a == WiserAwayActionEnum.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (ValidateAwayAction (value))
					{
					if (SendCommandAsync (new { AwayAction = value }).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Light away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public string CurrentState => _deviceTypeData.TryGetValue ("CurrentState", out var state) ? state.ToString () : "0";

		public bool IsDimmable => _deviceTypeData.TryGetValue ("IsDimmable", out var dimmable) && Convert.ToBoolean (dimmable);

		public bool IsOn => _currentState == Constants.TEXT_ON;

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

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public int TargetState => _deviceTypeData.TryGetValue ("TargetState", out var state) ? Convert.ToInt32 (state) : 0;

		public async Task<bool> TurnOnAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_ON } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TEXT_ON;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			bool result = await SendCommandAsync (new { RequestOverride = new { State = Constants.TEXT_OFF } }).ConfigureAwait (false);
			if (result)
				{
				_currentState = Constants.TEXT_OFF;
				}
			return result;
			}
		}

	public class WiserDimmableLight : WiserLight
		{
		public class WiserOutputRange
			{
			private readonly Dictionary<string, object> _data;

			public WiserOutputRange (Dictionary<string, object> data)
				{
				_data = data;
				}

			public int? Minimum => _data?.TryGetValue ("Minimum", out var min) == true ? (int?)Convert.ToInt32 (min) : null;

			public int? Maximum => _data?.TryGetValue ("Maximum", out var max) == true ? (int?)Convert.ToInt32 (max) : null;
			}

		public WiserDimmableLight (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData, schedule)
			{
			}

		public int CurrentLevel => _deviceTypeData.TryGetValue ("CurrentLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public int CurrentPercentage
			{
			get => _deviceTypeData.TryGetValue ("CurrentPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;
			set
				{
				if (value >= 0 && value <= 100)
					{
					SendCommand (new { RequestOverride = new { State = Constants.TEXT_ON, Percentage = value } });
					}
				else
					{
					throw new ArgumentException ("Brightness level percentage must be between 0 and 100");
					}
				}
			}

		public int ManualLevel => _deviceTypeData.TryGetValue ("ManualLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public int OverrideLevel => _deviceTypeData.TryGetValue ("OverrideLevel", out var level) ? Convert.ToInt32 (level) : 0;

		public WiserOutputRange OutputRange
			{
			get
				{
				if (_deviceTypeData.TryGetValue ("OutputRange", out var range) && range is Dictionary<string, object> rangeDict)
					{
					return new WiserOutputRange (rangeDict);
					}
				return new WiserOutputRange (null);
				}
			}

		public int ScheduledPercentage => _data.TryGetValue ("ScheduledPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;

		public int TargetPercentage => _deviceTypeData.TryGetValue ("TargetPercentage", out var percentage) ? Convert.ToInt32 (percentage) : 0;
		}

	public class WiserLightCollection
		{
		private readonly List<WiserLight> _lights = new List<WiserLight> ();

		public List<WiserLight> All => _lights;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserLightModeEnum))
			 .Cast<WiserLightModeEnum> ()
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
