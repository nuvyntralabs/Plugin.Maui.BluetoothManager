namespace Plugin.Maui.BluetoothManager.Tests;

public sealed class PermissionAndAdapterTests
{
	[Fact]
	public async Task ScanAsync_WhenPermissionDenied_Throws()
	{
		var (manager, _, _, _) = ManagerHarness.Create(permissions: permissions =>
			permissions.Status = BluetoothPermissionStatus.Denied);

		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() => manager.ScanAsync());
		Assert.Equal(BluetoothManagerError.PermissionDenied, error.Error);
	}

	[Fact]
	public async Task ScanAsync_WhenAdapterOff_Throws()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		transport.SetAdapterState(BluetoothAdapterState.Off);

		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() => manager.ScanAsync());
		Assert.Equal(BluetoothManagerError.AdapterUnavailable, error.Error);
	}

	[Fact]
	public async Task ScanAsync_WhenUnsupported_Throws()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		transport.IsSupported = false;

		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() => manager.ScanAsync());
		Assert.Equal(BluetoothManagerError.FeatureNotSupported, error.Error);
	}

	[Fact]
	public async Task RequestPermissionsAsync_UsesGateway()
	{
		var (manager, _, permissions, _) = ManagerHarness.Create();
		var status = await manager.RequestPermissionsAsync();

		Assert.Equal(BluetoothPermissionStatus.Granted, status);
		Assert.Equal(1, permissions.RequestCount);
	}

	[Fact]
	public async Task Snapshot_CapturesAdapterAndConnection()
	{
		var (manager, _, _, _) = ManagerHarness.Create();
		await manager.ConnectAsync(ManagerHarness.Device());

		var snapshot = manager.Snapshot;

		Assert.Equal(BluetoothAdapterState.On, snapshot.AdapterState);
		Assert.Equal(BluetoothConnectionState.Connected, snapshot.ConnectionState);
		Assert.NotNull(snapshot.ConnectedDevice);
		Assert.False(snapshot.IsScanning);
	}

	[Fact]
	public void SharedNetTransport_IsUnsupported()
	{
		var transport = new PlatformBleTransport();
		Assert.False(transport.IsSupported);
		Assert.Equal(BluetoothAdapterState.Unsupported, transport.AdapterState);
	}
}
