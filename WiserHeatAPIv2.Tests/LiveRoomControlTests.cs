// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

/// <summary>Explicitly selected control tests for one room named in private settings.</summary>
[TestFixture]
[Category ("Live")]
[Category ("LiveControl")]
[Explicit ("Select this fixture explicitly to operate the room specified by controlRoomName.")]
[NonParallelizable]
public sealed class LiveRoomControlTests
	{
	private LiveTestSettings _settings = null!;
	private WiserRestController? _controller;
	private WiserRoom _room = null!;
	private Dictionary<string, object> _original = null!;

	[SetUp]
	public async Task CaptureSelectedRoom ()
		{
		_settings = LiveTestSupport.LoadForRun ();
		if (string.IsNullOrWhiteSpace (_settings.ControlRoomName))
			Assert.Ignore ("Set controlRoomName in private LiveTestSettings.json to select a room for control tests.");
		_controller = new WiserRestController (new WiserConnection (_settings.HubHost, _settings.Secret));
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (_settings.TimeoutSeconds));
		var data = await ReadRoomsAsync (timeout.Token);
		var matches = data.Where (room => room.TryGetValue ("Name", out var name)
			&& string.Equals (name?.ToString (), _settings.ControlRoomName, StringComparison.OrdinalIgnoreCase)).ToList ();
		Assert.That (matches, Has.Count.EqualTo (1), "controlRoomName must identify exactly one room.");
		_original = matches.Single ();
		_room = new WiserRoom (_controller, _original, null, []);
		}

	[TearDown]
	public void Disconnect ()
		{
		_controller?.Dispose ();
		_controller = null;
		}

	private async Task<List<Dictionary<string, object>>> ReadRoomsAsync (CancellationToken token)
		{
		var data = await _controller!.GetHubDataAsync (RestConstants.WISER_HUB_DOMAIN.FormatInvariant (_settings.HubHost), cancellationToken: token);
		Assert.That (data.ContainsKey ("Room"), Is.True, "The hub did not return room data.");
		return (List<Dictionary<string, object>>)data["Room"];
		}

	private async Task WaitForAsync (Func<Dictionary<string, object>, bool> expected, CancellationToken token)
		{
		while (true)
			{
			token.ThrowIfCancellationRequested ();
			var data = (await ReadRoomsAsync (token)).Single (room => Convert.ToInt32 (room["id"]) == _room.Id);
			_room.Update (data, null, []);
			if (expected (data))
				return;
			await Task.Delay (250, token);
			}
		}

	[Test]
	public async Task WindowDetection_TogglesAndRestoresOriginalSetting ()
		{
		if (!_original.TryGetValue ("WindowDetectionActive", out var value) || value is not bool original)
			{
			Assert.Ignore ("The hub does not report a restorable window-detection setting.");
			return;
			}
		await RoomControlSupport.ChangeAndRestoreAsync (
			async token =>
				{
				Assert.That (await _room.SetWindowDetectionActiveAsync (!original, token), Is.True);
				await WaitForAsync (data => data.TryGetValue ("WindowDetectionActive", out var current) && Equals (current, !original), token);
				},
			async token =>
				{
				Assert.That (await _room.SetWindowDetectionActiveAsync (original, token), Is.True);
				await WaitForAsync (data => data.TryGetValue ("WindowDetectionActive", out var current) && Equals (current, original), token);
				}, _settings.TimeoutSeconds);
		}

	[Test]
	public async Task TemporarySetpoint_IsObservedAndScheduleControlIsRestored ()
		{
		if (!RoomControlSupport.IsScheduledWithoutOverride (_original))
			Assert.Ignore ("Temperature control requires Auto mode following its schedule, with no existing boost, override, or override timer.");
		if (!_original.TryGetValue ("CurrentSetPoint", out var value) || !_original.ContainsKey ("ScheduleId"))
			Assert.Ignore ("The hub does not report the current setpoint and schedule needed for restoration.");
		int original = Convert.ToInt32 (value);
		// Lowering the setpoint tests command handling without deliberately requesting more heat.
		if (original < 55 || original > 300)
			Assert.Ignore ("The current setpoint cannot be lowered by 0.5°C within the supported heating range.");
		int requested = original - 5;
		int schedule = Convert.ToInt32 (_original["ScheduleId"]);
		await RoomControlSupport.ChangeAndRestoreAsync (
			async token =>
				{
				// A one-minute override also bounds its lifetime if the test process is interrupted.
				Assert.That (await _room.SetTargetTemperatureForDurationAsync (requested / 10.0, 1, token), Is.True);
				await WaitForAsync (data => data.TryGetValue ("CurrentSetPoint", out var current) && Convert.ToInt32 (current) == requested, token);
				},
			async token =>
				{
				Assert.That (await _room.CancelOverridesAsync (token), Is.True);
				await WaitForAsync (data => RoomControlSupport.IsScheduledWithoutOverride (data)
					&& data.TryGetValue ("ScheduleId", out var id) && Convert.ToInt32 (id) == schedule
					&& data.TryGetValue ("CurrentSetPoint", out var current) && data.TryGetValue ("ScheduledSetPoint", out var scheduled)
					&& Convert.ToInt32 (current) == Convert.ToInt32 (scheduled), token);
				}, _settings.TimeoutSeconds);
		}
	}