// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

using WiserHeatApiV2;

using static System.FormattableString;

namespace WiserHeatingAPI
	{
	/// <summary>
	/// Wiser API Version 2
	/// 
	/// angelosantagata@gmail.com
	/// msparker@sky.com
	/// 
	/// https://github.com/asantaga/wiserheatingapi
	/// 
	/// This API allows you to get information from and control your wiserhub.
	/// </summary>
	// TODO: Keep objects and update instead of recreating on hub update
	// TODO: Update entity values after commend issued to get current values
	public class WiserAPI : IDisposable
		{
		// Update the logger initialization to ensure the `AddConsole` method is recognized.
		private static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserAPI));

		private const string VERSION = "1.0.0"; // Assuming a version, replace with actual version

		// Connection variables
		private WiserConnection _wiserApiConnection;
		private WiserRestController? _wiserRestController;

		// Hub Data
		private Dictionary<string, object> _domainData = new Dictionary<string, object> ();
		private Dictionary<string, object> _networkData = new Dictionary<string, object> ();
		private Dictionary<string, object> _scheduleData = new Dictionary<string, object> ();
		private Dictionary<string, object> _openthermData = new Dictionary<string, object> ();

		// Data stores for exposed properties
		private WiserDevices? _devices;
		private WiserHotwater? _hotwater;
		private WiserHeatingChannels? _heatingChannels;
		private WiserMoments? _moments;
		private WiserRooms? _rooms;
		private WiserSchedules? _schedules;
		private WiserSystem? _system;

		/// <summary>
		/// Main api class to access all entities and attributes of wiser system
		/// </summary>
		public WiserAPI (string? host, string? secret, WiserUnits units = WiserUnits.Metric)
			{
			var logger = ((log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_LOGGER).Logger);
#if DEBUG
			logger.Level = log4net.Core.Level.Debug;
#else
			logger.Level = log4net.Core.Level.Error;
#endif

			// Connection variables
			_wiserApiConnection = new WiserConnection (host, secret) { Units = units };
			_wiserRestController = null;

			// Log initialization info
			_LOGGER.InfoFormat (
				CultureInfo.InvariantCulture,
				"WiserHub API v{0} Initialised - Host: {1}, Units: {2}",
				VERSION,
				host,
				_wiserApiConnection.Units.ToString ().ToTitleCase ()
			);

			// Read hub _data if hub IP and secret exist
			if (!string.IsNullOrEmpty (_wiserApiConnection.Host) && !string.IsNullOrEmpty (_wiserApiConnection.Secret))
				{
				_wiserRestController = new WiserRestController (_wiserApiConnection);
				}
			else
				throw new WiserHubConnectionException ("Missing or incomplete connection information");
			}

		public async Task InitializeAsync (CancellationToken cancellationToken = default)
			{
			try
				{
				if (await ReadHubDataAsync (cancellationToken).ConfigureAwait (false))
					return;
				_LOGGER.Error ("Failed to read hub _data. Please check your connection settings.");
				// If we reach here, it means the hub _data could not be read
				throw new WiserHubConnectionException ("Failed to read hub _data. Please check your connection settings.");
				}
			catch (Exception ex)
				{
				_LOGGER.Error ("Error initializing Wiser API", ex);
				throw new WiserHubConnectionException ($"Failed to initialize Wiser API: {ex.Message}");
				}
			}

		/// <summary>
		/// Read all _data from hub and populate objects
		/// </summary>
		public async Task<bool> ReadHubDataAsync (CancellationToken cancellationToken = default)
			{
			try
				{
				if (_wiserRestController == null)
					{
					_LOGGER.Error ("WiserRestController is not initialized. Cannot read hub _data.");
					return false;
					}
				// Read _data from hub
				var newDomainData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubDomain, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				var newNetworkData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubNetwork, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				var newScheduleData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubSchedules, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				var newOpenthermData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubOpentherm, _wiserApiConnection.Host), false, cancellationToken: cancellationToken).ConfigureAwait (false);

				// Update internal _data
				_domainData = newDomainData;
				_networkData = newNetworkData;
				_scheduleData = newScheduleData;
				_openthermData = newOpenthermData;
				}
			catch (Exception ex)
				{
				_LOGGER.Error (ex);
				return false;
				}

			if (_domainData.Count > 0 && _networkData.Count > 0 && _scheduleData.Count > 0)
				{
				if (!(_domainData.TryGetValue ("Device", out var deviceDataObj) && deviceDataObj is List<Dictionary<string, object>> deviceData))
					deviceData = new List<Dictionary<string, object>> ();

				// Keep objects and update instead of recreating on hub update
				if (_system != null)
					_system.Update (_domainData, _networkData, deviceData, _openthermData);
				else
					_system = new WiserSystem (_wiserRestController, _domainData, _networkData, deviceData, _openthermData);

				if (_schedules != null)
					_schedules.Update (_scheduleData, _system.SunriseTimes, _system.SunsetTimes);
				else
					_schedules = new WiserSchedules (_wiserRestController, _scheduleData, _system.SunriseTimes, _system.SunsetTimes);

				if (_devices != null)
					_devices.Update (_domainData, _schedules);
				else
					_devices = new WiserDevices (_wiserRestController, _domainData, _schedules);

				if (!(_domainData.TryGetValue ("Room", out var roomDataObj) && roomDataObj is List<Dictionary<string, object>> roomData))
					roomData = new List<Dictionary<string, object>> ();

				if (_rooms != null)
					_rooms.Update (roomData, _schedules, _devices);
				else
					_rooms = new WiserRooms (_wiserRestController, roomData, _schedules, _devices);

				if (_domainData.TryGetValue ("HotWater", out var hotWaterDataObj) && hotWaterDataObj is List<Dictionary<string, object>> hotWaterData)
					{
					if (hotWaterData.Count > 0)
						{
						int scheduleId;
						// If there are multiple hot water _data items, use the first one
						if (hotWaterData.Count > 1)
							_LOGGER.Warn ($"Multiple hot water _data items found, using the first one. Count: {hotWaterData.Count}");
						var firstHotWaterData = hotWaterData[0];
						// If ScheduleId is not present, default to 0
						// Check if ScheduleId exists in the first hot water _data item
						if (firstHotWaterData.TryGetValue ("ScheduleId", out var scheduleIdObj))
							scheduleId = Convert.ToInt32 (scheduleIdObj, CultureInfo.InvariantCulture);
						else
							scheduleId = 0;
						var schedule = _schedules.GetById (WiserScheduleType.OnOff, scheduleId);
						if (schedule == null)
							{
							_LOGGER.Warn ($"No schedule found for Hot Water with ScheduleId: {scheduleId}. Using default schedule.");
							}
						if (_hotwater != null)
							_hotwater.Update (firstHotWaterData, schedule);
						else
							_hotwater = new WiserHotwater (_wiserRestController, firstHotWaterData, schedule);
						}
					}

				if (_domainData.TryGetValue ("HeatingChannel", out var heatingChannelDataObj) && heatingChannelDataObj is List<Dictionary<string, object>> heatingChannelData)
					{
					if (_heatingChannels != null)
						_heatingChannels.Update (heatingChannelData, _rooms);
					else
						_heatingChannels = new WiserHeatingChannels (heatingChannelData, _rooms);
					}

				if (_domainData.TryGetValue ("Moment", out var momentDataObj) && momentDataObj is List<Dictionary<string, object>> momentData)
					{
					if (_moments != null)
						_moments.Update (momentData);
					else
						_moments = new WiserMoments (_wiserRestController, momentData);
					}

				// If gets here with no exceptions then success and return true
				return true;
				}

