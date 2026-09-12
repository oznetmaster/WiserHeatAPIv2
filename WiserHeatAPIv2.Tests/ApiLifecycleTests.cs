// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class ApiLifecycleTests
	{
	private static void Snapshot (ScriptedHub hub, string name, int temperature)
		{
		hub.Reply ($$"""{"System":{},"Device":[],"Room":[{"id":4,"Name":"{{name}}","CurrentSetPoint":{{temperature}}}],"HeatingChannel":[],"Moment":[]}""");
		hub.Reply ("""{"Station":{}}""");
		hub.Reply ("""{"Heating":[]}""");
		hub.Reply ("not supported", HttpStatusCode.NotFound);
		}

	[Test]
	public async Task InitializeAndRefresh_PreserveRoomIdentityAndUpdateObservedState ()
		{
		using var hub = new ScriptedHub ();
		using var api = new WiserAPI ("hub.example", "test-secret", WiserUnits.Metric, hub);
		Assert.That (hub.Requests, Is.Empty, "Construction must not perform network IO.");
		Snapshot (hub, "Study", 205);
		await api.InitializeAsync ();
		var room = api.Rooms!.GetById (4);
		Assert.That (room, Is.Not.Null);
		Assert.That (room!.CurrentTargetTemperature, Is.EqualTo (20.5));
		var system = api.System;
		Snapshot (hub, "Office", 215);
		Assert.That (await api.ReadHubDataAsync (), Is.True);
		Assert.That (api.Rooms.GetById (4), Is.SameAs (room));
		Assert.That (api.System, Is.SameAs (system));
		Assert.That (room.Name, Is.EqualTo ("Office"));
		Assert.That (room.CurrentTargetTemperature, Is.EqualTo (21.5));
		Assert.That (hub.Requests.Take (4).Select (r => new Uri (r.Url).AbsolutePath), Is.EqualTo (new[]
			{
			"/data/v2/domain/", "/data/v2/network/", "/data/v2/schedules/", "/data/v2/opentherm/"
			}));
		hub.AssertComplete ();
		}

	[Test]
	public async Task FailedRefresh_KeepsTheLastSuccessfulRoomState ()
		{
		using var hub = new ScriptedHub ();
		using var api = new WiserAPI ("hub.example", "test-secret", WiserUnits.Metric, hub);
		Snapshot (hub, "Study", 205);
		await api.InitializeAsync ();
		hub.Reply ("""{"Room":[{"id":4,"Name":"Partial"}]}""");
		hub.Reply ("denied", HttpStatusCode.Unauthorized);
		Assert.That (await api.ReadHubDataAsync (), Is.False);
		Assert.That (api.Rooms!.GetById (4)!.Name, Is.EqualTo ("Study"));
		hub.AssertComplete ();
		}

	[Test]
	public void FailedInitialization_ReportsFailure ()
		{
		using var hub = new ScriptedHub ();
		using var api = new WiserAPI ("hub.example", "test-secret", WiserUnits.Metric, hub);
		hub.Reply ("denied", HttpStatusCode.Unauthorized);
		Assert.ThrowsAsync<WiserHubConnectionException> (async () => await api.InitializeAsync ());
		}

	[TestCase ("", "secret")]
	[TestCase ("hub.example", "")]
	public void IncompleteConnection_IsRejectedWithoutRequests (string host, string secret)
		{
		using var hub = new ScriptedHub ();
		Assert.Throws<WiserHubConnectionException> (() => new WiserAPI (host, secret, WiserUnits.Metric, hub));
		Assert.That (hub.Requests, Is.Empty);
		}

	[Test]
	public async Task DisposedApi_CannotRefreshAndDisposesTransport ()
		{
		using var hub = new ScriptedHub ();
		var api = new WiserAPI ("hub.example", "test-secret", WiserUnits.Metric, hub);
		api.Dispose ();
		Assert.That (hub.Disposed, Is.True);
		Assert.That (await api.ReadHubDataAsync (), Is.False);
		Assert.DoesNotThrow (api.Dispose);
		}
	}