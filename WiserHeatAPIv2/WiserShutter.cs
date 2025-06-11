// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if SHUTTER
	public class WiserShutter : WiserElectricalLevelDevice
		{
		public class WiserLiftMovementRange
			{
			private readonly WiserShutter _shutterInstance;
			private readonly Dictionary<string, object> _data;

			public WiserLiftMovementRange (WiserShutter shutterInstance, Dictionary<string, object> data)
				{
				_shutterInstance = shutterInstance;
				_data = data;
				}

			public int? OpenTime => _data?.TryGetValue ("LiftOpenTime", out var time) == true ? (int?)Convert.ToInt32 (time) : null;

			public int? CloseTime => _data?.TryGetValue ("LiftCloseTime", out var time) == true ? (int?)Convert.ToInt32 (time) : null;

			public async Task SetOpenTimeAsync (int time)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = time, LiftCloseTime = CloseTime }).ConfigureAwait (false);
				}

			public async Task SetCloseTimeAsync (int time)
				{
				await _shutterInstance.SendCommandAsync (new { LiftOpenTime = OpenTime, LiftCloseTime = time }).ConfigureAwait (false);
				}
			}

		private readonly WiserRestController _wiserRestController;
		private readonly WiserSchedule _schedule;
		private string _awayAction;
		private string _mode;
		private string _name;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserShutter (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
			 : base (data, deviceTypeData)
			{
			_wiserRestController = wiserRestController;
			_schedule = schedule;
			_awayAction = deviceTypeData.TryGetValue ("AwayAction", out var action) ? action.ToString () : Constants.TEXT_UNKNOWN;
			_mode = deviceTypeData.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_UNKNOWN;
			_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", ShutterId }, { "name", Name } });
				_schedule.DeviceIds.Add (Id);
				}
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSHUTTER, ShutterId);

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

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterModeEnum))
			 .Cast<WiserShutterModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public List<string> AvailableAwayModeActions => Enum.GetValues (typeof (WiserAwayActionEnum))
			 .Cast<WiserAwayActionEnum> ()
			 .Where (a => a == WiserAwayActionEnum.Close || a == WiserAwayActionEnum.NoChange)
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
					throw new ArgumentException ($"{value} is not a valid Shutter away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
					}
				}
			}

		public string ControlSource => _deviceTypeData.TryGetValue ("ControlSource", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public int CurrentLift => _deviceTypeData.TryGetValue ("CurrentLift", out var lift) ? Convert.ToInt32 (lift) : 0;

		public async Task SetCurrentLiftAsync (int percentage)
			{
			if (percentage >= 0 && percentage <= 100)
				{
				await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = percentage } }).ConfigureAwait (false);
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
				if (_deviceTypeData.TryGetValue ("DriveConfig", out var config) && config is Dictionary<string, object> configDict)
					{
					return new WiserLiftMovementRange (this, configDict);
					}
				return new WiserLiftMovementRange (this, null);
				}
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new { Identify = value }, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public bool IsOpen => CurrentLift == 100;

		public bool IsClosed => CurrentLift == 0;

		public bool IsClosing => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Closing";

		public bool IsOpening => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Opening";

		public bool IsStopped => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Stopped";

		public bool IsMoving => !IsStopped;

		public string LiftMovement => _deviceTypeData.TryGetValue ("LiftMovement", out var movement) ? movement.ToString () : Constants.TEXT_UNKNOWN;

		public int ManualLift => _deviceTypeData.TryGetValue ("ManualLift", out var lift) ? Convert.ToInt32 (lift) : 0;

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

		new public string Name
			{
			get => _name;
			set
				{
				if (SendCommandAsync (new { Name = value }).Result)
					{
					_name = value;
					}
				}
			}

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _deviceTypeData.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public string ScheduledLift => _deviceTypeData.TryGetValue ("ScheduledLift", out var lift) ? lift.ToString () : Constants.TEXT_UNKNOWN;

		public int ShutterId => _deviceTypeData.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public int TargetLift => _deviceTypeData.TryGetValue ("TargetLift", out var lift) ? Convert.ToInt32 (lift) : 0;

		public async Task OpenAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 100 } }).ConfigureAwait (false);
			}

		public async Task CloseAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "LiftTo", Percentage = 0 } }).ConfigureAwait (false);
			}

		public async Task StopAsync ()
			{
			await SendCommandAsync (new { RequestAction = new { Action = "Stop" } }).ConfigureAwait (false);
			}
		}

	public class WiserShutterCollection
		{
		private readonly List<WiserShutter> _shutters = new List<WiserShutter> ();

		public List<WiserShutter> All => _shutters;

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserShutterModeEnum))
			 .Cast<WiserShutterModeEnum> ()
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
