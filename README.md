# WiserHeatAPIv2

A .NET client library for the **Drayton Wiser Heating** local REST API, enabling discovery, monitoring, scheduling, and control of Wiser hubs, rooms, devices, hot water, smart plugs, lights, shutters, and related entities.

[![NuGet](https://img.shields.io/nuget/v/WiserHeatAPIv2.svg)](https://www.nuget.org/packages/WiserHeatAPIv2)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](WiserHeatAPIv2/LICENSE)

---

## Supported Platforms

| Target Framework | Supported |
|---|---|
| .NET 10 | ✅ |
| .NET Framework 4.7.2 | ✅ |

---

## Overview

`WiserHeatAPIv2` provides a strongly typed .NET wrapper around the local Wiser hub API. It supports:

- Direct connection to a Wiser hub by IP address and secret
- Local network discovery of Wiser hubs
- Reading and refreshing hub state
- Access to rooms, devices, schedules, hot water, system information, and capabilities
- Control operations for supported device types
- YAML and structured schedule import helpers

This .NET library is an adaptation of the original Python project **[wiserHeatAPIv2](https://pypi.org/project/wiserHeatAPIv2/)** by Mark Parker.

---

## Installation

```
dotnet add package WiserHeatAPIv2
```

The official NuGet package is built with all optional feature areas enabled.

---

## Conditional Features

The source project contains several feature areas controlled by compilation symbols:

- `SHUTTER` — shutter-related types and functionality
- `LIGHT` — lighting-related types and functionality
- `HEATACTUATOR` — heating actuator support
- `OPENTHERM` — OpenTherm-related support

The published NuGet package includes all of these optional capabilities.

Some direct dependencies currently resolve to prerelease package versions. These were introduced through vulnerability remediation recommended by Visual Studio and have been retained to avoid reintroducing the original security or compatibility issues without further validation.

---

## Quick Start

### Obtaining the Wiser secret

The Wiser hub secret is required to authenticate with the local API.

Reference: https://it.knightnet.org.uk/kb/nr-qa/drayton-wiser-heating-control/#controlling-the-system

To obtain it:

1. Press the setup button on the HeatHub so that the light starts flashing.
2. Look for the Wi-Fi network (SSID) called `WiserHeatXXXXXX`, where `XXXXXX` is the last 6 digits of the MAC address.
3. Connect to that network from a Windows, Linux, macOS, Android, or iPhone device.
4. Open a browser and navigate to `http://192.168.8.1/secret`.
5. The hub will return a string containing your system secret. Store this somewhere safe. If you are using the console test utility, place this value together with the hub IP address in `wiserkeys.params`.
6. Press the setup button on the HeatHub again to return it to normal operation.
7. Copy the secret and save it somewhere safe.

If you already know the hub IP address, you can connect directly. Otherwise, use the discovery helper shown below to find the hub first.

### Connect to a hub

```csharp
using WiserHeatApiV2;

var api = new WiserAPI("192.168.1.50", "your-wiser-secret");
await api.InitializeAsync();

Console.WriteLine($"System version: {api.System?.ActiveSystemVersion}");
Console.WriteLine($"Room count: {api.Rooms.All.Count}");
```

### Discover hubs on the local network

```csharp
using WiserHeatApiV2;

List<WiserDiscoveredHub> hubs = await WiserDiscovery.DiscoverHubAsync(60, 2);
foreach (var hub in hubs)
{
    Console.WriteLine(hub.Url);
}
```

### Inspect rooms and devices

```csharp
using WiserHeatApiV2;

var api = new WiserAPI("192.168.1.50", "your-wiser-secret");
await api.InitializeAsync();

foreach (var room in api.Rooms.All)
{
    Console.WriteLine($"{room.Name}: {room.CurrentTemperature}°C -> {room.CurrentTargetTemperature}°C");
}

foreach (var device in api.Devices.All)
{
    Console.WriteLine($"{device.ProductType} #{device.Id}");
}
```

### Refresh hub data

```csharp
await api.ReadHubDataAsync();
```

---

## Test Console

The solution includes `WiserHeatAPIv2Test`, a console application that exercises hub discovery, initialization, device listing, room inspection, and general API validation against a real Wiser installation.

---

## Documentation

Full API documentation is published at **[oznetmaster.github.io/WiserHeatAPIv2](https://oznetmaster.github.io/WiserHeatAPIv2/)**.

---

## Repository Contents

- `WiserHeatAPIv2` — the main library project published to NuGet
- `WiserHeatApp.Wpf` — a WPF desktop application for interacting with and monitoring a Wiser system
- `WiserHeatAPIv2Test` — a console-based test utility for exercising the API against a real hub

---

## Acknowledgements

Adapted from the Python project [wiserHeatAPIv2](https://pypi.org/project/wiserHeatAPIv2/) by Mark Parker.

---

## License

MIT © 2026 Neil Colvin — see [LICENSE](WiserHeatAPIv2/LICENSE).
