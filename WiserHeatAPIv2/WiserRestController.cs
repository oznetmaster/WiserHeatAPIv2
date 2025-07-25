// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WiserHeatApiV2
	{
	public static class RestConstants
		{
		public const double RestBackoffFactor = 0.5;
		public const int RestRetries = 3;
		public const int RestTimeout = 10;
		// Wiser Hub Rest Api URL Constants
		public const string WiserHubUrl = "http://{0}/data/v2/";
		public const string WiserHubDomain = WiserHubUrl + "domain/";
		public const string WiserHubNetwork = WiserHubUrl + "network/";
		public const string WiserHubSchedules = WiserHubUrl + "schedules/";
		public const string WiserHubOpentherm = WiserHubUrl + "opentherm/";
		public const string WiserSystem = "System";
		public const string WiserDevice = "Device/{0}";
		public const string WiserHotWater = "HotWater/{0}";
		public const string WiserRoom = "Room/{0}";
		public const string WiserSmartValve = "SmartValve/{0}";
		public const string WiserRoomStat = "RoomStat/{0}";
		public const string WiserSmartPlug = "SmartPlug/{0}";
#if HEATACTUATOR
		public const string WiserHeatingActuator = "HeatingActuator/{0}";
#endif
		public const string WiserUfhController = "UnderFloorHeating/{0}";
#if SHUTTER
		public const string WiserShutter = "Shutter/{0}";
#endif
#if LIGHT
		public const string WiserLight = "Light/{0}";
#endif
		}

	// Custom Exceptions
	public class WiserHubAuthenticationException : Exception
		{
		public WiserHubAuthenticationException (string message) : base (message) { }
		}

	public class WiserHubConnectionException : Exception
		{
		public WiserHubConnectionException (string message) : base (message) { }
		}

	public class WiserHubRESTException : Exception
		{
		public WiserHubRESTException (string message) : base (message) { }
		}

	// Connection Info Class
	public class WiserConnection
		{
		public string Host
			{
			get; set;
			}
		public string Secret
			{
			get; set;
			}
		public WiserUnits Units { get; set; } = WiserUnits.Metric; // Default to Metric

		public WiserConnection (string? host, string? secret)
			{
			Host = host ?? throw new ArgumentNullException (nameof (host));
			Secret = secret ?? throw new ArgumentNullException (nameof (secret));
			}
		}

	// Enums
	public enum WiserRestAction
		{
		GET,
		POST,
		PATCH,
		DELETE
		}

	public class WiserRestController : IDisposable
		{
		private readonly WiserConnection _wiserConnection;
		private HttpClient? _httpClient;
		private static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserRestController));

		public WiserRestController (WiserConnection wiserConnection)
			{
			var logger = ((log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_LOGGER).Logger);
#if DEBUG
			logger.Level = log4net.Core.Level.Debug;
#else
			logger.Level = log4net.Core.Level.Error;
#endif
			_wiserConnection = wiserConnection ?? throw new ArgumentNullException (nameof (wiserConnection));

			// Configure HttpClient with retry logic (simplified for example)
			_httpClient = new HttpClient
				{
				Timeout = TimeSpan.FromSeconds (RestConstants.RestTimeout)
				};
			_httpClient.DefaultRequestHeaders.Add ("SECRET", _wiserConnection.Secret);
			_httpClient.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
			_httpClient.DefaultRequestHeaders.ConnectionClose = true; // Equivalent to "Connection": "close"
			}

		// Fix for CRR0029: Explicitly call ConfigureAwait(false) to avoid implicit ConfigureAwait(true)
		public async Task<HttpResponseMessage?> ExecuteHttpRequestAsync (WiserRestAction action, string url, StringContent? data = null, CancellationToken cancellationToken = default)
			{
			HttpResponseMessage? response = null;
			int retryCount = RestConstants.RestRetries;
			TimeSpan delay = TimeSpan.FromSeconds (1); // Initial delay

			while (retryCount >= 0)
				{
				try
					{
					switch (action)
						{
						case WiserRestAction.GET:
							response = await _httpClient!.GetAsync (url, cancellationToken).ConfigureAwait (false);
							break;
						case WiserRestAction.POST:
							response = await _httpClient!.PostAsync (url, data, cancellationToken).ConfigureAwait (false);
							break;
						case WiserRestAction.PATCH:
							response = await _httpClient!.PatchAsync (url, data, cancellationToken).ConfigureAwait (false);
							break;
						case WiserRestAction.DELETE:
							response = await _httpClient!.DeleteAsync (url, cancellationToken).ConfigureAwait (false);
							break;
						default:
							throw new ArgumentOutOfRangeException (nameof (action), action, "Invalid WiserRestAction");
						}

					if (response.IsSuccessStatusCode)
						{
						return response;
						}
					else if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge ||
								response.StatusCode == HttpStatusCode.InternalServerError ||
								response.StatusCode == HttpStatusCode.BadGateway ||
								response.StatusCode == HttpStatusCode.ServiceUnavailable ||
								response.StatusCode == HttpStatusCode.GatewayTimeout)
						{
						// Wait and retry
						retryCount--;
						if (retryCount >= 0)
							{
							await Task.Delay (delay, cancellationToken).ConfigureAwait (false);

							delay = TimeSpan.FromSeconds (delay.TotalSeconds * RestConstants.RestBackoffFactor); // Exponential backoff
							}
						}
					else
						{
						// Non-retryable error
						return response;
						}
					}
				catch (HttpRequestException ex)
					{
					_LOGGER.Error ("HTTP Request Exception", ex);
					throw new WiserHubConnectionException ($"Connection error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				catch (TaskCanceledException ex)
					{
					_LOGGER.Error ("Task Canceled Exception", ex);
					throw new WiserHubConnectionException ($"Timeout error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				}

			// If we get here, we've exhausted our retries
			return response;
			}

		private async Task<bool> _DoHubActionAsync (WiserRestAction action, string url, object? data = null, bool raiseForEndpointError = true, CancellationToken cancellationToken = default)
			{
			StringContent? jsonContent = null;
			if (data != null)
				{
				var jsonData = JsonConvert.SerializeObject (data);
				jsonContent = new StringContent (jsonData, Encoding.UTF8, "application/json");
				}

			try
				{
				HttpResponseMessage? response = await ExecuteHttpRequestAsync (action, url, jsonContent, cancellationToken).ConfigureAwait (false);

				if (response == null)
					{
					_LOGGER.Error ("Response from Wiser Hub is null.");
					throw new WiserHubConnectionException ("Response from Wiser Hub is null.");
					}

				if (!response.IsSuccessStatusCode)
					{
					await _ProcessNokResponseAsync (response, raiseForEndpointError).ConfigureAwait (false);
					return false; // Return empty object on failure
					}
				else
					{
					return true;
					}
				}
			catch (WiserHubConnectionException)
				{
				throw; // Re-throw custom exception
				}
			catch (Exception ex)
				{
				// Catch any other unexpected exceptions
				_LOGGER.Error ("An unexpected error occurred in _DoHubActionAsync.", ex);
				throw new WiserHubConnectionException ($"An unexpected error occurred: {ex.Message}");
				}
			}

		public async Task<Dictionary<string, object>> GetHubDataAsync (string url, object? data = null, bool raiseForEndpointError = true, CancellationToken cancellationToken = default)
			{
			StringContent? jsonContent = null;
			if (data != null)
				{
				var jsonData = JsonConvert.SerializeObject (data);
				jsonContent = new StringContent (jsonData, Encoding.UTF8, "application/json");
				}

			try
				{
				HttpResponseMessage? response = await ExecuteHttpRequestAsync (WiserRestAction.GET, url, jsonContent, cancellationToken).ConfigureAwait (false);

				if (response == null)
					{
					_LOGGER.Error ("Response from Wiser Hub is null.");
					throw new WiserHubConnectionException ("Response from Wiser Hub is null.");
					}

				if (!response.IsSuccessStatusCode)
					{
					await _ProcessNokResponseAsync (response, raiseForEndpointError).ConfigureAwait (false);
					return new Dictionary<string, object> (); // Return empty object on failure
					}
				else
					{
					var content = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
					if (content.Length > 0)
						{
						// Remove non-ASCII characters (equivalent to the Python regex)
						string cleanedContent = Regex.Replace (Encoding.UTF8.GetString (content), @"[^\u0020-\u007F]+", string.Empty);
						var cleaned = JsonConvert.DeserializeObject<JToken> (cleanedContent);
						if (cleaned != null)
							return (Dictionary<string, object>?)ConvertJTokenToObject (cleaned) ?? new Dictionary<string, object> ();
						}
					return new Dictionary<string, object> ();
					}
				}
			catch (WiserHubConnectionException)
				{
				throw; // Re-throw custom exception
				}
			catch (Exception ex)
				{
				// Catch any other unexpected exceptions
				_LOGGER.Error ("An unexpected error occurred in _DoHubActionAsync.", ex);
				throw new WiserHubConnectionException ($"An unexpected error occurred: {ex.Message}");
				}
			}


		//public Dictionary<string, object> GetConnectionPools()
		//{
		//  // This is a placeholder as replicating connection pooling exactly might be complex.
		//  // Depending on your needs, you might use HttpClientFactory for connection pooling.
		//  // This example returns an empty dictionary.
		//  return new Dictionary<string, object>();
		//}

		private async Task _ProcessNokResponseAsync (HttpResponseMessage response, bool raiseForEndpointError = true)
			{
			string errorMessage = await response.Content.ReadAsStringAsync ().ConfigureAwait (false);
			if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
				throw new WiserHubAuthenticationException ($"Error authenticating to Wiser Hub {_wiserConnection.Host}. Check your secret key.  Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.NotFound && raiseForEndpointError)
				{
				throw new WiserHubRESTException ($"Rest endpoint not found on Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.RequestTimeout)
				{
				throw new WiserHubConnectionException ($"Connection timed out trying to communicate with Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (raiseForEndpointError)
				{
				throw new WiserHubRESTException ($"Unknown error communicating with Wiser Hub {_wiserConnection.Host}. Error code is: {response.StatusCode}. Message: {errorMessage}");
				}
			}

		public Task<bool> SendCommandAsync (string url, object? commandData, WiserRestAction method = WiserRestAction.PATCH, CancellationToken cancellationToken = default)
			{
			string fullUrl = $"{string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubDomain, _wiserConnection.Host)}{url}";
			_LOGGER.DebugFormat ("Sending command to url: {0} with parameters {1}", fullUrl, JsonConvert.SerializeObject (commandData));

			return _DoHubActionAsync (method, fullUrl, commandData, cancellationToken: cancellationToken);
			}

		private Task<bool> _DoScheduleActionAsync (WiserRestAction action, string url, object? scheduleData = null, CancellationToken cancellationToken = default)
			{
			string fullUrl = $"{string.Format (CultureInfo.InvariantCulture, RestConstants.WiserHubSchedules, _wiserConnection.Host)}{url}";
			_LOGGER.DebugFormat ("Actioning schedule to url: {0} with action {1} and _data {2}", fullUrl, action.ToString (), JsonConvert.SerializeObject (scheduleData));
			
			return _DoHubActionAsync (action, fullUrl, scheduleData, cancellationToken: cancellationToken);
			}

		public Task<bool> SendScheduleCommandAsync (string action, object? scheduleData, int id = 0, string? scheduleType = null, CancellationToken cancellationToken = default)
			{
			switch (action.ToUpper (CultureInfo.InvariantCulture))
				{
				case "UPDATE":
					return _DoScheduleActionAsync (
						 WiserRestAction.PATCH,
						 $"{scheduleType}/{id}",
						 scheduleData, cancellationToken);

				case "CREATE":
					return _DoScheduleActionAsync (
						 WiserRestAction.POST,
						 "Assign",
						 scheduleData, cancellationToken);

				case "ASSIGN":
					return _DoScheduleActionAsync (
						 WiserRestAction.PATCH,
						 "Assign",
						 scheduleData, cancellationToken);

				case "DELETE":
					return _DoScheduleActionAsync (
						 WiserRestAction.DELETE,
						 $"{scheduleType}/{id}",
						 scheduleData, cancellationToken);

				default:
					_LOGGER.ErrorFormat ("Invalid schedule action: {}", action);
					return Task.FromResult(false);
				}
			}

		public static object? ConvertJTokenToObject (JToken token)
			{
			switch (token.Type)
				{
				case JTokenType.Object:
					return token.Children<JProperty> ()
									.ToDictionary (prop => prop.Name, prop => ConvertJTokenToObject (prop.Value));
				case JTokenType.Array:
					if (token.First is JValue)
						{
						return token.Children<JValue> ().Select (c => c.Value).ToList ();
						}
					else
						{
						return token.Select (ConvertJTokenToObject).Cast<Dictionary<string, object>> ().ToList ();
						}
				default:
					return ((JValue)token).Value;
				}
			}

		public void Dispose ()
			{
			if (_httpClient != null)
				{
				_httpClient.Dispose();
				_httpClient = null;
				}
			GC.SuppressFinalize(this);
			}

		public string GetHost()
		{
			return _wiserConnection.Host;
		}
		}
	}
