// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2;

/// <summary>
/// HTTP client extensions used by the Wiser API.
/// </summary>
public static class HttpClientExtensions
	{
	/// <summary>
	/// Sends an HTTP PATCH request to the specified URI.
	/// </summary>
	/// <param name="client">The HTTP client instance.</param>
	/// <param name="requestUri">The request URI.</param>
	/// <param name="content">Optional request content.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The HTTP response message.</returns>
	public static Task<HttpResponseMessage> PatchAsync (this HttpClient client, string requestUri, HttpContent? content, CancellationToken cancellationToken = default)
		{
		var request = new HttpRequestMessage (new HttpMethod ("PATCH"), requestUri)
			{
			Content = content
			};
		return client.SendAsync (request, cancellationToken);
		}
	}

