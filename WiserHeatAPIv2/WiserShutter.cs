// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using static WiserHeatApiV2.EnumValues;
using static WiserHeatApiV2.Constants;

namespace WiserHeatApiV2;

#if SHUTTER
/// <summary>
/// Represents a shutter device with lift control and scheduling.
/// </summary>
public class WiserShutter : WiserElectricalLevelDevice
	{
	/// <summary>
	/// Represents lift movement configuration times for a shutter.
	/// </summary>
	/// <param name="shutterInstance">The shutter used to send movement-time updates.</param>
	/// <param name="data">The hub data used to populate this device or view.</param>
	public class WiserLiftMovementRange (WiserShutter shutterInstance, Dictionary<string, object>? data)
		{
		/// <summary>Gets the configured open movement time in milliseconds, if available.</summary>
		/// <value>The configured open movement time in milliseconds, if available.</value>
		public int? OpenTime => data?.TryGetValue ("LiftOpenTime", out var time) == true ? (int?)ConvertInvariant.ToInt32 (time) : null;

		/// <summary>Gets the configured close movement time in milliseconds, if available.</summary>
		/// <value>The configured close movement time in milliseconds, if available.</value>
		public int? CloseTime => data?.TryGetValue ("LiftCloseTime", out var time) == true ? (int?)ConvertInvariant.ToInt32 (time) : null;

		/// <summary>Sets the open movement time in milliseconds.</summary>
		/// <param name="time">The movement duration in milliseconds.</param>
		/// <param name="cancellationToken">A token used to cancel the operation.</param>
		/// <returns>A task that completes when the opening movement-time command finishes.</returns>
		public async Task SetOpenTimeAsync (int time, CancellationToken cancellationToken = default) =>
			_ = await shutterInstance.SendCommandAsync (new
				{
				LiftOpenTime = time,
				LiftCloseTime = CloseTime
				}, cancellationToken: cancellationToken).ConfigureAwait (false);

		/// <summary>Sets the close movement time in milliseconds.</summary>
		/// <param name="time">The movement duration in milliseconds.</param>
		/// <param name="cancellationToken">A token used to cancel the operation.</param>
		/// <returns>A task that completes when the closing movement-time command finishes.</returns>
		public async Task SetCloseTimeAsync (int time, CancellationToken cancellationToken = default) =>
			_ = await shutterInstance.SendCommandAsync (new
				{
				LiftOpenTime = OpenTime,
				LiftCloseTime = time
				}, cancellationToken: cancellationToken).ConfigureAwait (false);
		}

	private string _awayAction;
	private string _mode;
	//private string _name;

	/// <summary>
	/// Initializes a new instance of the <see cref="WiserShutter"/> class.
	/// </summary>
	/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
	/// <param name="data">The hub data used to populate this device or view.</param>
	/// <param name="deviceTypeData">The device-type-specific data returned by the hub.</param>
	/// <param name="schedule">The schedule associated with this device.</param>
	public WiserShutter (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData, WiserSchedule schedule)
		 : base (wiserRestController, data, deviceTypeData)
		{
		Schedule = schedule;
		_awayAction = deviceTypeData.GetStringOr ("AwayAction");
		_mode = deviceTypeData.GetStringOr ("Mode");
		//_name = deviceTypeData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		// Add device id to schedule
		if (Schedule != null)
			{
			Schedule.Assignments.Add (new Dictionary<string, object> { { "id", ShutterId }, { "name", Name } });
			Schedule.DeviceIds.Add (Id);
			}
		}

	private Task<bool> SendCommandAsync (object cmd, CancellationToken cancellationToken = default)
		{
		var url = RestConstants.WISER_REST_SHUTTER.FormatInvariant (ShutterId);

		return WiserRestController.SendCommandAsync (url, cmd, cancellationToken: cancellationToken);
		}

	private static bool ValidateMode (string mode) => AvailableModes.Any (m => m.Equals (mode, StringComparison.OrdinalIgnoreCase));

	private static bool ValidateAwayAction (string action) =>
		AvailableAwayModeActions.Any (a => a.Equals (action, StringComparison.OrdinalIgnoreCase));

	/// <summary>Gets the list of allowed shutter modes.</summary>
	/// <value>The list of allowed shutter modes.</value>
	public static List<string> AvailableModes => [.. GetValues<WiserShutterMode> ()
		 .Select (m => m.ToString ())];

	/// <summary>Gets the list of allowed away-mode actions for shutters.</summary>
	/// <value>The list of allowed away-mode actions for shutters.</value>
	public static List<string> AvailableAwayModeActions => [.. GetValues<WiserAwayAction> ()
		 .Where (a => a is WiserAwayAction.Close or WiserAwayAction.NoChange)
		 .Select (a => a.ToString ())];

	/// <summary>Gets or sets the action applied during Away mode.</summary>
	/// <value>The action applied during Away mode.</value>
	public string AwayModeAction
		{
		get => _awayAction;
		set
			{
			if (value == _awayAction)
				return; // No change needed
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
				throw new ArgumentException ($"{value} is not a valid Shutter away mode action. Valid modes are {string.Join (", ", AvailableAwayModeActions)}");
				}
			}
		}

	/// <summary>Gets the lift control source reported by the hub.</summary>
	/// <value>The lift control source reported by the hub.</value>
	public string ControlSource => DeviceTypeData.GetStringOr ("ControlSource");

	/// <summary>Gets the current lift position in percent (0..100).</summary>
	/// <value>The current lift position in percent (0..100).</value>
	public int CurrentLift => DeviceTypeData.TryGetValue ("CurrentLift", out var lift) ? ConvertInvariant.ToInt32 (lift) : 0;

	/// <summary>Requests a new lift target position (0..100 percent).</summary>
	/// <param name="percentage">The requested lift position from 0 (closed) to 100 (open).</param>
	/// <param name="cancellationToken">A token used to cancel the operation.</param>
	/// <returns>A task that completes when the lift-position command finishes.</returns>
	public async Task SetCurrentLiftAsync (int percentage, CancellationToken cancellationToken = default) =>
		_ = percentage is >= 0 and <= 100
			? await SendCommandAsync (new
				{
				RequestAction = new
					{
					Action = "LiftTo",
					Percentage = percentage
					}
				}, cancellationToken: cancellationToken).ConfigureAwait (false)
			: throw new ArgumentException ("Shutter percentage must be between 0 and 100");

	/// <summary>Gets the drive configuration (movement times) for the shutter.</summary>
	/// <value>The drive configuration (movement times) for the shutter.</value>
	public WiserLiftMovementRange DriveConfig =>
		DeviceTypeData.TryGetValue ("DriveConfig", out var config) && config is Dictionary<string, object> configDict
				? new WiserLiftMovementRange (this, configDict)
				: new WiserLiftMovementRange (this, null);

	/// <summary>Gets a value indicating whether the shutter is fully open.</summary>
	/// <value>A value indicating whether the shutter is fully open.</value>
	public bool IsOpen => CurrentLift == 100;

	/// <summary>Gets a value indicating whether the shutter is fully closed.</summary>
	/// <value>A value indicating whether the shutter is fully closed.</value>
	public bool IsClosed => CurrentLift == 0;

	/// <summary>Gets a value indicating whether the shutter is currently closing.</summary>
	/// <value>A value indicating whether the shutter is currently closing.</value>
	public bool IsClosing => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Closing";

	/// <summary>Gets a value indicating whether the shutter is currently opening.</summary>
	/// <value>A value indicating whether the shutter is currently opening.</value>
	public bool IsOpening => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Opening";

	/// <summary>Gets a value indicating whether the shutter movement is stopped.</summary>
	/// <value>A value indicating whether the shutter movement is stopped.</value>
	public bool IsStopped => DeviceTypeData.TryGetValue ("LiftMovement", out var movement) && movement.ToString () == "Stopped";

	/// <summary>Gets a value indicating whether the shutter is currently moving.</summary>
	/// <value>A value indicating whether the shutter is currently moving.</value>
	public bool IsMoving => !IsStopped;

	/// <summary>Gets the textual description of the current lift movement state.</summary>
	/// <value>The textual description of the current lift movement state.</value>
	public string LiftMovement => DeviceTypeData.GetStringOr ("LiftMovement");

	/// <summary>Gets the last manual lift position in percent.</summary>
	/// <value>The last manual lift position in percent.</value>
	public int ManualLift => DeviceTypeData.TryGetValue ("ManualLift", out var lift) ? ConvertInvariant.ToInt32 (lift) : 0;

	/// <summary>Gets or sets the operating mode for the shutter.</summary>
	/// <value>The operating mode for the shutter.</value>
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
				throw new ArgumentException ($"{value} is not a valid Shutter mode. Valid modes are {string.Join (", ", AvailableModes)}");
				}
			}
		}

	/*
	override public string Name
		{
		get => _name;
		set
			{
			if (value == _name)
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
				_name = value;
				}
			}
		}
	*/

	/// <summary>Gets the assigned schedule for the shutter, if any.</summary>
	/// <value>The assigned schedule for the shutter, if any.</value>
	public WiserSchedule? Schedule
		{
		get;
		}

	/// <summary>Gets the schedule identifier assigned to the shutter.</summary>
	/// <value>The schedule identifier assigned to the shutter.</value>
	public int ScheduleId => DeviceTypeData.TryGetValue ("ScheduleId", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the scheduled lift text reported by the hub.</summary>
	/// <value>The scheduled lift text reported by the hub.</value>
	public string ScheduledLift => DeviceTypeData.GetStringOr ("ScheduledLift");

	/// <summary>Gets the shutter device type identifier.</summary>
	/// <value>The shutter device type identifier.</value>
	public int ShutterId => DeviceTypeData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the target lift position in percent (0..100).</summary>
	/// <value>The target lift position in percent (0..100).</value>
	public int TargetLift => DeviceTypeData.TryGetValue ("TargetLift", out var lift) ? ConvertInvariant.ToInt32 (lift) : 0;

	/// <summary>Opens the shutter to 100%.</summary>
	/// <param name="cancellationToken">A token used to cancel the operation.</param>
	/// <returns>A task that completes when the open command finishes; physical movement may continue afterward.</returns>
	public Task OpenAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestAction = new
				{
				Action = "LiftTo",
				Percentage = 100
				}
			}, cancellationToken: cancellationToken);

	/// <summary>Closes the shutter to 0%.</summary>
	/// <param name="cancellationToken">A token used to cancel the operation.</param>
	/// <returns>A task that completes when the close command finishes; physical movement may continue afterward.</returns>
	public Task CloseAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestAction = new
				{
				Action = "LiftTo",
				Percentage = 0
				}
			}, cancellationToken: cancellationToken);

	/// <summary>Stops the shutter movement.</summary>
	/// <param name="cancellationToken">A token used to cancel the operation.</param>
	/// <returns>A task that completes when the stop command finishes.</returns>
	public Task StopAsync (CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			RequestAction = new
				{
				Action = "Stop"
				}
			}, cancellationToken: cancellationToken);
	}

