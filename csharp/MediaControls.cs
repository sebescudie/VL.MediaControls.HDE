using System.Runtime.Versioning;
using Windows.Media.Control;

[assembly: ImportAsIs]
[assembly: SupportedOSPlatform("windows10.0.17763")]

namespace MediaControls;

/// <summary>
/// Lists the media sessions Windows knows about (Spotify, Firefox, ...) and the one currently in focus.
/// </summary>
[ProcessNode]
public class MediaSessions : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Spread<MediaSession> _sessions = Spread<MediaSession>.Empty;
    private MediaSession? _current;
    private volatile bool _dirty;

    public MediaSessions()
    {
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        manager.SessionsChanged += (_, _) => _dirty = true;
        manager.CurrentSessionChanged += (_, _) => _dirty = true;
        _manager = manager;
        _dirty = true;
    }

    /// <summary>
    /// All sessions, and the one Windows considers current (the one the media keys would control).
    /// </summary>
    public void Update(out Spread<MediaSession> sessions, out MediaSession? current)
    {
        if (_dirty && _manager is { } manager)
        {
            _dirty = false;
            Rebuild(manager);
        }

        sessions = _sessions;
        current = _current;
    }

    private void Rebuild(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        foreach (var s in _sessions)
            s.Dispose();

        var currentId = manager.GetCurrentSession()?.SourceAppUserModelId;
        var builder = new SpreadBuilder<MediaSession>();
        _current = null;

        foreach (var s in manager.GetSessions())
        {
            var session = new MediaSession(s);
            builder.Add(session);
            if (_current is null && session.AppId == currentId)
                _current = session;
        }

        _sessions = builder.ToSpread();
    }

    public void Dispose()
    {
        foreach (var s in _sessions)
            s.Dispose();
    }
}

/// <summary>
/// One media session (one app). Properties refresh on their own when the app changes track or playback state.
/// </summary>
public class MediaSession : IDisposable
{
    private readonly GlobalSystemMediaTransportControlsSession _session;
    private int _version;

    /// <summary>App user model id, e.g. "Spotify.exe" or Firefox's "308046B0AF4A39CB".</summary>
    public string AppId { get; }
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string Album { get; private set; } = "";
    /// <summary>Cover art as encoded image bytes (PNG/JPEG), or null. Decode with FromEncodedData (SKImage).</summary>
    public byte[]? Thumbnail { get; private set; }
    public bool IsPlaying { get; private set; }
    /// <summary>Playing, Paused, Stopped, Changing, Opened or Closed.</summary>
    public string Status { get; private set; } = "";

    internal MediaSession(GlobalSystemMediaTransportControlsSession session)
    {
        _session = session;
        AppId = session.SourceAppUserModelId;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        ReadPlayback();
        _ = ReadMediaAsync();
    }

    public void Play() => _ = _session.TryPlayAsync();
    public void Pause() => _ = _session.TryPauseAsync();
    public void TogglePlayPause() => _ = _session.TryTogglePlayPauseAsync();
    public void Next() => _ = _session.TrySkipNextAsync();
    public void Previous() => _ = _session.TrySkipPreviousAsync();

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) => ReadPlayback();
    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) => _ = ReadMediaAsync();

    private void ReadPlayback()
    {
        var status = _session.GetPlaybackInfo().PlaybackStatus;
        Status = status.ToString();
        IsPlaying = status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    private async Task ReadMediaAsync()
    {
        var version = Interlocked.Increment(ref _version);
        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            byte[]? thumbnail = null;
            if (props.Thumbnail is { } reference)
            {
                using var stream = (await reference.OpenReadAsync()).AsStreamForRead();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                thumbnail = memory.ToArray();
            }

            // A newer refresh started while we were reading; let it win.
            if (version != _version)
                return;

            Title = props.Title ?? "";
            Artist = props.Artist ?? "";
            Album = props.AlbumTitle ?? "";
            Thumbnail = thumbnail;
        }
        catch
        {
            // Session went away mid-read. The manager will drop it on the next SessionsChanged.
        }
    }

    public void Dispose()
    {
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
    }
}
