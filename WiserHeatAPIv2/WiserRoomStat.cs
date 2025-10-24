// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Wiser room thermostat device with temperature and humidity sensors.
/// </summary>
public class WiserRoomStat (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice(wiserRestController, data, deviceTypeData)
	{
	/// <summary>Gets the battery information for this room stat.</summary>
	public WiserBattery Battery => new (Data);

	/// <summary>Gets the current measured humidity percentage.</summary>
	public int CurrentHumidity => DeviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? ConvertInvariant.ToInt32 (humidity) : 0;

	/// <summary>Gets the current target (setpoint) temperature in user units.</summary>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

	/// <summary>Gets the current measured temperature in user units.</summary>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");
	}

/// <summary>
/// Collection helper for room stats, providing lookup and counts.
/// </summary>
public class WiserRoomStats
	{
	/// <summary>Gets all room stats.</summary>
	public List<WiserRoomStat> All { get; } = [];

	/// <summary>Gets the number of room stats.</summary>
	public int Count => All.Count;

	/// <summary>Finds a room stat by device id.</summary>
	public WiserRoomStat GetById (int id) => All.FirstOrDefault (stat => stat.Id == id);
	}

