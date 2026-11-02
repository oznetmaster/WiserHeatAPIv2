// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Wiser smart radiator valve (iTRV) device with temperature and demand information.
/// </summary>
/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
/// <param name="data">The hub data for this device or channel.</param>
/// <param name="deviceTypeData">The device-type-specific data returned by the hub.</param>
public class WiserSmartValve (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
	{
	/// <summary>Gets the battery information for this valve.</summary>
	/// <value>The battery information for this valve.</value>
	public WiserBattery Battery => new (Data);

	/// <summary>Gets the current target (setpoint) temperature in user units.</summary>
	/// <value>The current target (setpoint) temperature in user units.</value>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0, units: WiserRestController.Units);

	/// <summary>Gets the current measured temperature in user units.</summary>
	/// <value>The current measured temperature in user units.</value>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current", WiserRestController.Units);

	/// <summary>Gets the mounting orientation if provided by the device.</summary>
	/// <value>The mounting orientation if provided by the device.</value>
	public string? MountingOrientation => DeviceTypeData.GetNullableStringOr ("MountingOrientation");

	/// <summary>Gets the current percentage heat demand (0..100).</summary>
	/// <value>The current percentage heat demand (0..100).</value>
	public int PercentageDemand => DeviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

	}

/// <summary>
/// Collection helper for smart valves, providing lookup and counts.
/// </summary>
public class WiserSmartValves
	{
	/// <summary>Gets all smart valves discovered on the hub.</summary>
	/// <value>All smart valves discovered on the hub.</value>
	public List<WiserSmartValve> All { get; } = [];

	/// <summary>Gets the number of smart valves.</summary>
	/// <value>The number of smart valves.</value>
	public int Count => All.Count;

	/// <summary>Finds a smart valve by its device id.</summary>
	/// <param name="id">Device id.</param>
	/// <returns>The matching valve or null if not found.</returns>
	public WiserSmartValve? GetById (int id) => All.FirstOrDefault (valve => valve.Id == id);
	}