// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
namespace WiserHeatApiV2
	{
#if OPENTHERM
	public class WiserOpentherm (IDictionary<string, object> data, string enabledStatus)
		{
		internal readonly IDictionary<string, object> _data = data;

		public double ChFlowActiveLowerSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveLowerSetpoint", out var value) ? value : null, "current");

		public double ChFlowActiveUpperSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveUpperSetpoint", out var value) ? value : null, "current");

		public bool Ch1FlowEnabled => _data.TryGetValue ("ch1FlowEnable", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public double Ch1FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch1FlowSetpoint", out var value) ? value : null, "current");

		public bool Ch2FlowEnabled => _data.TryGetValue ("ch2FlowEnable", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public double Ch2FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch2FlowSetpoint", out var value) ? value : null, "current");

		public string ConnectionStatus { get; } = enabledStatus;

		public bool Enabled => _data.TryGetValue ("Enabled", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public bool HwEnabled => _data.TryGetValue ("dhwEnable", out var value) && Convert.ToBoolean (value, CultureInfo.InvariantCulture);

		public double HwFlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("dhwFlowSetpoint", out var value) ? value : null, "current");

		public string? OperatingMode => _data.TryGetValue ("operatingMode", out var value) ? value.ToString () : null;

		public WiserOpenThermOperationalData OperationalData => _data.TryGetValue ("operationalData", out var data) && data is Dictionary<string, object> dataDict
					? new WiserOpenThermOperationalData (dataDict)
					: new WiserOpenThermOperationalData ([]);

		public WiserOpenThermBoilerParameters BoilerParameters => _data.TryGetValue ("preDefinedRemoteBoilerParameters", out var data) && data is Dictionary<string, object> dataDict
					? new WiserOpenThermBoilerParameters (dataDict)
					: new WiserOpenThermBoilerParameters ([]);

		public double RoomSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomSetpoint", out var value) ? value : null, "current");

		public double RoomTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomTemperature", out var value) ? value : null, "current");

		public int? TrackedRoomId => _data.TryGetValue ("TrackedRoomId", out var value) ? (int?)Convert.ToInt32 (value, CultureInfo.InvariantCulture) : null;
		}

	public class WiserOpenThermBoilerParameters (Dictionary<string, object> data)
		{
		public bool? HwSetpointTransferEnable => data.TryGetValue ("dhwSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value, CultureInfo.InvariantCulture) : null;

		public bool? ChSetpointTransferEnable => data.TryGetValue ("maxChSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value, CultureInfo.InvariantCulture) : null;

		public bool? HwSetpointReadWrite => data.TryGetValue ("dhwSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value, CultureInfo.InvariantCulture) : null;

		public bool? ChSetpointReadWrite => data.TryGetValue ("maxChSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value, CultureInfo.InvariantCulture) : null;
		}

	public class WiserOpenThermOperationalData (Dictionary<string, object> data)
		{
		public double ChPressureBar => data.TryGetValue ("ChPressureBar", out var value) ? Convert.ToDouble (value, CultureInfo.InvariantCulture) / 10 : 0;

		public double ChFlowTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 data.TryGetValue ("Ch1FlowTemperature", out var value) ? value : (int?)null, "current");

		public double ChReturnTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 data.TryGetValue ("ChReturnTemperature", out var value) ? value : (int?)null, "current");

		public double HwTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 data.TryGetValue ("Dhw1Temperature", out var value) ? value : (int?)null, "current");

		public int? RelativeModulationLevel => data.TryGetValue ("RelativeModulationLevel", out var value) ? (int?)Convert.ToInt32 (value, CultureInfo.InvariantCulture) : null;

		public int? SlaveStatus => data.TryGetValue ("SlaveStatus", out var value) ? (int?)Convert.ToInt32 (value, CultureInfo.InvariantCulture) : null;
		}
#endif
	}

// -----

