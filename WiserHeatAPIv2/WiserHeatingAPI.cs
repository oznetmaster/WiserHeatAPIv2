// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

using static System.FormattableString;

namespace WiserHeatApiV2
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
		private static readonly ILog _lOGGER = LogManager.GetLogger (typeof (WiserAPI));

		private const string _vERSION = "1.0.0"; // Assuming a version, replace with actual version

		// Connection variables
		private readonly WiserConnection _wiserApiConnection;
		private WiserRestController? _wiserRestController;

		// Hub Data
		private Dictionary<string, object> _domainData = [];
		private Dictionary<string, object> _networkData = [];
		private Dictionary<string, object> _scheduleData = [];
		private Dictionary<string, object> _openthermData = [];

		/// <summary>
		/// Main api class to access all entities and attributes of wiser system
		/// </summary>
		public WiserAPI (string? host, string? secret, WiserUnits units = WiserUnits.Metric)
			{
			var logger = (log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_lOGGER).Logger;
#if DEBUG
			logger.Level = log4net.Core.Level.Debug;
#else
			logger.Level = log4net.Core.Level.Error;
#endif

			// Connection variables
			_wiserApiConnection = new WiserConnection (host, secret) { Units = units };
			_wiserRestController = null;

			// Log initialization info
			_lOGGER.InfoFormat (
				CultureInfo.InvariantCulture,
				"WiserHub API v{0} Initialised - Host: {1}, Units: {2}",
				_vERSION,
				host,
				_wiserApiConnection.Units.ToString ().Title ()
			);

			// Read hub data if hub IP and secret exist
			_wiserRestController = !string.IsNullOrEmpty (_wiserApiConnection.Host) && !string.IsNullOrEmpty (_wiserApiConnection.Secret)
				? new WiserRestController (_wiserApiConnection)
				: throw new WiserHubConnectionException ("Missing or incomplete connection information");
			}

		public async Task InitializeAsync (CancellationToken cancellationToken = default)
			{
			try
				{
				if (await ReadHubDataAsync (cancellationToken).ConfigureAwait (false))
					return;
				_lOGGER.Error ("Failed to read hub data. Please check your connection settings.");
				// If we reach here, it means the hub data could not be read
				throw new WiserHubConnectionException ("Failed to read hub data. Please check your connection settings.");
				}
			catch (Exception ex)
				{
				_lOGGER.Error ("Error initializing Wiser API", ex);
				throw new WiserHubConnectionException ($"Failed to initialize Wiser API: {ex.Message}");
				}
			}

		/// <summary>
		/// Read all data from hub and populate objects
		/// </summary>
		public async Task<bool> ReadHubDataAsync (CancellationToken cancellationToken = default)
			{
			try
				{
				if (_wiserRestController == null)
					{
					_lOGGER.Error ("WiserRestController is not initialized. Cannot read hub data.");
					return false;
					}
				// Read data from hub
				Dictionary<string, object> newDomainData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubDomain, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newNetworkData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubNetwork, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newScheduleData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubSchedules, _wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newOpenthermData = await _wiserRestController.GetHubDataAsync (string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubOpentherm, _wiserApiConnection.Host), false, cancellationToken: cancellationToken).ConfigureAwait (false);

				// Update internal data
				_domainData = newDomainData;
				_networkData = newNetworkData;
				_scheduleData = newScheduleData;
				_openthermData = newOpenthermData;
				}
			catch (Exception ex)
				{
				_lOGGER.Error (ex);
				return false;
				}

			if (_domainData.Count > 0 && _networkData.Count > 0 && _scheduleData.Count > 0)
				{
				if (!(_domainData.TryGetValue ("Device", out var deviceDataObj) && deviceDataObj is List<Dictionary<string, object>> deviceData))
					deviceData = [];

				// Keep objects and update instead of recreating on hub update
				if (System != null)
					System.Update (_domainData, _networkData, deviceData, _openthermData);
				else
					System = new WiserSystem (_wiserRestController, _domainData, _networkData, deviceData, _openthermData);

				if (Schedules != null)
					Schedules.Update (_scheduleData, System.SunriseTimes, System.SunsetTimes);
				else
					Schedules = new WiserSchedules (_wiserRestController, _scheduleData, System.SunriseTimes, System.SunsetTimes);

				if (Devices != null)
					Devices.Update (_domainData, Schedules);
				else
					Devices = new WiserDevices (_wiserRestController, _domainData, Schedules);

				if (!(_domainData.TryGetValue ("Room", out var roomDataObj) && roomDataObj is List<Dictionary<string, object>> roomData))
					roomData = [];

				if (Rooms != null)
					Rooms.Update (roomData, Schedules, Devices);
				else
					Rooms = new WiserRooms (_wiserRestController, roomData, Schedules, Devices);

				if (_domainData.TryGetValue ("HotWater", out var hotWaterDataObj) && hotWaterDataObj is List<Dictionary<string, object>> hotWaterData)
					{
					if (hotWaterData.Count > 0)
						{
						int scheduleId;
						// If there are multiple hot water data items, use the first one
						if (hotWaterData.Count > 1)
							_lOGGER.Warn ($"Multiple hot water data items found, using the first one. Count: {hotWaterData.Count}");
						Dictionary<string, object> firstHotWaterData = hotWaterData[0];
						// If ScheduleId is not present, default to 0
						// Check if ScheduleId exists in the first hot water data item
						scheduleId = firstHotWaterData.TryGetValue ("ScheduleId", out var scheduleIdObj)
							? Convert.ToInt32 (scheduleIdObj, CultureInfo.InvariantCulture)
							: 0;
						WiserSchedule? schedule = Schedules.GetById (WiserScheduleType.OnOff, scheduleId);
						if (schedule == null)
							_lOGGER.Warn ($"No schedule found for Hot Water with ScheduleId: {scheduleId}. Using default schedule.");

						if (Hotwater != null)
							Hotwater.Update (firstHotWaterData, schedule);
						else
							Hotwater = new WiserHotwater (_wiserRestController, firstHotWaterData, schedule);
						}
					}

				if (_domainData.TryGetValue ("HeatingChannel", out var heatingChannelDataObj) && heatingChannelDataObj is List<Dictionary<string, object>> heatingChannelData)
					{
					if (HeatingChannels != null)
						HeatingChannels.Update (heatingChannelData, Rooms);
					else
						HeatingChannels = new WiserHeatingChannels (heatingChannelData, Rooms);
					}

				if (_domainData.TryGetValue ("Moment", out var momentDataObj) && momentDataObj is List<Dictionary<string, object>> momentData)
					{
					if (Moments != null)
						Moments.Update (momentData);
					else
						Moments = new WiserMoments (_wiserRestController, momentData);
					}

				// If gets here with no exceptions then success and return true
				return true;
				}

			_lOGGER.DebugFormat (
				 CultureInfo.InvariantCulture,
				 "No update: _domainData: {0}, _networkData: {1}, _scheduleData: {2}",
				 _domainData.Count, _networkData.Count, _scheduleData.Count
			);
			return false;
			}

		// API properties
		/// <summary>
		/// List of device entities attached to the Wiser Hub
		/// </summary>
		public WiserDevices? Devices { get; private set; }

		/// <summary>
		/// List of heating channel entities on the Wiser Hub
		/// </summary>
		public WiserHeatingChannels? HeatingChannels { get; private set; }

		/// <summary>
		/// List of hot water entities on the Wiser Hub
		/// </summary>
		public WiserHotwater? Hotwater { get; private set; }

		/// <summary>
		/// List of moment entities on the Wiser Hub
		/// </summary>
		public WiserMoments? Moments { get; private set; }

		/// <summary>
		/// List of room entities configured on the Wiser Hub
		/// </summary>
		public WiserRooms? Rooms { get; private set; }

		/// <summary>
		/// List of schedules
		/// </summary>
		public WiserSchedules? Schedules { get; private set; }

		/// <summary>
		/// Entity of the Wiser Hub
		/// </summary>
		public WiserSystem? System { get; private set; }

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
		public static string Version => _vERSION;

		/// <summary>
		/// Raw hub data
		/// </summary>
		public Dictionary<string, object> RawHubData => new ()
			{
			["Domain"] = _domainData,
			["Network"] = _networkData,
			["Schedule"] = _scheduleData,
			["OpenTherm"] = _openthermData
			};

		private static readonly Dictionary<string, string> _dataClassToEndpoint = new (StringComparer.OrdinalIgnoreCase)
{
	 { "domain",    RestConstants.WiserHubDomain },
	 { "network",   RestConstants.WiserHubNetwork },
	 { "schedules", RestConstants.WiserHubSchedules },
	 { "opentherm", RestConstants.WiserHubOpentherm }
};

		/// <summary>
		/// Output raw hub data to json file
		/// </summary>
		public async Task<bool> OutputRawHubDataAsync (string dataClass, string filename, string filePath, CancellationToken cancellationToken = default)
			{
			// Get correct endpoint
			var endpoint = _dataClassToEndpoint.TryGetValue (dataClass, out var endpointFormat)
				? string.Format (CultureInfo.InvariantCulture, endpointFormat, _wiserApiConnection.Host)
				: null;

			if (endpoint == null)
				{
				_lOGGER.Error ($"Invalid data class: {dataClass}. Valid options are 'domain', 'network', 'schedules', or 'opentherm'.");
				return false;
				}

			// Get raw json data
			if (_wiserRestController != null)
				{
				Dictionary<string, object> data = await _wiserRestController.GetHubDataAsync (endpoint, cancellationToken: cancellationToken).ConfigureAwait (false);
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
					_lOGGER.Error (ex.Message);
					return false;
					}
				}

			return false;
			}

		private static void LogResponseToFile (Dictionary<string, object> data, string filename, string filePath)
			{
			var fullPath = Path.Combine (filePath, filename);
			var jsonData = JsonConvert.SerializeObject (data, new JsonSerializerSettings { Formatting = Formatting.Indented });
			File.WriteAllText (fullPath, jsonData);
			}

		private static async Task LogResponseToFileAsync (Dictionary<string, object> data, string filename, string filePath)
			{
			var fullPath = Path.Combine (filePath, filename);
			var jsonData = JsonConvert.SerializeObject (data, new JsonSerializerSettings { Formatting = Formatting.Indented });

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
	}
