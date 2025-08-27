// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	/// <summary>
	/// Represents a Wiser smart radiator valve (iTRV) device with temperature and demand information.
	/// </summary>
	public class WiserSmartValve (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData) : WiserDevice(wiserRestController, data, deviceTypeData)
		{
		/// <summary>Gets the battery information for this valve.</summary>
		public WiserBattery Battery => new (Data);

		/// <summary>Gets the current target (setpoint) temperature in user units.</summary>
		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		/// <summary>Gets the current measured temperature in user units.</summary>
		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");

		/// <summary>Gets the mounting orientation if provided by the device.</summary>
		public string? MountingOrientation => DeviceTypeData.TryGetValue ("MountingOrientation", out var orientation) ? orientation.ToString () : null;

		/// <summary>Gets the current percentage heat demand (0..100).</summary>
		public int PercentageDemand => DeviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

		}

	/// <summary>
	/// Collection helper for smart valves, providing lookup and counts.
	/// </summary>
	public class WiserSmartValves
		{
		/// <summary>Gets all smart valves discovered on the hub.</summary>
		public List<WiserSmartValve> All { get; } = [];

		/// <summary>Gets the number of smart valves.</summary>
		public int Count => All.Count;

		/// <summary>Finds a smart valve by its device id.</summary>
		/// <param name="id">Device id.</param>
		/// <returns>The matching valve or null if not found.</returns>
		public WiserSmartValve GetById (int id) => All.FirstOrDefault (valve => valve.Id == id);
		}
	}
