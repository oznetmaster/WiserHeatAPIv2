using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
#if HEATACTUATOR
	public class WiserHeatingActuator : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERHEATINGACTUATOR, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_OFF);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF, "current");

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : 0;

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new { DeviceLockEnabled = value }, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new { Identify = value }, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : 0;

		public string OutputType => _deviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserHeatingActuatorCollection
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
