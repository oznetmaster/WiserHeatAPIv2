// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using static WiserHeatApiV2.RestConstants;

namespace WiserHeatApiV2
	{
	public class WiserHotwater
		{
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, object> _data;
		private string _mode;

		public WiserHotwater (WiserRestController wiserRestController, IDictionary<string, object> hwData, WiserSchedule? schedule)
			{
			_wiserRestController = wiserRestController;
			_data = hwData;
			Schedule = schedule;
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextAuto;

			// Add device id to schedule
			Schedule?.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
			}

		public void Update (IDictionary<string, object> hwData, WiserSchedule? schedule)
			{
			var oldId = Id;
			var oldName = Name;

			if (hwData != null)
				{
				_data.Clear ();
				foreach (KeyValuePair<string, object> kv in hwData)
					_data[kv.Key] = kv.Value;
				}

			Schedule = schedule; // Uncomment if you want to update the schedule reference
			_mode = _data.TryGetValue ("Mode", out var mode) ? mode.ToString () : Constants.TextAuto;
			if (Schedule != null)
				{
				if (oldId != Id || oldName != Name)
					{
					// Remove old assignment if the id or name has changed
					_ = Schedule.Assignments.RemoveAll (a => (int)a["id"] == oldId || (string)a["name"] == oldName);
					Schedule.Assignments.Add (new Dictionary<string, object> { { "id", Id }, { "name", Name } });
					}
				}
			}

		private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default) =>
			_wiserRestController.SendCommandAsync (
				 WiserRestHotWater.FormatInvariant (Id),
				 cmd,
				 cancellationToken: cancellationToken
			);

		private static bool ValidateMode (string mode) =>
			AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

		public static List<string> AvailableModes => [.. Enum.GetValues (typeof (WiserHotWaterMode))
			 .Cast<WiserHotWaterMode> ()
			 .Select (m => m.ToString ())];

		public bool AwayModeSuppressed => _data.TryGetValue ("AwayModeSuppressed", out var suppressed) && ConvertInvariant.ToBoolean (suppressed);

		public DateTime BoostEndTime => _data.TryGetValue ("OverrideTimeoutUnixTime", out var time) && ConvertInvariant.ToInt32 (time) > 0
			 ? DateTimeOffset.FromUnixTimeSeconds (ConvertInvariant.ToInt32 (time)).DateTime
			 : DateTime.MinValue;

		public double BoostTimeRemaining => IsBoost
			 ? (BoostEndTime - DateTime.Now).TotalSeconds
			 : 0;

		public string CurrentControlSource => _data.TryGetValue ("HotWaterDescription", out var source) ? source.ToString () : Constants.TextUnknown;

		public string CurrentState => _data.TryGetValue ("HotWaterRelayState", out var state) ? state.ToString () : Constants.TextUnknown;

		public int Id => _data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

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
						{
						_mode = value;
						}
					}
				else
					{
					throw new ArgumentException ($"{value} is not a valid Hot Water mode. Valid modes are {string.Join (", ", AvailableModes)}");
					}
				}
			}

		public static string Name => "HotWater";

		public static string ProductType => "HotWater";

		public WiserSchedule? Schedule { get; private set; }

		public int ScheduleId => _data.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

		public Task<bool> BoostAsync (int duration, CancellationToken cancellationToken = default) =>
			OverrideStateForDurationAsync (Constants.TextOn, duration, cancellationToken);

		public Task<bool> CancelBoostAsync (CancellationToken cancellationToken = default) =>
			IsBoost ? CancelOverridesAsync (cancellationToken) : Task.FromResult (true);

		public Task<bool> CancelOverridesAsync (CancellationToken cancellationToken = default) =>
			SendCommandAsync (new
				{
				RequestOverride = new
					{
					Type = Constants.TextNone
					}
				}, cancellationToken);

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
				else
					{
					return state.Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase)
						? await SendCommandAsync (new
							{
							RequestOverride = new
								{
								Type = Constants.TextManual,
								SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOff, "hotwater")
								}
							}, cancellationToken).ConfigureAwait (false)
						: throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TextOn} or {Constants.TextOff}");
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
			else
				{
				return state.Equals (Constants.TextOff, StringComparison.OrdinalIgnoreCase)
					? SendCommandAsync (new
						{
						RequestOverride = new
							{
							Type = Constants.TextManual,
							DurationMinutes = duration,
							SetPoint = WiserTemperatureFunctions.ToWiserTemp (Constants.TempHwOff)
							}
						}, cancellationToken)
					: throw new ArgumentException ($"Invalid state value {state}. Should be {Constants.TextOn} or {Constants.TextOff}");
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
