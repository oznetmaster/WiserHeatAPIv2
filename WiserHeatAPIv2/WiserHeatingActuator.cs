// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
#if HEATACTUATOR
	public class WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData) : WiserDevice (wiserRestController, data, deviceTypeData)
		{
		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
				 DeviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? ConvertInvariant.ToInt32 (setPoint) : Constants.TempOff);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			  DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? ConvertInvariant.ToInt32 (temp) : Constants.TempOff, "current");

		public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

		public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? ConvertInvariant.ToInt32 (power) : 0;

		public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TextUnknown;
		}

	public class WiserHeatingActuators
		{
		public List<WiserHeatingActuator> All { get; } = [];

		public int Count => All.Count;

		public WiserHeatingActuator GetById (int id) =>	All.FirstOrDefault (actuator => actuator.Id == id);
		}
#endif
	}
