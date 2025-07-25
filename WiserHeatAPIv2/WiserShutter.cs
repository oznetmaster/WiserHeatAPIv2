// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if SHUTTER
	public class WiserShutter : WiserElectricalLevelDevice
		{
		public class WiserLiftMovementRange
			{
			private readonly WiserShutter _shutterInstance;
			private readonly Dictionary<string, object>? _data;

			public WiserLiftMovementRange (WiserShutter shutterInstance, Dictionary<string, object>? data)
				{
				_shutterInstance = shutterInstance;
				_data = data;
				}

			public int? OpenTime => _data?.TryGetValue ("LiftOpenTime", out var time) == true ? (int?)Convert.ToInt32 (time, CultureInfo.InvariantCulture) : null;

			public int? CloseTime => _data?.TryGetValue ("LiftCloseTime", out var time) == true ? (int?)Convert.ToInt32 (time, CultureInfo.InvariantCulture) : null;

			public async Task SetOpenTimeAsync (int time, CancellationToken cancellationToken = default)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = time, LiftCloseTime = CloseTime }, cancellationToken: cancellationToken).ConfigureAwait (false);
				}

			public async Task SetCloseTimeAsync (int time, CancellationToken cancellationToken = default)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = OpenTime, LiftCloseTime = time }, cancellationToken: cancellationToken).ConfigureAwait (false);
				}
			}

		private readonly WiserSchedule? _schedule;
		private string _awayAction;
		private string _mode;

		public WiserShutter (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (wiserRestController, data, deviceTypeData)
			{
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TextUnknown;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextUnknown;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", ShutterId }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default)
			{
			string url = string.Format (CultureInfo.InvariantCulture, RestConstants.WiserShutter, ShutterId);

			return WiserRestController.SendCommandAsync (url, cmd, cancellationToken: cancellationToken);
			}

		private static bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		private static bool ValidateAwayAction (string action)
			{
			return AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));
			}

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterMode))
			 .Cast<WiserShutterMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public static List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayAction))
			 .Cast<WiserAwayAction> ()
			 .Where (a => a == WiserAwayAction.Close || a == WiserAwayAction.NoChange)
			 .Select (a => a.ToString ())
			 .ToList ();

		public string AwayModeAction
			{
			get => _awayAction;
			set
				{
				if (value == _awayAction)
					return; // No change needed
				if (ValidateAwayAction (value))
					{
					if (SendCommandAsync (new { AwayAction = value }).Result)
						{
						_awayAction = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Shutter away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => DeviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TextUnknown;

		public int CurrentLift => DeviceTypeData.TryGetValue ("CurrentLift", out var lift) ? Convert.ToInt32 (lift, CultureInfo.InvariantCulture) : 0;

		public async Task SetCurrentLiftAsync (int percentage, CancellationToken cancellationToken = default)
			{
			if (percentage >= 0 && percentage <= 100)
				{
				await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = percentage } }, cancellationToken: cancellationToken).ConfigureAwait (false);
				}
			else
				{
				throw new ArgumentException ("Shutter percentage must be between 0 and 100");
				}
			}

		public WiserLiftMovementRange DriveConfig
			{
			get
				{
				if (DeviceTypeData.TryGetValue ("DriveConfig", out var config) && config is Dictionary<string, object> configDict)
					{
					return new WiserLiftMovementRange (this, configDict);
					}
				return new WiserLiftMovementRange (this, null);
				}
			}

		public bool IsOpen => CurrentLift == 100;

		public bool IsClosed => CurrentLift == 0;

		public bool IsClosing => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Closing";

		public bool IsOpening => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Opening";

		public bool IsStopped => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Stopped";

		public bool IsMoving => !IsStopped;

		public string LiftMovement => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) ? movement.ToString () : Constants.TextUnknown;

		public int ManualLift => DeviceTypeData.TryGetValue ("ManualLift", out var lift) ? Convert.ToInt32 (lift, CultureInfo.InvariantCulture) : 0;

		public string Mode
			{
			get => _mode;
			set
				{
				if (ValidateMode (value))
					{
					if (SendCommandAsync (new { Mode = value }).Result)
						{
						_mode = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Shutter mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		override public string Name
			{
			get => base.Name;
			set
				{
				if (value == base.Name)
					return;
				if (string.IsNullOrWhiteSpace (value))
					{
					throw new ArgumentException ("Name cannot be null or empty.");
					}
				if (value.Length > 50)
					{
					throw new ArgumentException ("Name cannot exceed 50 characters.");
					}
				if (SendCommandAsync (new { Name = value }).Result)
					{
					base.Name = value;
					}
				}
			}

		public WiserSchedule? Schedule => _schedule;

		public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public string ScheduledLift => DeviceTypeData.TryGetValue ("ScheduledLift", out var lift) ? lift.ToString () : Constants.TextUnknown;

		public int ShutterId => DeviceTypeData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public int TargetLift => DeviceTypeData.TryGetValue ("TargetLift", out var lift) ? Convert.ToInt32 (lift, CultureInfo.InvariantCulture) : 0;

		public Task OpenAsync (CancellationToken cancellationToken = default)
			{
			return SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 100 } }, cancellationToken: cancellationToken);
			}

		public Task CloseAsync (CancellationToken cancellationToken = default)
			{
			return SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 0 } }, cancellationToken: cancellationToken);
			}

		public Task StopAsync (CancellationToken cancellationToken = default)
			{
			return SendCommandAsync (new { RequestAction = new { Action = "Stop" } }, cancellationToken: cancellationToken);
			}
		}

	public class WiserShutters
		{
		private readonly List<WiserShutter> _shutters = new List<WiserShutter> ();

		public List<WiserShutter> All => _shutters;

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterMode))
			 .Cast<WiserShutterMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public int Count => _shutters.Count;

		public WiserShutter GetById (int id)
			{
			return _shutters.FirstOrDefault (shutter => shutter.Id == id);
			}

		public WiserShutter GetByShutterId (int shutterId)
			{
			return _shutters.FirstOrDefault (shutter => shutter.ShutterId == shutterId);
			}

		public List<WiserShutter> GetByRoomId (int roomId)
			{
			return _shutters.Where (shutter => shutter.RoomId == roomId).ToList ();
			}
		}
#endif
	}
