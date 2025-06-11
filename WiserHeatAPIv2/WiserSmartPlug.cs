// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace WiserHeatApiV2
	{
	public class WiserSmartPlug : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private readonly WiserSchedule _schedule;
		private string _awayAction;
		private string _mode;
		private string _name;
		private bool _deviceLockEnabled;
		private string _outputState;
		private bool _identifyActive;

		public WiserSmartPlug (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_outputState = deviceTypeData.TryGetValue ("OutputState", out var state) ? state.ToString () : Constants.TEXT_OFF;
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSMARTPLUG, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		private bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		private bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugModeEnum))
			 .Cast<WiserSmartPlugModeEnum> ()
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
					if (SendCommandAsync (new
						{
						AwayAction = value
						}).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Smart Plug away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : -1;

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : -1;

		public string ManualState => _deviceTypeData.TryGetValue ("ManualState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

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
					throw new ArgumentException ($"{value} is not a valid Smart Plug mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		new public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new
					{
					Name = value
					}).Result)
					{
					_name = value;
					}
				}
			}

		public bool IsOn => _outputState == Constants.TEXT_ON;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public string ScheduledState => _deviceTypeData.TryGetValue ("ScheduledState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public async Task<bool> TurnOnAsync ()
			{
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TEXT_ON
				}).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TEXT_ON;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync ()
			{
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TEXT_OFF
				}).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TEXT_OFF;
				}
			return result;
			}
		}

	public class WiserSmartPlugCollection
		{
		private readonly List<WiserSmartPlug> _smartPlugs = new List<WiserSmartPlug> ();

		public List<WiserSmartPlug> All => _smartPlugs;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugModeEnum))
			 .Cast<WiserSmartPlugModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _smartPlugs.Count;

		public WiserSmartPlug GetById (int id)
			{
			return _smartPlugs.FirstOrDefault (plug => plug.Id == id);
			}
		}
	}