#pragma warning disable CA1305 // Specify IFormatProvider
			_LOGGER.DebugFormat (Invariant ($"No update: _domainData: {_domainData.Count}, _networkData: {_networkData.Count}, _scheduleData: {_scheduleData.Count}"));
#pragma warning restore CA1305 // Specify IFormatProvider
			return false;
			}

		// API properties
		/// <summary>
		/// List of device entities attached to the Wiser Hub
		/// </summary>
		public WiserDevices? Devices => _devices;

		/// <summary>
		/// List of heating channel entities on the Wiser Hub
		/// </summary>
		public WiserHeatingChannels? HeatingChannels => _heatingChannels;

		/// <summary>
		/// List of hot water entities on the Wiser Hub
		/// </summary>
		public WiserHotwater? Hotwater => _hotwater;

		/// <summary>
		/// List of moment entities on the Wiser Hub
		/// </summary>
		public WiserMoments? Moments => _moments;

		/// <summary>
		/// List of room entities configured on the Wiser Hub
		/// </summary>
		public WiserRooms? Rooms => _rooms;

		/// <summary>
		/// List of schedules
		/// </summary>
		public WiserSchedules? Schedules => _schedules;

		/// <summary>
		/// Entity of the Wiser Hub
		/// </summary>
		public WiserSystem? System => _system;

		/// <summary>
		/// Get or set units for temperature
		/// </summary>
		public WiserUnits Units
			{
			get => _wiserApiConnection.Units;
			set => _wiserApiConnection.Units = value;
			}

		/// <summary>
		/// API Version
		/// </summary>
		public static string Version => VERSION;

		/// <summary>
		/// Raw hub _data
		/// </summary>
		public Dictionary<string, object> RawHubData => new Dictionary<string, object>
			{
			["Domain"] = _domainData,
			["Network"] = _networkData,
			["Schedule"] = _scheduleData,
			["OpenTherm"] = _openthermData
			};

		/// <summary>
		/// Output raw hub _data to json file
		/// </summary>
		public async Task<bool> OutputRawHubDataAsync (string dataClass, string filename, string filePath, CancellationToken cancellationToken = default)
			{
			// Get correct endpoint
			string? endpoint = null;
			if (dataClass.Equals ("domain", StringComparison.OrdinalIgnoreCase))
				endpoint = string.Format(CultureInfo.InvariantCulture, RestConstants.WiserHubDomain, _wiserApiConnection.Host);
			else if (dataClass.Equals ("network", StringComparison.OrdinalIgnoreCase))
				endpoint = string.Format(CultureInfo.InvariantCulture, RestConstants.WiserHubNetwork, _wiserApiConnection.Host);
			else if (dataClass.Equals ("schedules", StringComparison.OrdinalIgnoreCase))
				endpoint = string.Format(CultureInfo.InvariantCulture, RestConstants.WiserHubSchedules, _wiserApiConnection.Host);
			else if (dataClass.Equals ("opentherm", StringComparison.OrdinalIgnoreCase))
				endpoint = string.Format(CultureInfo.InvariantCulture, RestConstants.WiserHubOpentherm, _wiserApiConnection.Host);
			else
				{
				_LOGGER.Error ($"Invalid _data class: {dataClass}. Valid options are 'domain', 'network', 'schedules', or 'opentherm'.");
				return false;
				}

			// Get raw json _data
			if (_wiserRestController != null)
				{
				var data = (Dictionary<string, object>)await _wiserRestController.GetHubDataAsync (endpoint, cancellationToken: cancellationToken).ConfigureAwait (false);
				try
					{
					if (data != null && data.Count > 0)
						{
						// Write out to file
						await LogResponseToFileAsync (data, filename, Path.Combine (filePath)).ConfigureAwait (false);
						return true;
						}
					}
				catch (Exception ex)
					{
					_LOGGER.Error (ex.Message);
					return false;
					}
				}
			return false;
			}

		private static void LogResponseToFile (Dictionary<string, object> data, string filename, string filePath)
			{
			string fullPath = Path.Combine (filePath, filename);
			string jsonData = JsonConvert.SerializeObject (data, new JsonSerializerSettings { Formatting = Formatting.Indented });
			File.WriteAllText (fullPath, jsonData);
			}

		private static async Task LogResponseToFileAsync (Dictionary<string, object> data, string filename, string filePath)
			{
			string fullPath = Path.Combine (filePath, filename);
			string jsonData = JsonConvert.SerializeObject (data, new JsonSerializerSettings { Formatting = Formatting.Indented });

			using (var stream = new FileStream (fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
			using (var writer = new StreamWriter (stream))
				{
				await writer.WriteAsync (jsonData).ConfigureAwait (false);
				}
			}

		public void Dispose ()
			{
			// Dispose of managed resources
			if (_wiserRestController != null)
				{
				_wiserRestController.Dispose ();
				_wiserRestController = null;
				}

			// Dispose of unmanaged resources if any
			// (e.g., close any open connections, files, etc.)
			// Add any additional cleanup code here
			GC.SuppressFinalize (this);
			}
		}

	public static class StringExtensions
		{
		public static string ToTitleCase (this string input)
			{
			if (string.IsNullOrEmpty (input))
				return input;

			return $"{char.ToUpper (input[0], CultureInfo.InvariantCulture)}{input.Substring (1).ToLower (CultureInfo.InvariantCulture)}";
			}
		}

	// Constants
	public static class WiserConstants
		{
		public const double DEFAULTAWAYMODETEMP = 16.0;
		public const double DEFAULTDEGRADEDTEMP = 18.0;
		public const int MAXBOOSTINCREASE = 5;
		public const double TEMPERROR = -1;
		public const double TEMPHWON = -20;
		public const double TEMPHWOFF = -20.5;
		public const double TEMPMINIMUM = 5;
		public const double TEMPMAXIMUM = 30;
		public const double TEMPOFF = -20;
		public const string WISERHUBDOMAIN = "domain";
		public const string WISERHUBNETWORK = "network";
		public const string WISERHUBSCHEDULES = "schedules";
		public const string WISERHUBOPENTHERM = "opentherm";
		}

	// Exceptions
	public class WiserHubConnectionException : Exception
		{
		public WiserHubConnectionException (string message) : base (message) { }
		}
	}
