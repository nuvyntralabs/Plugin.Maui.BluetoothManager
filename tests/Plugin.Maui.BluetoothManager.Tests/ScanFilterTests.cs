namespace Plugin.Maui.BluetoothManager.Tests;

public sealed class ScanFilterTests
{
	static readonly Guid PrinterService = BleUuid.FromShort(0x18F0);

	[Fact]
	public void Matches_DropsWeakRssi()
	{
		var device = ManagerHarness.Device(rssi: -90);
		var options = new ScanOptions { MinimumRssi = -70 };

		Assert.False(DeviceFilter.Matches(device, options, null));
	}

	[Fact]
	public void Matches_UsesGlobalMinimumRssi()
	{
		var device = ManagerHarness.Device(rssi: -80);
		Assert.False(DeviceFilter.Matches(device, new ScanOptions(), -70));
		Assert.True(DeviceFilter.Matches(device, new ScanOptions(), -90));
	}

	[Fact]
	public void Matches_RequiresNameSubstring()
	{
		var named = ManagerHarness.Device(name: "StarPrinter");
		var unnamed = ManagerHarness.Device(name: null);
		var options = new ScanOptions { NameContains = "printer" };

		Assert.True(DeviceFilter.Matches(named, options, null));
		Assert.False(DeviceFilter.Matches(unnamed, options, null));
	}

	[Fact]
	public void Matches_RequiresAdvertisedService()
	{
		var device = ManagerHarness.Device(services: PrinterService);
		var options = new ScanOptions { ServiceUuids = [PrinterService] };
		var other = new ScanOptions { ServiceUuids = [BleUuid.FromShort(0x180A)] };

		Assert.True(DeviceFilter.Matches(device, options, null));
		Assert.False(DeviceFilter.Matches(device, other, null));
	}

	[Fact]
	public void Matches_AppliesCustomPredicate()
	{
		var device = ManagerHarness.Device(name: "IoT-22");
		var options = new ScanOptions { Filter = item => item.Name?.StartsWith("IoT", StringComparison.Ordinal) == true };

		Assert.True(DeviceFilter.Matches(device, options, null));
	}

	[Fact]
	public async Task ScanAsync_ReturnsFilteredDevicesSortedByRssi()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		transport.PendingDiscoveries.Add(ManagerHarness.Device("11:11", "Sensor", -80));
		transport.PendingDiscoveries.Add(ManagerHarness.Device("22:22", "Printer", -30));

		var devices = await manager.ScanAsync(new ScanOptions { Duration = TimeSpan.FromMilliseconds(5) });

		Assert.Equal(["22:22", "11:11"], devices.Select(device => device.Id).ToArray());
	}

	[Fact]
	public async Task ScanAsync_RaisesDeviceDiscoveredForMatchesOnly()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		var seen = new List<string>();
		manager.DeviceDiscovered += (_, e) => seen.Add(e.Device.Id);
		transport.PendingDiscoveries.Add(ManagerHarness.Device("aa", "POS-1"));
		transport.PendingDiscoveries.Add(ManagerHarness.Device("bb", "Headset"));

		await manager.ScanAsync(new ScanOptions
		{
			Duration = TimeSpan.FromMilliseconds(5),
			NameContains = "POS"
		});

		Assert.Equal(["aa"], seen);
	}
}
