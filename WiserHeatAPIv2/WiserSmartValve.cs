// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserSmartValve (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice(wiserRestController, data, deviceTypeData)
		{
		public WiserBattery Battery => new (Data);

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");

		public string? MountingOrientation => DeviceTypeData.TryGetValue ("MountingOrientation", out var orientation) ? orientation.ToString () : null;

		public int PercentageDemand => DeviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

		}

	public class WiserSmartValves
		{
		public List<WiserSmartValve> All { get; } = [];

		public int Count => All.Count;

		public WiserSmartValve GetById (int id) => All.FirstOrDefault (valve => valve.Id == id);
		}
	}
