using System.Collections.ObjectModel;
using Plugin.Maui.BluetoothManager;

namespace BluetoothManager.Sample;

public partial class MainPage : ContentPage, IBluetoothManagerLogger
{
	readonly IBluetoothManager _manager;
	readonly ObservableCollection<BleDevice> _devices = [];
	readonly List<string> _logLines = [];

	public MainPage()
	{
		InitializeComponent();
		_manager = Plugin.Maui.BluetoothManager.BluetoothManager.Current;
		DeviceList.ItemsSource = _devices;

		_manager.AdapterStateChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
		{
			AppendLog($"Adapter {e.Previous} -> {e.Current}");
			RefreshStatus();
		});
		_manager.ConnectionStateChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
		{
			AppendLog($"{e.Device.DisplayName}: {e.Previous} -> {e.Current}");
			RefreshStatus();
		});
		_manager.DeviceDisconnected += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
		{
			AppendLog($"Disconnected {e.Device.DisplayName} ({e.Reason}) {e.Message}");
			RefreshStatus();
		});
		_manager.DeviceDiscovered += (_, e) => MainThread.BeginInvokeOnMainThread(() => Upsert(e.Device));
		_manager.EnableLogging(true, this);
		RefreshStatus();
	}

	async void OnPermissionClicked(object? sender, EventArgs e)
	{
		try
		{
			var status = await _manager.RequestPermissionsAsync();
			AppendLog($"Permission: {status}");
			RefreshStatus();
		}
		catch (Exception ex)
		{
			AppendLog(ex.Message);
		}
	}

	async void OnScanClicked(object? sender, EventArgs e)
	{
		try
		{
			_devices.Clear();
			AppendLog("Scanning...");
			var devices = await _manager.ScanAsync(new ScanOptions
			{
				Duration = TimeSpan.FromSeconds(8),
				NameContains = string.IsNullOrWhiteSpace(NameFilterEntry.Text) ? null : NameFilterEntry.Text
			});

			foreach (var device in devices)
				Upsert(device);

			AppendLog($"Scan returned {devices.Count} device(s).");
		}
		catch (Exception ex)
		{
			AppendLog(ex.Message);
		}
	}

	async void OnStopScanClicked(object? sender, EventArgs e)
	{
		await _manager.StopScanAsync();
		AppendLog("Scan stopped.");
	}

	async void OnConnectClicked(object? sender, EventArgs e)
	{
		if (DeviceList.SelectedItem is not BleDevice device)
		{
			AppendLog("Select a device first.");
			return;
		}

		try
		{
			await _manager.ConnectAsync(device, new ConnectOptions
			{
				AutoReconnect = ReconnectSwitch.IsToggled
			});

			var services = await _manager.GetServicesAsync(device);
			AppendLog($"Connected. {services.Count} service(s) discovered.");
			foreach (var service in services.Take(8))
				AppendLog($"  {service.Id} ({service.Characteristics.Count} char)");
		}
		catch (Exception ex)
		{
			AppendLog(ex.Message);
		}
	}

	async void OnDisconnectClicked(object? sender, EventArgs e)
	{
		try
		{
			await _manager.DisconnectAsync();
		}
		catch (Exception ex)
		{
			AppendLog(ex.Message);
		}
	}

	void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is BleDevice device)
			AppendLog($"Selected {device.DisplayName}");
	}

	void RefreshStatus()
	{
		var snapshot = _manager.Snapshot;
		StatusLabel.Text =
			$"Adapter: {snapshot.AdapterState} · Permission: {snapshot.PermissionStatus}{Environment.NewLine}" +
			$"Connection: {snapshot.ConnectionState} · Scanning: {snapshot.IsScanning}{Environment.NewLine}" +
			(snapshot.ConnectedDevice is { } device
				? $"Connected: {device.DisplayName}"
				: "Connected: none") +
			$"{Environment.NewLine}Platform: {_manager.Platform.NativeApi} ({_manager.Platform.DeviceIdKind})";
	}

	void Upsert(BleDevice device)
	{
		for (var i = 0; i < _devices.Count; i++)
		{
			if (_devices[i].Id == device.Id)
			{
				_devices[i] = device;
				return;
			}
		}

		_devices.Add(device);
	}

	public void Log(BluetoothManagerLogLevel level, string message, Exception? exception = null)
	{
		var line = exception is null
			? $"{DateTime.Now:HH:mm:ss} {level}: {message}"
			: $"{DateTime.Now:HH:mm:ss} {level}: {message} ({exception.GetType().Name})";

		MainThread.BeginInvokeOnMainThread(() => AppendLog(line));
	}

	void AppendLog(string line)
	{
		_logLines.Insert(0, line);
		if (_logLines.Count > 40)
			_logLines.RemoveAt(_logLines.Count - 1);

		LogLabel.Text = string.Join(Environment.NewLine, _logLines);
		RefreshStatus();
	}
}
