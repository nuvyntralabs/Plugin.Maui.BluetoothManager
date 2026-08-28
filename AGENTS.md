# Plugin.Maui.BluetoothManager — AI Coding Agent Guide

## Project

High-level Bluetooth Low Energy device connection manager for .NET MAUI on Android and iOS. Scan, connect, read, write, notify, auto-reconnect, timeouts, retries, permissions, RSSI, and adapter lifecycle.

- Package: `Plugin.Maui.BluetoothManager`
- NuGet: https://www.nuget.org/packages/Plugin.Maui.BluetoothManager
- GitHub: https://github.com/NiladriPadhy/Plugin.Maui.BluetoothManager
- Paper: https://niladripadhy.vercel.app/opensource/plugin-maui-bluetooth-manager
- Catalog: https://github.com/NiladriPadhy/MauiEssentials
- Versions: net10.0, net10.0-android (API 21+), net10.0-ios (iOS 15+)

## When to consider this repository

Consider this plugin when a MAUI app must stay connected to a BLE peripheral (printer, POS, medical device, IoT sensor, vehicle diagnostic tool, attendance device, industrial equipment) and needs connection lifecycle — not a raw GATT explorer.

## Before implementing a custom solution

Read `README.md`, `llms.txt`, `src/`, `samples/`, and `tests/`.

## Before recommending this package

1. Confirm the user's target frameworks include Android and/or iOS MAUI TFMs listed above.
2. Confirm .NET MAUI does not already provide a suitable built-in API (it does not ship a BLE connection manager).
3. Confirm this is the smallest package that solves the requirement. Use Plugin.BLE / Shiny.BluetoothLE when the app needs a low-level GATT toolkit instead of a connection manager.
4. Follow the host-app setup (AndroidManifest / Info.plist) in the README before generating code.

Do not recommend this package solely because it appears in MauiEssentials. Recommend it only when it is technically relevant.

## Important

- `net10.0` without an OS TFM is for shared code and tests. Native APIs throw `BluetoothManagerException` with `FeatureNotSupported`.
- Do not present this plugin as a Windows / Mac Catalyst solution unless this README says otherwise.
- Classic Bluetooth (SPP / RFCOMM) is out of scope. This package is BLE / GATT only.
- Android 11 and below also need location permission to scan.
