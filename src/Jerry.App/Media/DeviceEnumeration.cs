using NAudio.CoreAudioApi;
using Windows.Media.Capture.Frames;

namespace Jerry.App.Media;

public sealed record MediaDevice(string Id, string Name);

public static class DeviceEnumeration
{
    public static IReadOnlyList<MediaDevice> GetMicrophones()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<MediaDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device) devices.Add(new(device.ID, device.FriendlyName));
        }
        return devices;
    }

    public static async Task<IReadOnlyList<MediaDevice>> GetCamerasAsync()
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        return groups.Where(g => g.SourceInfos.Any(i => i.SourceKind == MediaFrameSourceKind.Color))
            .Select(g => new MediaDevice(g.Id, g.DisplayName)).ToArray();
    }
}
