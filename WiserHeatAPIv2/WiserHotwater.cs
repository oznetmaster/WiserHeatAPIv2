// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserHotwater
		{
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, object> _data;
		private WiserSchedule? _schedule;
		private string _mode;

		public WiserHotwater (WiserRestController wiserRestController, IDictionary<string, object> hwData, WiserSchedule? schedule)
			{
			_wiserRestController = wiserRestController;
			_data = hwData;
			_schedule = schedule;
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextAuto;

			// Add device id to schedule
			if (_schedule != null)
				{
				_schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
				}
			}

		public void Update (IDictionary<string, object> hwData, WiserSchedule? schedule)
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
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextAuto;
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
		private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default)
			{
			return _wiserRestController.SendCommandAsync (
				 string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHotWater, Id),
				 cmd,
				 cancellationToken: cancellationToken
			);
			}

		private static bool ValidateMode (string mode)
			{
			return AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));
			}

		public static List<string> AvailableModes => Enum.GetValues (typeof (WiserHotWaterMode))
			 .Cast<WiserHotWaterMode> ()
			 .Select (m => m.ToString ())
			 .ToList ();

		public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && Convert.ToBoolean (suppressed, CultureInfo.InvariantCulture);

		public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && Convert.ToInt32 (time, CultureInfo.InvariantCulture) > 0
			 ? DateTimeOffset.FromUnixTimeSeconds (Convert.ToInt32 (time, CultureInfo.InvariantCulture)).DateTime
			 : DateTime.MinValue;

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTime - DateTime.Now).TotalSeconds
			 : 0;

		public string CurrentControlSource => _data.TryGetValue ("HotWaterDescription", out var source) ? source.ToString () : Constants.TextUnknown;

		public string CurrentState => _data.TryGetValue ("HotWaterRelayState", out var state) ? state.ToString () : Constants.TextUnknown;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public bool IsAwayMode => CurrentControlSource == "FromAwayMode";

		public bool IsBoost => CurrentControlSource?.Contains ("Boost") ?? false;

		public bool IsHeating => _data.TryGetValue ("WaterHeatingState", out var state) && state.ToString () == Constants.TextOn;

		public bool IsOverride => _data.TryGetValue ("OverrideType", out var type) &&
                                             type.ToString () != Constants.TextUnknown &&
                                             type.ToString () != Constants.TextNone;

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

		public static string Name => "HotWater";

		public static string ProductType => "HotWater";

		public WiserSchedule? Schedule => _schedule;

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public Task<bool> BoostAsync (int duration, CancellationToken cancellationToken = default)
			{
			return OverrideStateForDurationAsync (Constants.TextOn, duration, cancellationToken);
			}
		public Task<bool> CancelBoostAsync (CancellationToken cancellationToken = default)
			{
			return IsBoost ? CancelOverridesAsync (cancellationToken) : Task.FromResult (true);
			}

		public Task<bool> CancelOverridesAsync (CancellationToken cancellationToken = default)
			{
			return SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TextNone
					}
				}, cancellationToken);
			}

		public async Task<bool> OverrideStateAsync (string state, CancellationToken cancellationToken = default)
			{
			if (await CancelBoostAsync (cancellationToken).ConfigureAwait (false))
				{
				if (state.Equals (Constants.TextOn, StringComparison.OrdinalIgnoreCase))
					{
					return await SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TextManual,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOn, "hotwater")
							}
						}, cancellationToken).ConfigureAwait (false);
					}
				else if (state.Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase))
					{
					return await SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TextManual,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOff, "hotwater")
							}
						}, cancellationToken).ConfigureAwait (false);
					}
				else
					{
					throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TextOn} or {Constants.TextOff}");
					}
				}
			return false;
			}

		public Task<bool> OverrideStateForDurationAsync (string state, int duration, CancellationToken cancellationToken = default)
			{
			if (state.Equals (Constants.TextOn, StringComparison.OrdinalIgnoreCase))
				{
				return SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TextManual,
						DurationMinutes = duration,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOn, "hotwater")
						}
					}, cancellationToken);
				}
			else if (state.Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase))
				{
				return SendCommandAsync (new
					{
					RequestOverride = new
						{
						Type = Constants.TextManual,
						DurationMinutes = duration,
						SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOff)
						}
					}, cancellationToken);
				}
			else
				{
				throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TextOn} or {Constants.TextOff}");
				}
			}

		public async Task<bool> ScheduleAdvanceAsync (CancellationToken cancellationToken = default)
			{
			if (Schedule != null && Schedule.Next != null)
				{
				if (await CancelBoostAsync (cancellationToken).ConfigureAwait (false))
					{
					return await OverrideStateAsync (Schedule.Next.Setting!.ToString (), cancellationToken).ConfigureAwait (false);
					}
				}
			return false;
			}
		}
	}
