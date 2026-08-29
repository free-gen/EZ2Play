using System;
using System.Collections.Generic;
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

        private const int MaxSfxVoices = 3;

        private byte[] _moveData;
        private byte[] _launchData;
        private byte[] _backData;

        private readonly object _sfxLock = new object();
        private readonly List<SfxVoice> _sfxVoices = new List<SfxVoice>();

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
                _moveData = LoadSoundBytes(ResMove, "Focus.mp3");
                _launchData = LoadSoundBytes(ResLaunch, "Invoke.mp3");
                _backData = LoadSoundBytes(ResBack, "Back.mp3");
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

        private static byte[] LoadSoundBytes(string resourceName, string fileName)
        {
            using (var stream = LoadSound(resourceName, fileName))
            {
                return stream?.ToArray();
            }
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

        public void PlayMoveSound() => PlaySfx(_moveData);
        public void PlayLaunchSound() => PlaySfx(_launchData);
        public void PlayBackSound() => PlaySfx(_backData);

        private void PlaySfx(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            SfxVoice voice = null;
            SfxVoice oldestVoice = null;

            try
            {
                voice = new SfxVoice(data);
                voice.Player.PlaybackStopped += (s, e) => ReleaseSfxVoice(voice);

                lock (_sfxLock)
                {
                    if (_sfxVoices.Count >= MaxSfxVoices)
                    {
                        oldestVoice = _sfxVoices[0];
                        _sfxVoices.RemoveAt(0);
                    }

                    _sfxVoices.Add(voice);
                }

                oldestVoice?.Dispose();
                voice.Player.Play();
            }

            catch
            {
                ReleaseSfxVoice(voice);
            }
        }

        private void ReleaseSfxVoice(SfxVoice voice)
        {
            if (voice == null) return;

            lock (_sfxLock)
            {
                _sfxVoices.Remove(voice);
            }

            voice.Dispose();
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

        private sealed class SfxVoice : IDisposable
        {
            private readonly MemoryStream _stream;
            private readonly Mp3FileReader _reader;
            private bool _disposed;

            public WaveOutEvent Player { get; }

            public SfxVoice(byte[] data)
            {
                _stream = new MemoryStream(data, false);
                _reader = new Mp3FileReader(_stream);

                Player = new WaveOutEvent();
                Player.Init(new VolumeWaveProvider(_reader, SfxVolume));
                Player.Volume = 1f;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;

                Player?.Stop();
                Player?.Dispose();
                _reader?.Dispose();
                _stream?.Dispose();
            }
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

            List<SfxVoice> voices;

            lock (_sfxLock)
            {
                voices = new List<SfxVoice>(_sfxVoices);
                _sfxVoices.Clear();
            }

            foreach (var voice in voices)
                voice.Dispose();

            _moveData = null;
            _launchData = null;
            _backData = null;

            _backgroundPlayer?.Stop();
            _backgroundPlayer?.Dispose();
            _backgroundReader?.Dispose();
            _menuStream?.Dispose();
        }
    }
}