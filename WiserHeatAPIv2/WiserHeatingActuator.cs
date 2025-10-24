// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

#if HEATACTUATOR
/// <summary>
/// Represents a heating actuator device with power telemetry and setpoints.
/// </summary>
public class WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
	{
	/// <summary>Gets the current occupied setpoint temperature.</summary>
	public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? ConvertInvariant.ToInt32 (setPoint) : Constants.TEMP_OFF);

	/// <summary>Gets the current measured temperature.</summary>
	public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
		  DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TEMP_OFF, "current");

	/// <summary>Gets the delivered energy summation value.</summary>
	public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

	/// <summary>Gets the current instantaneous power draw.</summary>
	public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

	/// <summary>Gets the output type of the actuator.</summary>
	public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;
	}

/// <summary>
/// Collection wrapper and lookup helpers for heating actuator devices.
/// </summary>
public class WiserHeatingActuators
	{
	/// <summary>Gets all heating actuators.</summary>
	public List<WiserHeatingActuator> All { get; } = [];

	/// <summary>Gets the number of actuators in the collection.</summary>
	public int Count => All.Count;

	/// <summary>Finds an actuator by its device id.</summary>
	public WiserHeatingActuator GetById (int id) =>	All.FirstOrDefault (actuator => actuator.Id == id);
	}
#endif

