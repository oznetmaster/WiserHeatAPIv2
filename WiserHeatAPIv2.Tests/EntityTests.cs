// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class EntityTests
	{
	private ScriptedHub _hub = null!;
	private WiserRestController _controller = null!;

	[SetUp]
	public void SetUp ()
		{
		_hub = new ScriptedHub ();
		_controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret"), _hub);
		}

	[TearDown]
	public void TearDown ()
		{
		_controller.Dispose ();
		_hub.Dispose ();
		}

	private WiserRoom Room (string json = """{"id":4,"Name":"Study","Mode":"Auto","CurrentSetPoint":205}""") =>
		new (_controller, ScriptedHub.Data (json), null, []);

	[TestCase ("Auto", 205, "Auto")]
	[TestCase ("Manual", 205, "Manual")]
	[TestCase ("Manual", -200, "Off")]
	[TestCase ("manual", -200, "Off")]
	public void Room_InfersEffectiveHeatingMode (string mode, int setpoint, string expected)
		{
		var room = new WiserRoom (_controller, new Dictionary<string, object>
			{
			["Mode"] = mode,
			["CurrentSetPoint"] = setpoint
			}, null, []);
		Assert.That (room.Mode, Is.EqualTo (expected));
		Assert.That (_hub.Requests, Is.Empty);
		}

	[TestCase ("SetpointOrigin", "Boost", true, false)]
	[TestCase ("SetPointOrigin", "Boost", true, false)]
	[TestCase ("SetpointOrigin", "Away", false, true)]
	[TestCase ("SetPointOrigin", "Away", false, true)]
	public void Room_AcceptsBothFirmwareOriginSpellings (string key, string origin, bool boost, bool away)
		{
		var room = new WiserRoom (_controller, new Dictionary<string, object> { [key] = origin }, null, []);
		Assert.That (room.IsBoost, Is.EqualTo (boost));
		Assert.That (room.IsAwayMode, Is.EqualTo (away));
		Assert.That (room.TargetTemperatureOrigin, Is.EqualTo (origin));
		}

	[Test]
	public void Room_UpdateReplacesValuesAndRemovesMissingTelemetry ()
		{
		var room = Room ("""{"id":4,"Name":"Study","PercentageDemand":60,"ControlOutputState":"On"}""");
		Assert.That (room.IsHeating, Is.True);
		room.Update (ScriptedHub.Data ("""{"id":4,"Name":"Office","CalculatedTemperature":212}"""), null, []);
		Assert.That (room.Name, Is.EqualTo ("Office"));
		Assert.That (room.CurrentTemperature, Is.EqualTo (21.2));
		Assert.That (room.PercentageDemand, Is.Zero);
		Assert.That (room.IsHeating, Is.False);
		}

	[TestCase (20.5, 205)]
	[TestCase (40, 300)]
	[TestCase (0, 50)]
	public async Task Room_SetpointCommandUsesTenthsAndHeatingLimits (double temperature, int expected)
		{
		_hub.Reply ();
		Assert.That (await Room ().SetTargetTemperatureAsync (temperature), Is.True);
		var request = _hub.Requests.Single ();
		Assert.That (request.Url, Is.EqualTo ("http://hub.example/data/v2/domain/Room/4"));
		Assert.That (request.Method, Is.EqualTo ("PATCH"));
		var payload = JObject.Parse (request.Body)["RequestOverride"]!;
		Assert.That (payload["Type"]!.Value<string> (), Is.EqualTo ("Manual"));
		Assert.That (payload["SetPoint"]!.Value<int> (), Is.EqualTo (expected));
		}

	[Test]
	public async Task Room_BoostCapsIncreaseAndIncludesDuration ()
		{
		_hub.Reply ();
		await Room ().BoostAsync (8, 30);
		var payload = JObject.Parse (_hub.Requests.Single ().Body)["RequestOverride"]!;
		Assert.That (payload["Type"]!.Value<string> (), Is.EqualTo ("Boost"));
		Assert.That (payload["IncreaseSetPointBy"]!.Value<int> (), Is.EqualTo (50));
		Assert.That (payload["DurationMinutes"]!.Value<int> (), Is.EqualTo (30));
		}

	[Test]
	public async Task Room_ZeroDurationBoostCancelsAnExistingOverride ()
		{
		_hub.Reply ();
		await Room ("""{"id":4,"SetPointOrigin":"Boost"}""").BoostAsync (2, 0);
		Assert.That (JObject.Parse (_hub.Requests.Single ().Body)["RequestOverride"]!["Type"]!.Value<string> (), Is.EqualTo ("None"));
		}

	[Test]
	public void Room_InvalidModeAndMissingScheduleSendNoCommands ()
		{
		var room = Room ();
		Assert.ThrowsAsync<ArgumentException> (async () => await room.SetModeAsync ("invalid"));
		Assert.ThrowsAsync<InvalidOperationException> (async () => await room.ScheduleAdvanceAsync ());
		Assert.That (_hub.Requests, Is.Empty);
		}

	[Test]
	public async Task Room_DeleteUsesDeleteVerb ()
		{
		_hub.Reply ();
		await Room ().DeleteAsync ();
		Assert.That (_hub.Requests.Single ().Method, Is.EqualTo ("DELETE"));
		Assert.That (_hub.Requests.Single ().Body, Is.Empty);
		}

	[Test]
	public async Task SmartPlug_StateChangesOnlyAfterSuccessfulCommandsAndRepeatedOnIsANoOp ()
		{
		var plug = new WiserSmartPlug (_controller, ScriptedHub.Data ("""{"id":8,"ProductType":"SmartPlug"}"""),
			ScriptedHub.Data ("""{"Name":"Desk","OutputState":"Off","Mode":"Auto"}"""), null!);
		_hub.Reply ();
		Assert.That (await plug.TurnOnAsync (), Is.True);
		Assert.That (plug.IsOn, Is.True);
		Assert.That (await plug.TurnOnAsync (), Is.True);
		Assert.That (_hub.Requests, Has.Count.EqualTo (1));
		Assert.That (_hub.Requests[0].Url, Is.EqualTo ("http://hub.example/data/v2/domain/SmartPlug/8"));
		Assert.That (JObject.Parse (_hub.Requests[0].Body)["RequestOutput"]!.Value<string> (), Is.EqualTo ("On"));
		_hub.Reply ("denied", HttpStatusCode.Unauthorized);
		Assert.ThrowsAsync<WiserHubAuthenticationException> (async () => await plug.TurnOffAsync ());
		Assert.That (plug.IsOn, Is.True);
		_hub.Reply ();
		Assert.That (await plug.TurnOffAsync (), Is.True);
		Assert.That (plug.IsOn, Is.False);
		}

	[Test]
	public async Task Device_LockCommandUsesDeviceEndpointAndIsIdempotent ()
		{
		var device = new WiserDevice (_controller, ScriptedHub.Data ("""{"id":9}"""), new Dictionary<string, object> ());
		_hub.Reply ();
		await device.SetDeviceLockEnabledAsync (true);
		await device.SetDeviceLockEnabledAsync (true);
		Assert.That (device.DeviceLockEnabled, Is.True);
		Assert.That (_hub.Requests.Single ().Url, Is.EqualTo ("http://hub.example/data/v2/domain/Device/9"));
		Assert.That (JObject.Parse (_hub.Requests[0].Body)["DeviceLockEnabled"]!.Value<bool> (), Is.True);
		}

	[TestCase (true, new int[] { 1, 2, 3 })]
	[TestCase (false, new int[] { 2, 3 })]
	public async Task HeatingSchedule_AssignmentDeduplicatesAndOptionallyRetainsExistingRooms (bool retain, int[] expected)
		{
		var schedule = new WiserHeatingSchedule (_controller, "Heating",
			ScriptedHub.Data ("""{"id":7,"Name":"Weekdays"}"""), new Dictionary<string, string> (), new Dictionary<string, string> ());
		schedule.Assignments.Add (new Dictionary<string, object> { ["id"] = 1, ["name"] = "Study" });
		_hub.Reply ();
		Assert.That (await schedule.AssignScheduleAsync ([2, 2, 3], retain), Is.True);
		var payload = JObject.Parse (_hub.Requests.Single ().Body);
		Assert.That (payload["Assignments"]!.Values<int> (), Is.EquivalentTo (expected));
		Assert.That (payload["Heating"]!["id"]!.Value<int> (), Is.EqualTo (7));
		}

	[Test]
	public void HeatingSchedule_ExportRemovesRuntimeFieldsAndConvertsSlots ()
		{
		var schedule = new WiserHeatingSchedule (_controller, "Heating",
			ScriptedHub.Data ("""{"id":7,"Name":"Weekdays","CurrentSetpoint":205,"Monday":{"Time":[630,2200],"DegreesC":[205,160]}}"""),
			new Dictionary<string, string> (), new Dictionary<string, string> ());
		Assert.That (schedule.ScheduleData.Keys, Is.EquivalentTo (new[] { "Monday" }));
		Assert.That (schedule.Name, Is.EqualTo ("Weekdays"));
		Assert.That (schedule.CurrentSetting, Is.EqualTo (20.5));
		var exported = JObject.FromObject (schedule.WsScheduleData);
		Assert.That (exported["ScheduleData"]![0]!["slots"]![0]!["Time"]!.Value<string> (), Is.EqualTo ("06:30"));
		Assert.That (exported["ScheduleData"]![0]!["slots"]![0]!["Setpoint"]!.Value<double> (), Is.EqualTo (20.5));
		}
	}