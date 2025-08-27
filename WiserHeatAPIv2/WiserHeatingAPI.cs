// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;

using static WiserHeatApiV2.RestConstants;

namespace WiserHeatApiV2
	{
	/// <summary>
	/// Main Wiser API facade providing access to hub state, entities and control operations.
	/// This is the primary entry point for interacting with a Wiser heating system.
	/// </summary>
	/// <remarks>
	/// The API automatically reads and synchronizes with hub data during initialization.
	/// Call <see cref="ReadHubDataAsync"/> periodically to refresh entity state.
	/// </remarks>
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
		/// Initializes a new instance of the <see cref="WiserAPI"/> class with connection parameters.
		/// </summary>
		/// <param name="host">IP address or hostname of the Wiser hub.</param>
		/// <param name="secret">Secret key for hub authentication (obtained from the hub's display or app).</param>
		/// <param name="units">Preferred temperature unit system for all API operations.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="host"/> or <paramref name="secret"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="WiserHubConnectionException">Thrown when connection information is missing or empty.</exception>
		/// <remarks>
		/// After creating an instance, call <see cref="InitializeAsync"/> to establish communication and read hub data.
		/// Both <paramref name="host"/> and <paramref name="secret"/> must be non-empty.
		/// </remarks>
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
			_lOGGER.InfoFormatInvariant (
				"WiserHub API v{0} Initialised - Host: {1}, Units: {2}",
				_vERSION,
				host,
				_wiserApiConnection.Units.ToString ().TitleCase ()
			);

			// Read hub data if hub IP and secret exist
			_wiserRestController = !string.IsNullOrEmpty (_wiserApiConnection.Host) && !string.IsNullOrEmpty (_wiserApiConnection.Secret)
				? new WiserRestController (_wiserApiConnection)
				: throw new WiserHubConnectionException ("Missing or incomplete connection information");
			}

		/// <summary>
		/// Establishes communication with the hub and performs initial data synchronization.
		/// This method must be called after construction before using any API properties.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token to observe.</param>
		/// <returns>A task that represents the asynchronous initialization operation.</returns>
		/// <exception cref="WiserHubConnectionException">
		/// Thrown when initialization fails (underlying errors are caught and wrapped).
		/// </exception>
		/// <remarks>
		/// This method populates all API properties (Rooms, Devices, System, etc.) with current hub data.
		/// Any underlying exceptions are caught and rethrown as <see cref="WiserHubConnectionException"/>.
		/// </remarks>
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
		/// Retrieves the latest data from the hub and updates all API entities accordingly.
		/// Call this method periodically to refresh entity state.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token to observe.</param>
		/// <returns>
		/// A task that represents the asynchronous operation. The task result is <see langword="true"/> if all required hub
		/// data was retrieved and processed successfully; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method updates existing entity instances rather than replacing them, preserving object references.
		/// Errors are logged and the method returns <see langword="false"/>; exceptions are not propagated.
		/// The OpenTherm endpoint is optional; failures are suppressed during retrieval.
		/// </remarks>
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
				Dictionary<string, object> newDomainData = await _wiserRestController.GetHubDataAsync (WiserHubDomain.FormatInvariant (_wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newNetworkData = await _wiserRestController.GetHubDataAsync (WiserHubNetwork.FormatInvariant (_wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newScheduleData = await _wiserRestController.GetHubDataAsync (WiserHubSchedules.FormatInvariant (_wiserApiConnection.Host), cancellationToken: cancellationToken).ConfigureAwait (false);
				Dictionary<string, object> newOpenthermData = await _wiserRestController.GetHubDataAsync (WiserHubOpentherm.FormatInvariant (_wiserApiConnection.Host), raiseForEndpointError: false, cancellationToken: cancellationToken).ConfigureAwait (false);

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
							? ConvertInvariant.ToInt32 (scheduleIdObj)
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

			_lOGGER.DebugFormatInvariant (
				 "No update: _domainData: {0}, _networkData: {1}, _scheduleData: {2}",
				 _domainData.Count.ToStringInvariant (), _networkData.Count.ToStringInvariant (), _scheduleData.Count.ToStringInvariant ()
			);
			return false;
			}

		// API properties
		/// <summary>
		/// Gets the collection of devices (sensors, actuators, smart plugs, etc.) connected to the Wiser hub.
		/// </summary>
		/// <value>A <see cref="WiserDevices"/> instance providing access to all hub devices, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserDevices? Devices { get; private set; }

		/// <summary>
		/// Gets the collection of heating channels configured on the Wiser hub.
		/// </summary>
		/// <value>A <see cref="WiserHeatingChannels"/> instance providing access to heating channel state, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserHeatingChannels? HeatingChannels { get; private set; }

		/// <summary>
		/// Gets the hot water controller entity, if configured on the hub.
		/// </summary>
		/// <value>A <see cref="WiserHotwater"/> instance for hot water control, or <see langword="null"/> if no hot water is configured or not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserHotwater? Hotwater { get; private set; }

		/// <summary>
		/// Gets the collection of Moment entities (energy monitoring) available on the hub.
		/// </summary>
		/// <value>A <see cref="WiserMoments"/> instance providing access to energy data, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserMoments? Moments { get; private set; }

		/// <summary>
		/// Gets the collection of rooms configured on the Wiser hub.
		/// </summary>
		/// <value>A <see cref="WiserRooms"/> instance providing access to all rooms and their controls, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserRooms? Rooms { get; private set; }

		/// <summary>
		/// Gets the collection of schedules configured on the Wiser hub.
		/// </summary>
		/// <value>A <see cref="WiserSchedules"/> instance providing access to heating, on/off, and level schedules, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserSchedules? Schedules { get; private set; }

		/// <summary>
		/// Gets the hub system information, including firmware, network status, and global settings.
		/// </summary>
		/// <value>A <see cref="WiserSystem"/> instance with hub metadata and system-level controls, or <see langword="null"/> if not yet initialized.</value>
		/// <remarks>Call <see cref="InitializeAsync"/> to populate this property.</remarks>
		public WiserSystem? System { get; private set; }

		/// <summary>
		/// Gets or sets the preferred temperature unit system for all API operations.
		/// </summary>
		/// <value>The unit system used for temperature values throughout the API.</value>
		/// <remarks>
		/// Changing this property affects temperature values returned by all entities.
		/// Existing temperature values are converted automatically.
		/// </remarks>
		public WiserUnits Units
			{
			get => _wiserApiConnection.Units;
			set => _wiserApiConnection.Units = value;
			}

		/// <summary>
		/// Gets the API version string.
		/// </summary>
		/// <value>A string representing the current API version.</value>
		public static string Version => _vERSION;

		/// <summary>
		/// Gets the raw hub payload data organized by category, useful for debugging or advanced scenarios.
		/// </summary>
		/// <value>
		/// A dictionary containing the latest raw JSON responses from the hub, keyed by category:
		/// "Domain", "Network", "Schedule", "OpenTherm".
		/// </value>
		/// <remarks>
		/// This data reflects the last successful call to <see cref="ReadHubDataAsync"/>.
		/// The returned dictionaries are snapshots of the last refresh; modifying them does not affect hub state.
		/// </remarks>
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
		/// Exports raw hub data for a specific category to a JSON file for debugging or backup purposes.
		/// </summary>
		/// <param name="dataClass">The data category to export. Valid values: "domain", "network", "schedules", "opentherm".</param>
		/// <param name="filename">The output filename (e.g., "data.json").</param>
		/// <param name="filePath">The directory path where the file should be written.</param>
		/// <param name="cancellationToken">Cancellation token to observe.</param>
		/// <returns>
		/// A task that represents the asynchronous operation. The task result is <see langword="true"/> if data was retrieved
		/// and written successfully; otherwise, <see langword="false"/>.
		/// </returns>
		/// <exception cref="WiserHubAuthenticationException">Thrown when authentication with the hub fails.</exception>
		/// <exception cref="WiserHubConnectionException">Thrown when a connection or timeout error occurs.</exception>
		/// <exception cref="WiserHubRESTException">Thrown when the hub returns an error response.</exception>
		/// <exception cref="DirectoryNotFoundException">Thrown when the specified directory path does not exist.</exception>
		/// <exception cref="UnauthorizedAccessException">Thrown when write access to the file path is denied.</exception>
		/// <remarks>
		/// The exported JSON file contains the exact payload structure returned by the hub for the specified data category.
		/// Returns <see langword="false"/> when the <paramref name="dataClass"/> is invalid, the hub returns no data,
		/// or writing the file fails.
		/// </remarks>
		public async Task<bool> OutputRawHubDataAsync (string dataClass, string filename, string filePath, CancellationToken cancellationToken = default)
			{
			// Get correct endpoint
			var endpoint = _dataClassToEndpoint.TryGetValue (dataClass, out var endpointFormat)
		? endpointFormat.FormatInvariant (_wiserApiConnection.Host)
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

		/// <summary>
		/// Releases all resources used by the <see cref="WiserAPI"/> instance, including HTTP connections.
		/// </summary>
		/// <remarks>
		/// Call this method when finished with the API instance to ensure proper cleanup of network resources.
		/// After disposal, the instance should not be used.
		/// </remarks>
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

	/// <summary>
	/// Legacy constants maintained for backward compatibility with earlier API versions.
	/// </summary>
	/// <remarks>
	/// For new code, prefer using the <see cref="Constants"/> class which provides more comprehensive and up-to-date values.
	/// </remarks>
	public static class WiserConstants
		{
		/// <summary>Default away mode temperature in degrees Celsius.</summary>
		public const double DEFAULTAWAYMODETEMP = 16.0;
		/// <summary>Default degraded mode temperature in degrees Celsius.</summary>
		public const double DEFAULTDEGRADEDTEMP = 18.0;
		/// <summary>Maximum allowed Boost delta in degrees Celsius.</summary>
		public const int MAXBOOSTINCREASE = 5;
		/// <summary>Hub temperature error sentinel.</summary>
		public const double TEMPERROR = -1;
		/// <summary>Hot water ON sentinel temperature.</summary>
		public const double TEMPHWON = -20;
		/// <summary>Hot water OFF sentinel temperature.</summary>
		public const double TEMPHWOFF = -20.5;
		/// <summary>Minimum allowed setpoint temperature in degrees Celsius.</summary>
		public const double TEMPMINIMUM = 5;
		/// <summary>Maximum allowed setpoint temperature in degrees Celsius.</summary>
		public const double TEMPMAXIMUM = 30;
		/// <summary>Heating OFF sentinel temperature.</summary>
		public const double TEMPOFF = -20;
		/// <summary>Endpoint key for domain data.</summary>
		public const string WISERHUBDOMAIN = "domain";
		/// <summary>Endpoint key for network data.</summary>
		public const string WISERHUBNETWORK = "network";
		/// <summary>Endpoint key for schedules data.</summary>
		public const string WISERHUBSCHEDULES = "schedules";
		/// <summary>Endpoint key for OpenTherm data.</summary>
		public const string WISERHUBOPENTHERM = "opentherm";
		}
	}
