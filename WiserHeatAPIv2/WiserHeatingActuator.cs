// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

#if HEATACTUATOR
/// <summary>
/// Represents a heating actuator device with power telemetry and setpoints.
/// </summary>
/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
/// <param name="data">The hub data used to populate this device or view.</param>
/// <param name="deviceTypeData">The device-type-specific data returned by the hub.</param>
public class WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
	{
	/// <summary>Gets the current occupied setpoint temperature.</summary>
	/// <value>The current occupied setpoint temperature.</value>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? ConvertInvariant.ToInt32 (setPoint) : Constants.TEMP_OFF, units: WiserRestController.Units);

	/// <summary>Gets the current measured temperature.</summary>
	/// <value>The current measured temperature.</value>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		  DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TEMP_OFF, "current", WiserRestController.Units);

	/// <summary>Gets the delivered energy summation value.</summary>
	/// <value>The delivered energy summation value.</value>
	public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

	/// <summary>Gets the current instantaneous power draw.</summary>
	/// <value>The current instantaneous power draw.</value>
	public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

	/// <summary>Gets the output type of the actuator.</summary>
	/// <value>The output type of the actuator.</value>
	public string? OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;
	}

/// <summary>
/// Collection wrapper and lookup helpers for heating actuator devices.
/// </summary>
public class WiserHeatingActuators
	{
	/// <summary>Gets all heating actuators.</summary>
	/// <value>All heating actuators.</value>
	public List<WiserHeatingActuator> All { get; } = [];

	/// <summary>Gets the number of actuators in the collection.</summary>
	/// <value>The number of actuators in the collection.</value>
	public int Count => All.Count;

	/// <summary>Finds an actuator by its device id.</summary>
	/// <param name="id">The device identifier to look up.</param>
	/// <returns>The first matching heating actuator, or <see langword="null"/> if none matches.</returns>
	public WiserHeatingActuator? GetById (int id) => All.FirstOrDefault (actuator => actuator.Id == id);
	}
#endif