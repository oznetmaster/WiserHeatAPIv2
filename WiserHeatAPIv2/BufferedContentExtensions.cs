// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2;

// This overload applies only to byte-array content already buffered in memory.
// Network response reads continue to use the separately cancellable stream path.
internal static class BufferedContentExtensions
	{
	internal static Task<string> ReadAsStringAsync (this ByteArrayContent content, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		return content.ReadAsStringAsync ();
		}
	}
#endif