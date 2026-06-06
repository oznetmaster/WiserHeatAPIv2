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

---

## Quick Start

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

## Acknowledgements

Adapted from the Python project [wiserHeatAPIv2](https://pypi.org/project/wiserHeatAPIv2/) by Mark Parker.

---

## License

MIT © 2026 Neil Colvin — see [LICENSE](WiserHeatAPIv2/LICENSE).
