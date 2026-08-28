namespace Plugin.Maui.BluetoothManager.Tests;

public sealed class ConnectionLifecycleTests
{
	[Fact]
	public async Task ConnectAsync_MovesThroughConnectingToConnected()
	{
		var (manager, _, _, _) = ManagerHarness.Create();
		var states = new List<BluetoothConnectionState>();
		manager.ConnectionStateChanged += (_, e) => states.Add(e.Current);

		var device = await manager.ConnectAsync(ManagerHarness.Device());

		Assert.Equal(BluetoothConnectionState.Connected, manager.ConnectionState);
		Assert.Equal(device.Id, manager.ConnectedDevice?.Id);
		Assert.Contains(BluetoothConnectionState.Connecting, states);
		Assert.Contains(BluetoothConnectionState.Connected, states);
	}

	[Fact]
	public async Task ConnectAsync_TimesOut()
	{
		var (manager, transport, _, _) = ManagerHarness.Create(options =>
			options.ConnectionTimeout = TimeSpan.FromMilliseconds(5));
		transport.ConnectShouldTimeout = true;

		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() =>
			manager.ConnectAsync(ManagerHarness.Device()));

		Assert.Equal(BluetoothManagerError.ConnectionTimeout, error.Error);
		Assert.Equal(BluetoothConnectionState.Failed, manager.ConnectionState);
	}

	[Fact]
	public async Task DisconnectAsync_RaisesUserRequested()
	{
		var (manager, _, _, _) = ManagerHarness.Create();
		var reasons = new List<DisconnectReason>();
		manager.DeviceDisconnected += (_, e) => reasons.Add(e.Reason);

		var device = await manager.ConnectAsync(ManagerHarness.Device());
		await manager.DisconnectAsync(device);

		Assert.Contains(DisconnectReason.UserRequested, reasons);
		Assert.Null(manager.ConnectedDevice);
	}

	[Fact]
	public async Task UnexpectedDrop_RaisesRemoteDisconnected()
	{
		var (manager, transport, _, _) = ManagerHarness.Create(options => options.AutoReconnect = false);
		DisconnectReason? reason = null;
		manager.DeviceDisconnected += (_, e) => reason = e.Reason;

		var device = await manager.ConnectAsync(ManagerHarness.Device());
		transport.Drop(device.Id);

		Assert.Equal(DisconnectReason.RemoteDisconnected, reason);
	}

	[Fact]
	public async Task AdapterOff_DisconnectsActiveSessions()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		DisconnectReason? reason = null;
		manager.DeviceDisconnected += (_, e) => reason = e.Reason;

		await manager.ConnectAsync(ManagerHarness.Device());
		transport.SetAdapterState(BluetoothAdapterState.Off);

		Assert.Equal(DisconnectReason.AdapterUnavailable, reason);
		Assert.Equal(BluetoothAdapterState.Off, manager.AdapterState);
	}

	[Fact]
	public async Task WriteAndRead_RoundTrip()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		var device = ManagerHarness.Device();
		var characteristic = new BleCharacteristic(device.Id, BleUuid.FromShort(0x18F0), BleUuid.FromShort(0x2AF1), BleCharacteristicProperties.Read | BleCharacteristicProperties.Write);
		transport.AddService(device.Id, new BleService(device.Id, characteristic.ServiceId, true, [characteristic]));

		await manager.ConnectAsync(device);
		await manager.WriteAsync(characteristic, "HELLO"u8.ToArray());
		var response = await manager.ReadAsync(characteristic);

		Assert.Equal("HELLO"u8.ToArray(), response);
	}

	[Fact]
	public async Task FindCharacteristicAsync_ReturnsDiscoveredCharacteristic()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		var device = ManagerHarness.Device();
		var serviceId = BleUuid.FromShort(0x180A);
		var characteristicId = BleUuid.FromShort(0x2A29);
		transport.AddService(device.Id, new BleService(device.Id, serviceId, true, [
			new BleCharacteristic(device.Id, serviceId, characteristicId, BleCharacteristicProperties.Read)
		]));

		await manager.ConnectAsync(device);
		var found = await manager.FindCharacteristicAsync(serviceId, characteristicId, device);

		Assert.NotNull(found);
		Assert.Equal(characteristicId, found!.Id);
	}

	[Fact]
	public async Task ReadAsync_WhenDisconnected_ThrowsNotConnected()
	{
		var (manager, _, _, _) = ManagerHarness.Create();
		var characteristic = new BleCharacteristic("missing", Guid.NewGuid(), Guid.NewGuid(), BleCharacteristicProperties.Read);

		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() => manager.ReadAsync(characteristic));
		Assert.Equal(BluetoothManagerError.NotConnected, error.Error);
	}

	[Fact]
	public void BleUuid_FromShort_UsesBluetoothBase()
	{
		var uuid = BleUuid.FromShort(0x180A);
		Assert.Equal(Guid.Parse("0000180a-0000-1000-8000-00805f9b34fb"), uuid);
		Assert.Equal(uuid, BleUuid.Parse("180A"));
	}
}
