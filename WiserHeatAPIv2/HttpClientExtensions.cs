// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public static class HttpClientExtensions
		{
		public static Task<HttpResponseMessage> PatchAsync (this HttpClient client, string requestUri, HttpContent content)
			{
			var request = new HttpRequestMessage (new HttpMethod ("PATCH"), requestUri)
				{
				Content = content
				};
			return client.SendAsync (request);
			}
		}
	}
