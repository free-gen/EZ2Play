using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace EZ2Play.App
{
    // Apply volume scaling to an underlying wave provider.
    internal sealed class VolumeWaveProvider : IWaveProvider
    {
        // Maximum volume level, where 2.0 is approximately +6 dB.
        private const float MaxVolume = 2f;

        private readonly IWaveProvider _source;
        private float _volume;

        internal IWaveProvider Source => _source;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Max(0f, Math.Min(MaxVolume, value));
        }

        public VolumeWaveProvider(IWaveProvider source, float volume)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _volume = Math.Max(0f, Math.Min(MaxVolume, volume));
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);

            if (read <= 0) return read;

            if (_volume <= 0.001f)
            {
                Array.Clear(buffer, offset, read);
                return read;
            }

            // Skip scaling when the volume is effectively unchanged.
            if (_volume >= 0.999f && _volume <= 1.001f)
                return read;

            int bps = WaveFormat.BitsPerSample;

            if (bps == 16)
            {
                int sampleCount = read / 2;

                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = offset + i * 2;
                    short s = (short)(buffer[idx] | (buffer[idx + 1] << 8));
                    int scaled = (int)(s * _volume);

                    scaled = Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));

                    buffer[idx] = (byte)(scaled & 0xFF);
                    buffer[idx + 1] = (byte)(scaled >> 8);
                }
            }

            else if (bps == 32 && WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                int sampleCount = read / 4;

                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = offset + i * 4;
                    float s = BitConverter.ToSingle(buffer, idx) * _volume;

                    s = Math.Max(-1f, Math.Min(1f, s));

                    byte[] b = BitConverter.GetBytes(s);
                    Array.Copy(b, 0, buffer, idx, 4);
                }
            }

            return read;
        }
    }

    public class Sound : IDisposable
    {
        // Disable background music for debugging.
        public static bool DisableMusic { get; set; } = false;

        private const float SfxVolume = 0.65f;
        private const float MusicVolume = 0.80f;

        public const int FadeDurationMs = 1000;

        private const string ResMove = "EZ2Play.Assets.Focus.mp3";
        private const string ResLaunch = "EZ2Play.Assets.Invoke.mp3";
        private const string ResBack = "EZ2Play.Assets.Back.mp3";
        private const string ResMenu = "EZ2Play.Assets.Ambient.mp3";

        private Mp3FileReader _moveReader;
        private Mp3FileReader _launchReader;
        private Mp3FileReader _backReader;

        private MemoryStream _moveStream;
        private MemoryStream _launchStream;
        private MemoryStream _backStream;

        private WaveOutEvent _movePlayer;
        private WaveOutEvent _launchPlayer;
        private WaveOutEvent _backPlayer;

        private WaveOutEvent _backgroundPlayer;
        private Mp3FileReader _backgroundReader;
        private VolumeWaveProvider _musicVolumeProvider;
        private MemoryStream _menuStream;

        private bool _isBackgroundPlaying;

        private readonly object _musicStopLock = new object();
        private CancellationTokenSource _pendingMusicStopCts;

        public bool IsBackgroundPlaying => _isBackgroundPlaying;

        public Sound()
        {
            InitializeSfx();
            InitializeBackgroundMusic();
        }

        private void InitializeSfx()
        {
            try
            {
                _movePlayer = new WaveOutEvent();
                _launchPlayer = new WaveOutEvent();
                _backPlayer = new WaveOutEvent();

                _moveStream = LoadSound(ResMove, "Focus.mp3");
                _launchStream = LoadSound(ResLaunch, "Invoke.mp3");
                _backStream = LoadSound(ResBack, "Back.mp3");

                if (_moveStream != null)
                {
                    _moveReader = new Mp3FileReader(_moveStream);
                    _movePlayer.Init(new VolumeWaveProvider(_moveReader, SfxVolume));
                }

                if (_launchStream != null)
                {
                    _launchReader = new Mp3FileReader(_launchStream);
                    _launchPlayer.Init(new VolumeWaveProvider(_launchReader, SfxVolume));
                }

                if (_backStream != null)
                {
                    _backReader = new Mp3FileReader(_backStream);
                    _backPlayer.Init(new VolumeWaveProvider(_backReader, SfxVolume));
                }
            }

            catch
            {
            }
        }

        private void InitializeBackgroundMusic()
        {
            try
            {
                _menuStream = LoadSound(ResMenu, "Ambient.mp3");

                if (_menuStream == null) return;

                _backgroundReader = new Mp3FileReader(_menuStream);

                var looped = new LoopStream(_backgroundReader);
                var fadeProvider = new FadeWaveProvider(looped, 0f);

                _musicVolumeProvider = new VolumeWaveProvider(fadeProvider, MusicVolume);

                _backgroundPlayer = new WaveOutEvent();
                _backgroundPlayer.Init(_musicVolumeProvider);
            }

            catch
            {
            }
        }

        // Prefer ui.pack and fall back to the embedded resource.
        private static MemoryStream LoadSound(string resourceName, string fileName)
        {
            return PackLoader.LoadFromPack(fileName) ?? LoadEmbeddedToMemory(resourceName);
        }

        private static MemoryStream LoadEmbeddedToMemory(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var res = assembly.GetManifestResourceStream(resourceName))
            {
                if (res == null) return null;

                var ms = new MemoryStream();
                res.CopyTo(ms);
                ms.Position = 0;

                return ms;
            }
        }

        public void PlayMoveSound() => PlaySfx(_movePlayer, _moveReader);
        public void PlayLaunchSound() => PlaySfx(_launchPlayer, _launchReader);
        public void PlayBackSound() => PlaySfx(_backPlayer, _backReader);

        private void PlaySfx(WaveOutEvent player, Mp3FileReader reader)
        {
            if (player == null || reader == null) return;

            try
            {
                player.Stop();
                reader.Position = 0;
                player.Play();
            }

            catch
            {
            }
        }

        private void CancelPendingMusicStop()
        {
            lock (_musicStopLock)
            {
                if (_pendingMusicStopCts == null) return;

                _pendingMusicStopCts.Cancel();
                _pendingMusicStopCts = null;
            }
        }

        public void PlayBackgroundMusic(int fadeMs = FadeDurationMs)
        {
            CancelPendingMusicStop();

            if (DisableMusic || _backgroundPlayer == null || _backgroundReader == null)
                return;

            _isBackgroundPlaying = true;
            _backgroundReader.Position = 0;
            _backgroundPlayer.Play();

            (_musicVolumeProvider.Source as FadeWaveProvider)?.FadeTo(1f, fadeMs);
        }

        public void StopBackgroundMusicSafe(int fadeMs = FadeDurationMs)
        {
            if (_backgroundPlayer == null) return;

            CancellationTokenSource stopCts;

            lock (_musicStopLock)
            {
                _pendingMusicStopCts?.Cancel();

                stopCts = new CancellationTokenSource();
                _pendingMusicStopCts = stopCts;
            }

            (_musicVolumeProvider.Source as FadeWaveProvider)?.FadeTo(0f, fadeMs);

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(fadeMs, stopCts.Token);

                    lock (_musicStopLock)
                    {
                        // A new Play or Stop may have been requested while waiting.
                        if (stopCts.IsCancellationRequested || !ReferenceEquals(_pendingMusicStopCts, stopCts))
                            return;

                        _pendingMusicStopCts = null;

                        _isBackgroundPlaying = false;
                        _backgroundPlayer?.Stop();
                    }
                }

                catch (TaskCanceledException)
                {
                    // Expected when music is requested again before fade-out completes.
                }

                finally
                {
                    stopCts.Dispose();
                }
            });
        }

        // Apply smooth fade-in and fade-out volume changes.
        internal class FadeWaveProvider : IWaveProvider
        {
            private readonly IWaveProvider _source;
            private float _targetVolume;
            private float _currentVolume;
            private float _fadeStep;

            public FadeWaveProvider(IWaveProvider source, float startVolume = 0f)
            {
                _source = source;
                _currentVolume = startVolume;
                _targetVolume = startVolume;
            }

            public WaveFormat WaveFormat => _source.WaveFormat;

            public void FadeTo(float target, int milliseconds, int sampleRate = 44100)
            {
                _targetVolume = target;

                int steps = (milliseconds * sampleRate) / 1000;
                _fadeStep = (_targetVolume - _currentVolume) / Math.Max(1, steps);
            }

            public int Read(byte[] buffer, int offset, int count)
            {
                int read = _source.Read(buffer, offset, count);

                if (read <= 0) return read;

                int bps = WaveFormat.BitsPerSample;

                if (bps == 16)
                {
                    int samples = read / 2;

                    for (int i = 0; i < samples; i++)
                    {
                        int idx = offset + i * 2;
                        short s = (short)(buffer[idx] | (buffer[idx + 1] << 8));

                        s = (short)(s * _currentVolume);

                        buffer[idx] = (byte)(s & 0xFF);
                        buffer[idx + 1] = (byte)(s >> 8);

                        _currentVolume += _fadeStep;

                        if ((_fadeStep > 0 && _currentVolume > _targetVolume) ||
                            (_fadeStep < 0 && _currentVolume < _targetVolume))
                            _currentVolume = _targetVolume;
                    }
                }

                else if (bps == 32 && WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    int samples = read / 4;

                    for (int i = 0; i < samples; i++)
                    {
                        int idx = offset + i * 4;
                        float s = BitConverter.ToSingle(buffer, idx) * _currentVolume;
                        byte[] b = BitConverter.GetBytes(s);

                        Array.Copy(b, 0, buffer, idx, 4);

                        _currentVolume += _fadeStep;

                        if ((_fadeStep > 0 && _currentVolume > _targetVolume) ||
                            (_fadeStep < 0 && _currentVolume < _targetVolume))
                            _currentVolume = _targetVolume;
                    }
                }

                return read;
            }
        }

        // Restart the source when it reaches the end.
        internal class LoopStream : IWaveProvider
        {
            private readonly IWaveProvider _source;

            public LoopStream(IWaveProvider source)
            {
                _source = source;
            }

            public WaveFormat WaveFormat => _source.WaveFormat;

            public int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;

                while (totalRead < count)
                {
                    int read = _source.Read(buffer, offset + totalRead, count - totalRead);

                    if (read == 0)
                    {
                        if (_source is Mp3FileReader mp3Reader)
                            mp3Reader.Position = 0;
                        else
                            break;
                    }

                    totalRead += read;
                }

                return totalRead;
            }
        }

        public void Dispose()
        {
            CancelPendingMusicStop();

            _isBackgroundPlaying = false;

            _movePlayer?.Stop();
            _movePlayer?.Dispose();
            _movePlayer = null;

            _launchPlayer?.Stop();
            _launchPlayer?.Dispose();
            _launchPlayer = null;

            _backPlayer?.Stop();
            _backPlayer?.Dispose();
            _backPlayer = null;

            _moveReader?.Dispose();
            _launchReader?.Dispose();
            _backReader?.Dispose();

            _moveStream?.Dispose();
            _launchStream?.Dispose();
            _backStream?.Dispose();

            _backgroundPlayer?.Stop();
            _backgroundPlayer?.Dispose();
            _backgroundReader?.Dispose();
            _menuStream?.Dispose();
        }
    }
}