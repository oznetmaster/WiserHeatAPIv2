// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

/// <summary>Opt-in, read-only checks against a configured Wiser hub.</summary>
[TestFixture]
[Category ("Live")]
[NonParallelizable]
public sealed class LiveHubTests
	{
	private WiserAPI? _api;
	private LiveTestSettings _settings = null!;

	[OneTimeSetUp]
	public async Task AuthenticateAndReadInitialSnapshot ()
		{
		_settings = LiveTestSupport.LoadForRun ();
		_api = new WiserAPI (_settings.HubHost, _settings.Secret);
		try
			{
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (_settings.TimeoutSeconds));
			await _api.InitializeAsync (timeout.Token);
			}
		catch
			{
			_api.Dispose ();
			_api = null;
			throw;
			}
		}

	[OneTimeTearDown]
	public void Disconnect ()
		{
		_api?.Dispose ();
		_api = null;
		}

	[Test]
	public void SavedSecret_AuthenticatesAndLoadsRequiredEndpoints ()
		{
		Assert.That (_api!.System, Is.Not.Null);
		foreach (string endpoint in new[] { "Domain", "Network", "Schedule" })
			Assert.That ((Dictionary<string, object>)_api.RawHubData[endpoint], Is.Not.Empty, endpoint + " returned no data.");
		}

	[Test]
	public void Rooms_HaveUniqueIdsAndCanBeLookedUp ()
		{
		var rooms = _api!.Rooms!.All;
		if (rooms.Count == 0)
			Assert.Ignore ("The hub has no configured rooms.");
		Assert.That (rooms.Select (room => room.Id), Is.Unique);
		foreach (var room in rooms)
			Assert.That (_api.Rooms.GetById (room.Id), Is.SameAs (room));
		}

	[Test]
	public void RoomTelemetry_CanBeReadWithoutCommands ()
		{
		var rooms = _api!.Rooms!.All;
		if (rooms.Count == 0)
			Assert.Ignore ("The hub has no configured rooms.");
		foreach (var room in rooms)
			{
			Assert.That (double.IsNaN (room.CurrentTemperature) || double.IsInfinity (room.CurrentTemperature), Is.False);
			Assert.That (double.IsNaN (room.CurrentTargetTemperature) || double.IsInfinity (room.CurrentTargetTemperature), Is.False);
			Assert.That (room.Name, Is.Not.Null);
			Assert.That (room.Mode, Is.AnyOf ("Auto", "Manual", "Off"));
			}
		}

	[Test]
	public void Devices_HaveUniqueIdsAndCanBeLookedUp ()
		{
		var devices = _api!.Devices!.All;
		if (devices.Count == 0)
			Assert.Ignore ("The hub has no supported devices configured.");
		Assert.That (devices.Select (device => device.Id), Is.Unique);
		foreach (var device in devices)
			Assert.That (_api.Devices.GetById (device.Id), Is.SameAs (device));
		}

	[Test]
	public void Schedules_HaveUniqueIdsWithinEachTypeAndReadableData ()
		{
		var schedules = _api!.Schedules!.All;
		if (schedules.Count == 0)
			Assert.Ignore ("The hub has no schedules configured.");
		foreach (var group in schedules.GroupBy (schedule => schedule.ScheduleType))
			Assert.That (group.Select (schedule => schedule.Id), Is.Unique);
		foreach (var schedule in schedules)
			Assert.That (schedule.ScheduleData, Is.Not.Null);
		}

	[Test]
	public async Task RepeatedRefresh_PreservesExistingRoomIdentity ()
		{
		var original = _api!.Rooms!.All.ToDictionary (room => room.Id);
		for (int refresh = 0; refresh < 2; refresh++)
			{
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (_settings.TimeoutSeconds));
			Assert.That (await _api.ReadHubDataAsync (timeout.Token), Is.True, "Hub refresh failed.");
			foreach (var room in _api.Rooms.All)
				if (original.TryGetValue (room.Id, out var previous))
					Assert.That (room, Is.SameAs (previous));
			}
		}

	[Test]
	public void OpenTherm_ResponseIsReadableWhenAvailable ()
		{
		var data = (Dictionary<string, object>)_api!.RawHubData["OpenTherm"];
		if (data.Count == 0)
			Assert.Ignore ("The hub returned no OpenTherm data; this capability is optional.");
		Assert.That (data.Keys.All (key => !string.IsNullOrWhiteSpace (key)), Is.True);
		}
	}