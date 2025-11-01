// Copyright ©2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2;

/// <summary>
/// Polyfill to get strongly-typed enum helpers across TFMs.
/// </summary>
internal static class EnumValues
{
#if NETFRAMEWORK
	public static IEnumerable<TEnum> GetValues<TEnum>() where TEnum : struct, Enum =>
		Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
#else
	public static IEnumerable<TEnum> GetValues<TEnum>() where TEnum : struct, Enum =>
		Enum.GetValues<TEnum>();
#endif

	public static bool TryParse<TEnum>(string value, bool ignoreCase, out TEnum result) where TEnum : struct, Enum =>
		Enum.TryParse(value, ignoreCase, out result);

	public static TEnum ParseOrThrow<TEnum>(string value, bool ignoreCase = true) where TEnum : struct, Enum
		=> Enum.TryParse(value, ignoreCase, out TEnum parsed)
			? parsed
			: throw new ArgumentException($"'{value}' is not a valid {typeof(TEnum).Name}");
}
