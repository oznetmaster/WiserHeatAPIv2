// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
#if HEATACTUATOR
	public class WiserHeatingActuator : WiserDevice
		{
		public WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
             DeviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint, CultureInfo.InvariantCulture) : Constants.TempOff);

        public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
             DeviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp, CultureInfo.InvariantCulture) : Constants.TempOff, "current");

		public int DeliveredPower => DeviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power, CultureInfo.InvariantCulture) : 0;

		public int InstantaneousPower => DeviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power, CultureInfo.InvariantCulture) : 0;

		public string OutputType => DeviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TextUnknown;
		}

	public class WiserHeatingActuators
		{
		private readonly List<WiserHeatingActuator> _heatingActuators = new List<WiserHeatingActuator> ();

		public List<WiserHeatingActuator> All => _heatingActuators;

		public int Count => _heatingActuators.Count;

		public WiserHeatingActuator GetById (int id)
			{
			return _heatingActuators.FirstOrDefault (actuator => actuator.Id == id);
			}
		}
#endif
	}
