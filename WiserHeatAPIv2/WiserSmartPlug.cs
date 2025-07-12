// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace WiserHeatApiV2
	{
	public class WiserSmartPlug : WiserDevice
		{
		private readonly WiserSchedule _schedule;
		private string _awayAction;
		private string _mode;
		private string _outputState;

		public WiserSmartPlug (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_outputState = deviceTypeData.TryGetValue ("OutputState", out var state) ? state.ToString () : Constants.TEXT_OFF;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default)
			{
			return _wiserRestController.SendCommandAsync (string.Format (RestConstants.WISERSMARTPLUG, Id), cmd, cancellationToken: cancellationToken);
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
		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (_awayAction == value)
					return; // No change needed
				SetAwayModeActionAsync (value).GetAwaiter ().GetResult ();
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : -1;

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : -1;

		public string ManualState => _deviceTypeData.TryGetValue ("ManualState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public string Mode
			{
			get => _mode;
			set
				{
				if (_mode == value)
					return; // No change needed
				SetModeAsync (value).GetAwaiter ().GetResult ();
				}
			}

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
				SetNameAsync (value).GetAwaiter ().GetResult ();
				}
			}

		public bool IsOn => _outputState == Constants.TEXT_ON;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public string ScheduledState => _deviceTypeData.TryGetValue ("ScheduledState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public async Task<bool> TurnOnAsync (CancellationToken cancellationToken = default)
			{
			if (_outputState == Constants.TEXT_ON)
				return true; // No change needed
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TEXT_ON
				}, cancellationToken: cancellationToken).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TEXT_ON;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync (CancellationToken cancellationToken = default)
			{
			if (_outputState == Constants.TEXT_OFF)
				return true; // No change needed
			bool result = await SendCommandAsync (new
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
