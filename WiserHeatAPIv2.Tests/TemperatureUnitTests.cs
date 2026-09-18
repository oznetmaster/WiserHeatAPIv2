// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class TemperatureUnitTests
	{
	[Test]
	public void OpenThermViews_FollowLiveConnectionUnitsWithoutChangingRawData ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret");
		using var controller = new WiserRestController (connection, hub);
		var raw = ScriptedHub.Data ("""{"roomTemperature":200,"roomSetpoint":210,"chFlowActiveLowerSetpoint":300,"chFlowActiveUpperSetpoint":800,"ch1FlowSetpoint":500,"ch2FlowSetpoint":450,"dhwFlowSetpoint":600,"operationalData":{"Ch1FlowTemperature":500,"ChReturnTemperature":400,"Dhw1Temperature":550,"ChPressureBar":15}}""");
		var system = new WiserSystem (controller, new Dictionary<string, object> (), new Dictionary<string, object> (), [], raw);
		var model = system.Opentherm!;
		var operational = model.OperationalData;
		Assert.That (model.RoomTemperature, Is.EqualTo (20));
		connection.Units = WiserUnits.Imperial;
		Assert.That (new[] { model.RoomTemperature, model.RoomSetpoint, model.ChFlowActiveLowerSetpoint, model.ChFlowActiveUpperSetpoint, model.Ch1FlowSetpoint, model.Ch2FlowSetpoint, model.HwFlowSetpoint },
			Is.EqualTo (new[] { 68, 69.8, 86, 176, 122, 113, 140 }));
		Assert.That (new[] { operational.ChFlowTemperature, operational.ChReturnTemperature, operational.HwTemperature }, Is.EqualTo (new[] { 122, 104, 131 }));
		Assert.That (operational.ChPressureBar, Is.EqualTo (1.5));
		connection.Units = WiserUnits.Metric;
		Assert.That (model.RoomTemperature, Is.EqualTo (20));
		Assert.That (operational.ChFlowTemperature, Is.EqualTo (50));
		Assert.That (Convert.ToInt32 (raw["roomTemperature"]), Is.EqualTo (200));
		Assert.That (hub.Requests, Is.Empty);
		}

	[Test]
	public void OpenThermPublicConstructors_KeepCelsiusCompatibility ()
		{
		var model = new WiserOpentherm (ScriptedHub.Data ("""{"roomTemperature":200}"""), "Connected");
		var operational = new WiserOpenThermOperationalData (ScriptedHub.Data ("""{"Ch1FlowTemperature":500}"""));
		Assert.That (model.RoomTemperature, Is.EqualTo (20));
		Assert.That (operational.ChFlowTemperature, Is.EqualTo (50));
		}

	[Test]
	public void HeatingActuator_FollowsUnitsAndPreservesOffSentinel ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial };
		using var controller = new WiserRestController (connection, hub);
		var typeData = ScriptedHub.Data ("""{"OccupiedHeatingSetPoint":210,"MeasuredTemperature":200}""");
		var actuator = new WiserHeatingActuator (controller, ScriptedHub.Data ("""{"id":4}"""), typeData);
		Assert.That (actuator.CurrentTargetTemperature, Is.EqualTo (69.8));
		Assert.That (actuator.CurrentTemperature, Is.EqualTo (68));
		typeData["OccupiedHeatingSetPoint"] = -200;
		Assert.That (actuator.CurrentTargetTemperature, Is.EqualTo (-20));
		connection.Units = WiserUnits.Metric;
		Assert.That (actuator.CurrentTemperature, Is.EqualTo (20));
		}

	[TestCase (41, 50)]
	[TestCase (68, 200)]
	[TestCase (69.8, 210)]
	[TestCase (86, 300)]
	[TestCase (95, 300)]
	[TestCase (32, 50)]
	[TestCase (-20, -200)]
	public void FahrenheitSetpoint_ConvertsBeforeApplyingCelsiusLimits (double input, int expected) =>
		Assert.That (WiserTemperatureFunctions.ToWiserTemp (input, units: WiserUnits.Imperial), Is.EqualTo (expected));

	[TestCase ("delta", 3.6, 20)]
	[TestCase ("delta", 18, 50)]
	[TestCase ("current", 32, 0)]
	[TestCase ("current", 68, 200)]
	[TestCase ("hotwater", 110, 1100)]
	[TestCase ("hotwater", -20, -200)]
	public void FahrenheitInput_UsesContextAndPreservesControlSentinels (string context, double input, int expected) =>
		Assert.That (WiserTemperatureFunctions.ToWiserTemp (input, context, WiserUnits.Imperial), Is.EqualTo (expected));

	[TestCase ("set_heating", 210, 69.8)]
	[TestCase ("set_heating", -200, -20)]
	[TestCase ("delta", 20, 3.6)]
	[TestCase ("current", 200, 68)]
	[TestCase ("current", 0, 32)]
	[TestCase ("hotwater", 1100, 110)]
	[TestCase ("hotwater", -200, -20)]
	public void FahrenheitOutput_UsesContextAndPreservesControlSentinels (string context, int input, double expected) =>
		Assert.That (WiserTemperatureFunctions.FromWiserTemp (input, context, WiserUnits.Imperial), Is.EqualTo (expected));

	[Test]
	public void RoomReadings_FollowUnitChangesWithoutChangingRawState ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret");
		using var controller = new WiserRestController (connection, hub);
		var room = new WiserRoom (controller, ScriptedHub.Data (
			"""{"id":4,"Mode":"Manual","CurrentSetPoint":210,"CalculatedTemperature":200,"DisplayedSetPoint":210,"ManualSetPoint":210,"ScheduledSetPoint":210}"""), null, []);
		connection.Units = WiserUnits.Imperial;
		Assert.That (room.CurrentTemperature, Is.EqualTo (68));
		Assert.That (new[] { room.CurrentTargetTemperature, room.DisplayedSetpoint, room.ManualTargetTemperature, room.ScheduledTargetTemperature }, Is.All.EqualTo (69.8));
		connection.Units = WiserUnits.Metric;
		Assert.That (room.CurrentTemperature, Is.EqualTo (20));
		Assert.That (new[] { room.CurrentTargetTemperature, room.DisplayedSetpoint, room.ManualTargetTemperature, room.ScheduledTargetTemperature }, Is.All.EqualTo (21));
		Assert.That (hub.Requests, Is.Empty);
		}

	[TestCase (false)]
	[TestCase (true)]
	public async Task RoomCommands_EncodeFahrenheitAsHubCelsius (bool timed)
		{
		using var hub = new ScriptedHub ();
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial }, hub);
		var room = new WiserRoom (controller, ScriptedHub.Data ("""{"id":4,"Mode":"Manual","CurrentSetPoint":210}"""), null, []);
		hub.Reply ();
		Assert.That (await (timed ? room.SetTargetTemperatureForDurationAsync (68, 30) : room.SetTargetTemperatureAsync (68)), Is.True);
		Assert.That (JObject.Parse (hub.Requests.Single ().Body)["RequestOverride"]!["SetPoint"]!.Value<int> (), Is.EqualTo (200));
		hub.AssertComplete ();
		}

	[Test]
	public async Task RoomBoost_UsesFahrenheitDifferenceWithoutAbsoluteOffset ()
		{
		using var hub = new ScriptedHub ();
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial }, hub);
		var room = new WiserRoom (controller, ScriptedHub.Data ("""{"id":4}"""), null, []);
		hub.Reply ();
		Assert.That (await room.BoostAsync (3.6, 30), Is.True);
		Assert.That (JObject.Parse (hub.Requests.Single ().Body)["RequestOverride"]!["IncreaseSetPointBy"]!.Value<int> (), Is.EqualTo (20));
		hub.AssertComplete ();
		}

	[Test]
	public void DeviceTemperatures_FollowConnectionUnits ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial };
		using var controller = new WiserRestController (connection, hub);
		var data = ScriptedHub.Data ("""{"id":4}""");
		var typeData = ScriptedHub.Data ("""{"SetPoint":210,"MeasuredTemperature":200}""");
		var valve = new WiserSmartValve (controller, data, typeData);
		var stat = new WiserRoomStat (controller, data, typeData);
		var ufh = new WiserUFHController (controller, data, typeData);
		Assert.That (new[] { valve.CurrentTemperature, stat.CurrentTemperature, ufh.CurrentTemperature }, Is.All.EqualTo (68));
		Assert.That (new[] { valve.CurrentTargetTemperature, stat.CurrentTargetTemperature }, Is.All.EqualTo (69.8));
		connection.Units = WiserUnits.Metric;
		Assert.That (new[] { valve.CurrentTemperature, stat.CurrentTemperature, ufh.CurrentTemperature }, Is.All.EqualTo (20));
		Assert.That (new[] { valve.CurrentTargetTemperature, stat.CurrentTargetTemperature }, Is.All.EqualTo (21));
		}

	[Test]
	public async Task HeatingSchedule_ExportsImportsAndAdvancesInConfiguredUnits ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial };
		using var controller = new WiserRestController (connection, hub);
		var data = ScriptedHub.Data ("""{"id":7,"Name":"Synthetic","CurrentSetpoint":210,"Next":{"Day":"Monday","Time":630,"DegreesC":210},"Monday":{"Time":[630,2200],"DegreesC":[210,-200]}}""");
		var schedule = new WiserHeatingSchedule (controller, "Heating", data, new Dictionary<string, string> (), new Dictionary<string, string> ());
		var next = schedule.Next!;
		Assert.That (schedule.CurrentSetting, Is.EqualTo (69.8));
		Assert.That (next.Setting, Is.EqualTo (69.8));
		var exported = JObject.FromObject (schedule.WsScheduleData);
		Assert.That (exported["ScheduleData"]![0]!["slots"]![0]!["Setpoint"]!.Value<double> (), Is.EqualTo (69.8));
		Assert.That (exported["ScheduleData"]![0]!["slots"]![1]!["Setpoint"]!.Value<double> (), Is.EqualTo (-20));
		hub.Reply ();
		// Match the public editor's dictionary/list input shape, not JToken values.
		var editor = new Dictionary<string, object>
			{
			["Type"] = "Heating",
			["ScheduleData"] = new List<object>
				{
				new Dictionary<string, object>
					{
					["day"] = "Monday",
					["slots"] = new List<IDictionary<string, object>>
						{
						new Dictionary<string, object> { ["Time"] = "06:30", ["Setpoint"] = 69.8 },
						new Dictionary<string, object> { ["Time"] = "22:00", ["Setpoint"] = -20 }
						}
					}
				}
			};
		Assert.That (await schedule.SetScheduleFromWsDataAsync (editor), Is.True);
		Assert.That (JObject.Parse (hub.Requests.Single ().Body)["Monday"]!["DegreesC"]!.Values<int> (), Is.EqualTo (new[] { 210, -200 }));
		connection.Units = WiserUnits.Metric;
		Assert.That (next.Setting, Is.EqualTo (21));
		Assert.That (schedule.CurrentSetting, Is.EqualTo (21));
		}

	[Test]
	public void SystemTargets_ReadAndWriteInConfiguredUnits ()
		{
		using var hub = new ScriptedHub ();
		var connection = new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial };
		using var controller = new WiserRestController (connection, hub);
		var system = new WiserSystem (controller, ScriptedHub.Data ("""{"System":{"AwayModeSetPointLimit":150,"DegradedModeSetpointThreshold":120}}"""), new Dictionary<string, object> (), [], new Dictionary<string, object> ());
		Assert.That (system.AwayModeTargetTemperature, Is.EqualTo (59));
		Assert.That (system.DegradedModeTargetTemperature, Is.EqualTo (53.6));
		hub.Reply ();
		system.AwayModeTargetTemperature = 68;
		Assert.That (JObject.Parse (hub.Requests.Last ().Body)["AwayModeSetPointLimit"]!.Value<int> (), Is.EqualTo (200));
		hub.Reply ();
		system.DegradedModeTargetTemperature = 59;
		Assert.That (JObject.Parse (hub.Requests.Last ().Body)["DegradedModeSetpointThreshold"]!.Value<int> (), Is.EqualTo (150));
		connection.Units = WiserUnits.Metric;
		Assert.That (system.AwayModeTargetTemperature, Is.EqualTo (20));
		Assert.That (system.DegradedModeTargetTemperature, Is.EqualTo (15));
		hub.AssertComplete ();
		}

	[Test]
	public void OverrideTemperature_ConvertsPresentValuesAndPreservesAbsentAndOff ()
		{
		using var hub = new ScriptedHub ();
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial }, hub);
		var room = new WiserRoom (controller, ScriptedHub.Data ("""{"id":4,"OverrideSetpoint":210}"""), null, []);
		Assert.That (room.OverrideTargetTemperature, Is.EqualTo (69.8));
		room.Update (ScriptedHub.Data ("""{"id":4,"OverrideSetpoint":-200}"""), null, []);
		Assert.That (room.OverrideTargetTemperature, Is.EqualTo (-20));
		room.Update (ScriptedHub.Data ("""{"id":4}"""), null, []);
		Assert.That (room.OverrideTargetTemperature, Is.Zero);
		}

	[Test]
	public void RoomOffMode_RemainsRecognizableInFahrenheit ()
		{
		using var hub = new ScriptedHub ();
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret") { Units = WiserUnits.Imperial }, hub);
		var room = new WiserRoom (controller, ScriptedHub.Data ("""{"id":4,"Mode":"Manual","CurrentSetPoint":-200}"""), null, []);
		Assert.That (room.Mode, Is.EqualTo ("Off"));
		Assert.That (room.CurrentTargetTemperature, Is.EqualTo (-20));
		}
	}