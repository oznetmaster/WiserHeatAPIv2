// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Wiser room thermostat device with temperature and humidity sensors.
/// </summary>
/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
/// <param name="data">The hub data for this device or channel.</param>
/// <param name="deviceTypeData">The device-type-specific data returned by the hub.</param>
public class WiserRoomStat (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
	{
	/// <summary>Gets the battery information for this room stat.</summary>
	/// <value>The battery information for this room stat.</value>
	public WiserBattery Battery => new (Data);

	/// <summary>Gets the current measured humidity percentage.</summary>
	/// <value>The current measured humidity percentage.</value>
	public int CurrentHumidity => DeviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? ConvertInvariant.ToInt32 (humidity) : 0;

	/// <summary>Gets the current target (setpoint) temperature in user units.</summary>
	/// <value>The current target (setpoint) temperature in user units.</value>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0, units: WiserRestController.Units);

	/// <summary>Gets the current measured temperature in user units.</summary>
	/// <value>The current measured temperature in user units.</value>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current", WiserRestController.Units);
	}

/// <summary>
/// Collection helper for room stats, providing lookup and counts.
/// </summary>
public class WiserRoomStats
	{
	/// <summary>Gets all room stats.</summary>
	/// <value>All room stats.</value>
	public List<WiserRoomStat> All { get; } = [];

	/// <summary>Gets the number of room stats.</summary>
	/// <value>The number of room stats.</value>
	public int Count => All.Count;

	/// <summary>Finds a room stat by device id.</summary>
	/// <param name="id">The identifier to look up.</param>
	/// <returns>The first matching room thermostat, or <see langword="null"/> if none matches.</returns>
	public WiserRoomStat? GetById (int id) => All.FirstOrDefault (stat => stat.Id == id);
	}