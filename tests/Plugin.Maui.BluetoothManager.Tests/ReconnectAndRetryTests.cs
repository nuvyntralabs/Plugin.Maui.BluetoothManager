namespace Plugin.Maui.BluetoothManager.Tests;

public sealed class ReconnectAndRetryTests
{
	[Fact]
	public void ReconnectPolicy_DoublesUntilMax()
	{
		var first = ReconnectPolicy.DelayForAttempt(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
		var second = ReconnectPolicy.DelayForAttempt(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
		var capped = ReconnectPolicy.DelayForAttempt(8, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));

		Assert.Equal(TimeSpan.FromSeconds(1), first);
		Assert.Equal(TimeSpan.FromSeconds(2), second);
		Assert.Equal(TimeSpan.FromSeconds(10), capped);
	}

	[Fact]
	public void ReconnectPolicy_HonorsDisabledFlag()
	{
		var options = new ConnectOptions { AutoReconnect = false };
		var defaults = new BluetoothManagerOptions { AutoReconnect = true, MaxReconnectAttempts = 5 };

		Assert.False(ReconnectPolicy.ShouldReconnect(options, defaults, 0));
	}

	[Fact]
	public async Task UnexpectedDrop_AutoReconnects()
	{
		var (manager, transport, _, _) = ManagerHarness.Create(options =>
		{
			options.AutoReconnect = true;
			options.MaxReconnectAttempts = 3;
		});

		var device = await manager.ConnectAsync(ManagerHarness.Device());
		var firstConnects = transport.ConnectAttempts;

		transport.Drop(device.Id);

		var connected = false;
		for (var i = 0; i < 40 && !connected; i++)
		{
			await Task.Delay(10);
			connected = manager.ConnectionState == BluetoothConnectionState.Connected
				&& transport.ConnectAttempts > firstConnects;
		}

		Assert.True(connected);
		Assert.True(transport.ConnectAttempts > firstConnects);
	}

	[Fact]
	public async Task UserDisconnect_DoesNotReconnect()
	{
		var (manager, transport, _, _) = ManagerHarness.Create();
		var device = await manager.ConnectAsync(ManagerHarness.Device());
		var connects = transport.ConnectAttempts;

		await manager.DisconnectAsync(device);
		await Task.Delay(20);

		Assert.Equal(connects, transport.ConnectAttempts);
		Assert.Null(manager.ConnectedDevice);
	}

	[Fact]
	public async Task WriteAsync_RetriesTransientFailures()
	{
		var (manager, transport, _, _) = ManagerHarness.Create(options => options.OperationRetryCount = 2);
		var device = ManagerHarness.Device();
		var characteristic = new BleCharacteristic(device.Id, Guid.NewGuid(), Guid.NewGuid(), BleCharacteristicProperties.Write);
		transport.WritesBeforeSuccess = 2;

		await manager.ConnectAsync(device);
		await manager.WriteAsync(characteristic, [0x01]);

		Assert.Equal(3, transport.WriteAttempts);
	}

	[Fact]
	public async Task WriteAsync_ThrowsAfterRetriesExhausted()
	{
		var (manager, transport, _, _) = ManagerHarness.Create(options => options.OperationRetryCount = 1);
		var device = ManagerHarness.Device();
		var characteristic = new BleCharacteristic(device.Id, Guid.NewGuid(), Guid.NewGuid(), BleCharacteristicProperties.Write);
		transport.WritesBeforeSuccess = 5;

		await manager.ConnectAsync(device);
		var error = await Assert.ThrowsAsync<BluetoothManagerException>(() => manager.WriteAsync(characteristic, [0x01]));

		Assert.Equal(BluetoothManagerError.OperationFailed, error.Error);
		Assert.Equal(2, transport.WriteAttempts);
	}
}
