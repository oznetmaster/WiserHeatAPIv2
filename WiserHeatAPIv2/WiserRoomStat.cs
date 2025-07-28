// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserRoomStat (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice(wiserRestController, data, deviceTypeData)
		{
		public WiserBattery Battery => new (Data);

		public int CurrentHumidity => DeviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? Convert.ToInt32 (humidity, CultureInfo.InvariantCulture) : 0;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");

		// Update all references to constants to use the new names (e.g., Constants.TextAuto, Constants.TextManual, etc.)
		// This includes: TempError, TempMinimum, TempMaximum, TempHwOn, TempHwOff, TempOff, RoomstatMinBatteryLevel, RoomstatFullBatteryLevel, TrvFullBatteryLevel, TrvMinBatteryLevel, MaxBoostIncrease, TextAuto, TextManual, TextOff, TextOn, TextNone, TextUnknown, etc.
		}

	public class WiserRoomStats
		{
		public List<WiserRoomStat> All { get; } = [];

		public int Count => All.Count;

		public WiserRoomStat GetById (int id) => All.FirstOrDefault (stat => stat.Id == id);
		}
	}
