using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WiserHeatApiV2
	{
	public static class RestConstants
		{
		public const double REST_BACKOFF_FACTOR = 0.5;
		public const int REST_RETRIES = 3;
		public const int REST_TIMEOUT = 10;
		// Wiser Hub Rest Api URL Constants
		public const string WISERHUBURL = "http://{0}/data/v2/";
		public const string WISERHUBDOMAIN = WISERHUBURL + "domain/";
		public const string WISERHUBNETWORK = WISERHUBURL + "network/";
		public const string WISERHUBSCHEDULES = WISERHUBURL + "schedules/";
		public const string WISERHUBOPENTHERM = WISERHUBURL + "opentherm/";
		public const string WISERSYSTEM = "System";
		public const string WISERDEVICE = "Device/{0}";
		public const string WISERHOTWATER = "HotWater/{0}";
		public const string WISERROOM = "Room/{0}";
		public const string WISERSMARTVALVE = "SmartValve/{0}";
		public const string WISERROOMSTAT = "RoomStat/{0}";
		public const string WISERSMARTPLUG = "SmartPlug/{0}";
		public const string WISERHEATINGACTUATOR = "HeatingActuator/{0}";
		public const string WISERUFHCONTROLLER = "UnderFloorHeating/{0}";
#if SHUTTER
		public const string WISERSHUTTER = "Shutter/{0}";
#endif
#if LIGHT
		public const string WISERLIGHT = "Light/{0}";
#endif
		}

	// Custom Exceptions
	public class WiserHubAuthenticationError : Exception
		{
		public WiserHubAuthenticationError (string message) : base (message) { }
		}

	public class WiserHubConnectionError : Exception
		{
		public WiserHubConnectionError (string message) : base (message) { }
		}

	public class WiserHubRESTError : Exception
		{
		public WiserHubRESTError (string message) : base (message) { }
		}

	// Connection Info Class
	public class WiserConnection
		{
		public string Host { get; set; }
		public string Secret { get; set; }
		public WiserUnitsEnum Units { get; set; } = WiserUnitsEnum.Metric; // Default to Metric
		}

	// Enums
	public enum WiserRestActionEnum
		{
		GET,
		POST,
		PATCH,
		DELETE
		}

	public interface IWiserRestController
		{
		Task<Dictionary<string, object>> GetHubDataAsync (string url, bool raiseForEndpointError = true);
		Task<bool> SendCommandAsync (string url, object commandData, WiserRestActionEnum method = WiserRestActionEnum.PATCH);
		}

	public class WiserRestController : IWiserRestController
		{
		private readonly WiserConnection _wiserConnection;
		private readonly HttpClient _httpClient;
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserRestController));

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
				Timeout = TimeSpan.FromSeconds (RestConstants.REST_TIMEOUT)
				};
			_httpClient.DefaultRequestHeaders.Add ("SECRET", _wiserConnection.Secret);
			_httpClient.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
			_httpClient.DefaultRequestHeaders.ConnectionClose = true; // Equivalent to "Connection": "close"
			}

		// Fix for CRR0029: Explicitly call ConfigureAwait(false) to avoid implicit ConfigureAwait(true)
		public async Task<HttpResponseMessage> ExecuteHttpRequestAsync (WiserRestActionEnum action, string url, StringContent data = null)
			{
			HttpResponseMessage response = null;
			int retryCount = RestConstants.REST_RETRIES;
			TimeSpan delay = TimeSpan.FromSeconds (1); // Initial delay

			while (retryCount >= 0)
				{
				try
					{
					switch (action)
						{
						case WiserRestActionEnum.GET:
							response = await _httpClient.GetAsync (url).ConfigureAwait (false);
							break;
						case WiserRestActionEnum.POST:
							response = await _httpClient.PostAsync (url, data).ConfigureAwait (false);
							break;
						case WiserRestActionEnum.PATCH:
							response = await _httpClient.PatchAsync (url, data).ConfigureAwait (false);
							break;
						case WiserRestActionEnum.DELETE:
							response = await _httpClient.DeleteAsync (url).ConfigureAwait (false);
							break;
						default:
							throw new ArgumentOutOfRangeException (nameof (action), action, "Invalid WiserRestActionEnum");
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
							await Task.Delay (delay).ConfigureAwait (false);

							delay = TimeSpan.FromSeconds (delay.TotalSeconds * RestConstants.REST_BACKOFF_FACTOR); // Exponential backoff
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
					throw new WiserHubConnectionError ($"Connection error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				catch (TaskCanceledException ex)
					{
					_LOGGER.Error ("Task Canceled Exception", ex);
					throw new WiserHubConnectionError ($"Timeout error trying to communicate with Wiser Hub {_wiserConnection.Host}. Error is {ex.Message}");
					}
				}

			// If we get here, we've exhausted our retries
			return response;
			}

		private async Task<Dictionary<string, object>> _DoHubActionAsync (WiserRestActionEnum action, string url, object data = null, bool raiseForEndpointError = true)
			{
			StringContent jsonContent = null;
			if (data != null)
				{
				var jsonData = JsonConvert.SerializeObject (data);
				jsonContent = new StringContent (jsonData, Encoding.UTF8, "application/json");
				}

			try
				{
				HttpResponseMessage response = await ExecuteHttpRequestAsync (action, url, jsonContent).ConfigureAwait (false);

				if (!response.IsSuccessStatusCode)
					{
					await _ProcessNokResponseAsync (response, raiseForEndpointError).ConfigureAwait (false);
					return new Dictionary<string, object> (); // Return empty object on failure
					}
				else
					{
					if (action == WiserRestActionEnum.GET)
						{
						var content = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
						if (content.Length > 0)
							{
							// Remove non-ASCII characters (equivalent to the Python regex)
							string cleanedContent = Regex.Replace (Encoding.UTF8.GetString (content), @"[^\u0020-\u007F]+", string.Empty);
							var cleaned = JsonConvert.DeserializeObject<JToken> (cleanedContent);
							return (Dictionary<string, object>)ConvertJTokenToObject (cleaned);
							}
						}
					else
						{
						return null;
						}
					return new Dictionary<string, object> ();
					}
				}
			catch (WiserHubConnectionError)
				{
				throw; // Re-throw custom exception
				}
			catch (Exception ex)
				{
				// Catch any other unexpected exceptions
				_LOGGER.Error ("An unexpected error occurred in _DoHubActionAsync.", ex);
				throw new WiserHubConnectionError ($"An unexpected error occurred: {ex.Message}");
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
				throw new WiserHubAuthenticationError ($"Error authenticating to Wiser Hub {_wiserConnection.Host}. Check your secret key.  Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.NotFound && raiseForEndpointError)
				{
				throw new WiserHubRESTError ($"Rest endpoint not found on Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (response.StatusCode == HttpStatusCode.RequestTimeout)
				{
				throw new WiserHubConnectionError ($"Connection timed out trying to communicate with Wiser Hub {_wiserConnection.Host}. Message: {errorMessage}");
				}
			else if (raiseForEndpointError)
				{
				throw new WiserHubRESTError ($"Unknown error communicating with Wiser Hub {_wiserConnection.Host}. Error code is: {response.StatusCode}. Message: {errorMessage}");
				}
			}

		public async Task<Dictionary<string, object>> GetHubDataAsync (string url, bool raiseForEndpointError = true)
			{
			return await _DoHubActionAsync (WiserRestActionEnum.GET, url, raiseForEndpointError: raiseForEndpointError).ConfigureAwait (false);
			}

		public async Task<bool> SendCommandAsync (string url, object commandData, WiserRestActionEnum method = WiserRestActionEnum.PATCH)
			{
			string fullUrl = string.Format (RestConstants.WISERHUBDOMAIN, _wiserConnection.Host) + url;
			_LOGGER.DebugFormat ("Sending command to url: {0} with parameters {1}", fullUrl, JsonConvert.SerializeObject (commandData));

			return await _DoHubActionAsync (method, fullUrl, commandData).ConfigureAwait (false) == null;
			}

		private async Task<bool> _DoScheduleActionAsync (WiserRestActionEnum action, string url, object scheduleData = null)
			{
			string fullUrl = string.Format (RestConstants.WISERHUBSCHEDULES, _wiserConnection.Host) + url;
			_LOGGER.DebugFormat ("Actioning schedule to url: {0} with action {1} and data {2}", fullUrl, action.ToString (), JsonConvert.SerializeObject (scheduleData));
			return await _DoHubActionAsync (action, fullUrl, scheduleData).ConfigureAwait (false) == null;
			}

		public async Task<bool> SendScheduleCommandAsync (string action, object scheduleData, int id = 0, string scheduleType = null)
			{
			bool result;
			switch (action.ToUpper ())
				{
				case "UPDATE":
					result = await _DoScheduleActionAsync (
						 WiserRestActionEnum.PATCH,
						 $"{scheduleType}/{id}",
						 scheduleData).ConfigureAwait (false);
					break;

				case "CREATE":
					result = await _DoScheduleActionAsync (
						 WiserRestActionEnum.POST,
						 "Assign",
						 scheduleData).ConfigureAwait (false);
					break;

				case "ASSIGN":
					result = await _DoScheduleActionAsync (
						 WiserRestActionEnum.PATCH,
						 "Assign",
						 scheduleData).ConfigureAwait (false);
					break;

				case "DELETE":
					result = await _DoScheduleActionAsync (
						 WiserRestActionEnum.DELETE,
						 $"{scheduleType}/{id}",
						 scheduleData).ConfigureAwait (false);
					break;

				default:
					_LOGGER.ErrorFormat ("Invalid schedule action: {}", action);
					return false;
				}
			return result;
			}

		public static object ConvertJTokenToObject (JToken token)
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

		}
	}
