// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class ModelTests
	{
	[TestCase (5, 50)]
	[TestCase (20.5, 205)]
	[TestCase (30, 300)]
	[TestCase (40, 300)]
	[TestCase (0, 50)]
	[TestCase (-20, -200)]
	[TestCase (2000, 50)]
	public void HeatingSetpoint_ConvertsAndClampsMetricTemperature (double temperature, int expected) =>
		Assert.That (WiserTemperatureFunctions.ToWiserTemp (temperature), Is.EqualTo (expected));

	[TestCase (50, 5)]
	[TestCase (205, 20.5)]
	[TestCase (300, 30)]
	[TestCase (400, 30)]
	[TestCase (-200, -20)]
	[TestCase (2000, 5)]
	public void HubSetpoint_DecodesTemperature (int temperature, double expected) =>
		Assert.That (WiserTemperatureFunctions.FromWiserTemp (temperature), Is.EqualTo (expected));

	[TestCase ("delta", 8, 50)]
	[TestCase ("delta", 2.5, 25)]
	[TestCase ("current", 35, 350)]
	[TestCase ("current", -5, -50)]
	[TestCase ("hotwater", 110, 1100)]
	[TestCase ("hotwater", -20, -200)]
	public void TemperatureConversion_RespectsContext (string context, double temperature, int expected) =>
		Assert.That (WiserTemperatureFunctions.ToWiserTemp (temperature, context), Is.EqualTo (expected));

	[Test]
	public void NullableTemperature_HandlesMissingValues ()
		{
		Assert.That (WiserTemperatureFunctions.FromWiserTemp ((int?)null), Is.Zero);
		Assert.That (WiserTemperatureFunctions.FromWiserTemp ((object?)null), Is.Zero);
		Assert.That (WiserTemperatureFunctions.FromWiserTemp (DBNull.Value), Is.Zero);
		Assert.That (WiserTemperatureFunctions.FromWiserTemp ((object)205L), Is.EqualTo (20.5));
		}

	[TestCase ("bad")]
	[TestCase (true)]
	public void UnsupportedTemperatureType_IsRejected (object value) =>
		Assert.Throws<ArgumentException> (() => WiserTemperatureFunctions.FromWiserTemp (value));

	[TestCase (0L, "00:00")]
	[TestCase (5L, "00:05")]
	[TestCase (630L, "06:30")]
	[TestCase (2359L, "23:59")]
	public void ScheduleTime_FormatsHubTime (long value, string expected) =>
		Assert.That (value.ToWiserTime (), Is.EqualTo (expected));

	[TestCase (-1L)]
	[TestCase (2400L)]
	public void ScheduleTime_RejectsOutOfRangeValues (long value) =>
		Assert.Throws<ArgumentOutOfRangeException> (() => value.ToWiserTime ());

	[TestCase ("00:00", 0)]
	[TestCase ("06:30", 390)]
	[TestCase ("23:59", 1439)]
	public void ScheduleTime_ParsesHoursAndMinutes (string value, int minutes) =>
		Assert.That (value.FromWiserTime (), Is.EqualTo (TimeSpan.FromMinutes (minutes)));

	[TestCase ("RoomStat", 17, 0)]
	[TestCase ("RoomStat", 22, 50)]
	[TestCase ("RoomStat", 27, 100)]
	[TestCase ("RoomStat", 30, 100)]
	[TestCase ("RoomStat", 10, 0)]
	[TestCase ("Unknown", 27, 0)]
	public void Battery_ConvertsVoltageAndClampsPercentage (string product, int voltage, int expected)
		{
		var battery = new WiserBattery (new Dictionary<string, object>
			{
			["ProductType"] = product,
			["BatteryLevel"] = "Normal",
			["BatteryVoltage"] = voltage
			});
		Assert.That (battery.Voltage, Is.EqualTo (voltage / 10.0));
		Assert.That (battery.Percent, Is.EqualTo (expected));
		}

	[Test]
	public void MissingTelemetry_HasSafeDefaults ()
		{
		var battery = new WiserBattery (new Dictionary<string, object> ());
		Assert.That (battery.Level, Is.EqualTo ("No Battery"));
		Assert.That (battery.Percent, Is.Zero);
		var signal = new WiserSignalStrength (new Dictionary<string, object> ());
		Assert.That (signal.ControllerReceptionRssi, Is.Null);
		Assert.That (signal.DeviceSignalStrength, Is.Null);
		var gps = new WiserGPS (new Dictionary<string, object> ());
		Assert.That (gps.Latitude, Is.Null);
		Assert.That (gps.Longitude, Is.Null);
		}

	[TestCase (-100, 0)]
	[TestCase (-75, 50)]
	[TestCase (-50, 100)]
	[TestCase (-40, 100)]
	public void SignalStrength_DecodesNestedReception (int rssi, int expected)
		{
		var data = new Dictionary<string, object>
			{
			["ReceptionOfController"] = new Dictionary<string, object> { ["Rssi"] = rssi, ["Lqi"] = 180 },
			["ReceptionOfDevice"] = new Dictionary<string, object> { ["Rssi"] = rssi, ["Lqi"] = 160 }
			};
		var signal = new WiserSignalStrength (data);
		Assert.That (signal.ControllerSignalStrength, Is.EqualTo (expected));
		Assert.That (signal.DeviceSignalStrength, Is.EqualTo (expected));
		Assert.That (signal.ControllerReceptionLqi, Is.EqualTo (180));
		Assert.That (signal.DeviceReceptionLqi, Is.EqualTo (160));
		}

	[Test]
	public void Capabilities_DefaultMissingFlagsAndReturnADictionaryCopy ()
		{
		var capabilities = new WiserHubCapabilitiesInfo (new Dictionary<string, object> { ["SmartPlug"] = true });
		Assert.That (capabilities.SmartPlug, Is.True);
		Assert.That (capabilities.Shutter, Is.False);
		capabilities.All["SmartPlug"] = false;
		Assert.That (capabilities.SmartPlug, Is.True);
		}

	[TestCase ("Connected", true)]
	[TestCase ("Disconnected", false)]
	public void Cloud_ReportsConnectionState (string status, bool expected) =>
		Assert.That (new WiserCloud (status, new Dictionary<string, object> ()).ConnectedToCloud, Is.EqualTo (expected));
	}