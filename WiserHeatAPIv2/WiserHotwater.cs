// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Threading.Tasks;

	public class WiserHotwater
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _data;
		private WiserSchedule _schedule;
		private string _mode;

		public WiserHotwater (WiserRestController wiserRestController, Dictionary<string, object> hwData, WiserSchedule schedule)
			{
			_wiserRestController = wiserRestController;
			_data = hwData;
			_schedule = schedule;
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_AUTO;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				}
			}

		public void Update (Dictionary<string, object> hwData, WiserSchedule schedule)
			{
			var oldId = Id;
			var oldName = Name;

			if (hwData != null)
				{
				_data.Clear ();
				foreach (var kv in hwData)
					_data[kv.Key] = kv.Value;
				}

			_schedule = schedule; // Uncomment if you want to update the schedule reference
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TEXT_AUTO;
			if (_schedule != null)
				{
				if (oldId != Id || oldName != Name)
					{
					// Remove old assignment if the id or name has changed
					_schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
					_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
					}
				}

			}
		private async Task<bool> SendCommandAsync (object cmd)
			{
			string url = string.Format (RestConstants.WISERHOTWATER, Id);
			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		private bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		public List<string> AvailableModes => Enum.GetValues (typeof (WiserHotWaterModeEnum))
			 .Cast<WiserHotWaterModeEnum> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && Convert.ToBoolean (suppressed);

		public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && Convert.ToInt32 (time) > 0
			 ? DateTimeOffset.FromUnixTimeSeconds (Convert.ToInt32 (time)).DateTime
			 : DateTime.MinValue;

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTime - DateTime.Now).TotalSeconds
			 : 0;

		public string CurrentControlSource => _data.TryGetValue ("HotWaterDescription", out var source) ? source.ToString () : Constants.TEXT_UNKNOWN;

		public string CurrentState => _data.TryGetValue ("HotWaterRelayState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0;

		public bool IsAwayMode => CurrentControlSource == "FromAwayMode";

		public bool IsBoost => CurrentControlSource?.Contains ("Boost") ?? false;

		public bool IsHeating => _data.TryGetValue ("WaterHeatingState", out var state) && state.ToString () == Constants.TEXT_ON;

		public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
										 type.ToString () != Constants.TEXT_UNKNOWN &&
										 type.ToString () != Constants.TEXT_NONE;

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
						_mode = value;
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Hot Water mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		public string Name => "HotWater";

		public string ProductType => "HotWater";

		public WiserSchedule Schedule => _schedule;

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0;

		public async Task<bool> BoostAsync (int duration)
			{
			return await OverrideStateForDurationAsync (Constants.TEXT_ON, duration).ConfigureAwait (false);
			}

		public async Task<bool> CancelBoostAsync ()
			{
			if (IsBoost)
				{
				return await CancelOverridesAsync ().ConfigureAwait (false);
				}
			return true;
			}

		public async Task<bool> CancelOverridesAsync ()
			{
			return await SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TEXT_NONE
					}
				}).ConfigureAwait (false);
			}

		public async Task<bool> OverrideStateAsync (string state)
			{
			if (await CancelBoostAsync ())
				{
				if (state.Equals (Constants.TEXT_ON, StringComparison.OrdinalIgnoreCase))
					{
					return await SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TEXT_MANUAL,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_ON, "hotwater")
							}
						}).ConfigureAwait (false);
					}
				else if (state.Equals (Constants.TEXT_OFF, StringComparison.OrdinalIgnoreCase))
					{
					return await SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TEXT_MANUAL,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_OFF, "hotwater")
							}
						}).ConfigureAwait (false);
					}
				else
					{
					throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TEXT_ON} or {Constants.TEXT_OFF}");
					}
				}
			return false;
			}

		public async Task<bool> OverrideStateForDurationAsync (string state, int duration)
			{
			if (state.Equals (Constants.TEXT_ON, StringComparison.OrdinalIgnoreCase))
				{
				return await SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TEXT_MANUAL,
						DurationMinutes = duration,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_ON, "hotwater")
						}
					}).ConfigureAwait (false);
				}
			else if (state.Equals (Constants.TEXT_OFF, StringComparison.OrdinalIgnoreCase))
				{
				return await SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TEXT_MANUAL,
						DurationMinutes = duration,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TEMP_HW_OFF)
						}
					}).ConfigureAwait (false);
				}
			else
				{
				throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TEXT_ON} or {Constants.TEXT_OFF}");
				}
			}

		public async Task<bool> ScheduleAdvanceAsync ()
			{
			if (Schedule != null)
				{
				if (await CancelBoostAsync ().ConfigureAwait (false))
					{
					return await OverrideStateAsync (Schedule.Next.Setting.ToString ()).ConfigureAwait (false);
					}
				}
			return false;
			}
		}
	}
