namespace Plugin.Maui.BluetoothManager;

static class DeviceFilter
{
	public static bool Matches(BleDevice device, ScanOptions options, int? globalMinimumRssi)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(options);

		var minimumRssi = options.MinimumRssi ?? globalMinimumRssi;
		if (minimumRssi is int floor && device.Rssi < floor)
			return false;

		if (!string.IsNullOrWhiteSpace(options.NameContains))
		{
			if (string.IsNullOrWhiteSpace(device.Name) ||
				device.Name.IndexOf(options.NameContains, StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
		}

		if (options.ServiceUuids is { Count: > 0 } required)
		{
			var advertised = device.AdvertisedServiceIds;
			if (!required.Any(uuid => advertised.Contains(uuid)))
				return false;
		}

		return options.Filter?.Invoke(device) ?? true;
	}
}
