// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;


namespace WiserHeatApiV2
	{
	public class WiserSmartPlug : WiserDevice
		{
		private readonly WiserSchedule? _schedule;
		private string _awayAction;
		private string _mode;
		private string _outputState;

		public WiserSmartPlug (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TextUnknown;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextUnknown;
			base.Name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;
			_outputState = deviceTypeData.TryGetValue ("OutputState", out var state) ? state.ToString () : Constants.TextOff;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default)
			{
			return WiserRestController.SendCommandAsync (
				 string.Format (System.Globalization.CultureInfo.InvariantCulture, RestConstants.WiserSmartPlug, Id),
				 cmd,
				 cancellationToken: cancellationToken
			);
			}

		private static bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		private static bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugMode))
			 .Cast<WiserSmartPlugMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public static List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayAction))
			 .Cast<WiserAwayAction> ()
			 .Where (a => a == WiserAwayAction.Off || a == WiserAwayAction.NoChange)
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
			if (Name == value)
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
				Name = value;
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

		public string ControlSource => DeviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TextUnknown;

		public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power, CultureInfo.InvariantCulture) : -1;

		public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power, CultureInfo.InvariantCulture) : -1;

		public string ManualState => DeviceTypeData.TryGetValue ("ManualState", out var state) ? state.ToString () : Constants.TextUnknown;

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
			get => base.Name;
			set
				{
				if (base.Name == value)
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

		public bool IsOn => _outputState == Constants.TextOn;

		public WiserSchedule? Schedule => _schedule;

		public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string ScheduledState => DeviceTypeData.TryGetValue ("ScheduledState", out var state) ? state.ToString () : Constants.TextUnknown;

		public async Task<bool> TurnOnAsync (CancellationToken cancellationToken = default)
			{
			if (_outputState == Constants.TextOn)
				return true; // No change needed
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TextOn
				}, cancellationToken: cancellationToken).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TextOn;
				}
			return result;
			}

		public async Task<bool> TurnOffAsync (CancellationToken cancellationToken = default)
			{
			if (_outputState == Constants.TextOff)
				return true; // No change needed
			bool result = await SendCommandAsync (new
				{
				RequestOutput = Constants.TextOff
				}, cancellationToken: cancellationToken).ConfigureAwait (false);
			if (result)
				{
				_outputState = Constants.TextOff;
				}
			return result;
			}
		}

	public class WiserSmartPlugs
		{
		private readonly List<WiserSmartPlug> _smartPlugs = new List<WiserSmartPlug> ();

		public List<WiserSmartPlug> All => _smartPlugs;

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserSmartPlugMode))
			 .Cast<WiserSmartPlugMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _smartPlugs.Count;

		public WiserSmartPlug GetById (int id)
			{
			return _smartPlugs.FirstOrDefault (plug => plug.Id == id);
			}
		}
	}
