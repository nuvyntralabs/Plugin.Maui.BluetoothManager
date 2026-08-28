using Microsoft.Maui.ApplicationModel;

namespace Plugin.Maui.BluetoothManager;

sealed class MauiBluetoothPermissionGateway : IBluetoothPermissionGateway
{
	public Task<BluetoothPermissionStatus> CheckAsync() => EvaluateAsync(request: false);

	public Task<BluetoothPermissionStatus> RequestAsync() => EvaluateAsync(request: true);

	static async Task<BluetoothPermissionStatus> EvaluateAsync(bool request)
	{
#if ANDROID || IOS
		var bluetooth = request
			? await Permissions.RequestAsync<Permissions.Bluetooth>().ConfigureAwait(false)
			: await Permissions.CheckStatusAsync<Permissions.Bluetooth>().ConfigureAwait(false);

		var status = Map(bluetooth);
		if (status != BluetoothPermissionStatus.Granted)
			return status;

#if ANDROID
		if (!OperatingSystem.IsAndroidVersionAtLeast(31))
		{
			var location = request
				? await Permissions.RequestAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false)
				: await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(false);

			var locationStatus = Map(location);
			if (locationStatus != BluetoothPermissionStatus.Granted)
				return locationStatus;
		}
#endif

		return BluetoothPermissionStatus.Granted;
#else
		return BluetoothPermissionStatus.Unknown;
#endif
	}

	static BluetoothPermissionStatus Map(PermissionStatus status) => status switch
	{
		PermissionStatus.Granted => BluetoothPermissionStatus.Granted,
		PermissionStatus.Denied => BluetoothPermissionStatus.Denied,
		PermissionStatus.Disabled => BluetoothPermissionStatus.DeniedForever,
		PermissionStatus.Restricted => BluetoothPermissionStatus.Restricted,
		_ => BluetoothPermissionStatus.Unknown
	};
}