/// <summary>
/// Collection wrapper and lookup helpers for shutter devices.
/// </summary>
public class WiserShutters
	{
		/// <summary>Gets all shutter devices.</summary>
		/// <value>All shutter devices.</value>
		public List<WiserShutter> All { get; } = [];

		/// <summary>Gets the list of allowed shutter modes.</summary>
		/// <value>The list of allowed shutter modes.</value>
		public static List<string> AvailableModes => [.. GetValues<WiserShutterMode> ()
			 .Select (m => m.ToString ())];

		/// <summary>Gets the number of shutters in the collection.</summary>
		/// <value>The number of shutters in the collection.</value>
		public int Count => All.Count;

		/// <summary>Finds a shutter by its device id.</summary>
		/// <param name="id">The device identifier to look up.</param>
		/// <returns>The first matching shutter, or <see langword="null"/> if none matches.</returns>
		public WiserShutter? GetById (int id) => All.FirstOrDefault (shutter => shutter.Id == id);

		/// <summary>Finds a shutter by its shutter id.</summary>
		/// <param name="shutterId">The shutter identifier to look up.</param>
		/// <returns>The first matching shutter, or <see langword="null"/> if none matches.</returns>
		public WiserShutter? GetByShutterId (int shutterId) => All.FirstOrDefault (shutter => shutter.ShutterId == shutterId);

		/// <summary>Gets all shutters that belong to the specified room.</summary>
		/// <param name="roomId">The identifier of the room whose devices are requested.</param>
		/// <returns>All shutters assigned to the room, or an empty list if none match.</returns>
		public List<WiserShutter> GetByRoomId (int roomId) => [.. All.Where (shutter => shutter.RoomId == roomId)];
	}
#endif
