using System;
using System.IO;
using System.Reflection;
using NAudio.Wave;
using System.IO.Compression;
using System.Threading.Tasks;

namespace EZ2Play.App
{
    internal sealed class VolumeWaveProvider : IWaveProvider
    {
        private const float MaxVolume = 2f;
        private readonly IWaveProvider _source;
        private float _volume;

        internal IWaveProvider Source => _source;

        public float Volume { get => _volume; set => _volume = Math.Max(0f, Math.Min(MaxVolume, value)); }

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

            if (_volume >= 0.999f && _volume <= 1.001f) return read;

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
        public static bool DisableMusic { get; set; } = false;

        private const string SoundPackFile = "sound.pack";

        private const string ResMove = "EZ2Play.src.select.mp3";
        private const string ResLaunch = "EZ2Play.src.action.mp3";
        private const string ResBack = "EZ2Play.src.abort.mp3";
        private const string ResMenu = "EZ2Play.src.ambient.mp3";

        private Mp3FileReader _moveReader;
        private Mp3FileReader _launchReader;
        private Mp3FileReader _backReader;
        private MemoryStream _moveStream;
        private MemoryStream _launchStream;
        private MemoryStream _backStream;

        private WaveOutEvent _sfxPlayer;
        private WaveOutEvent _backgroundPlayer;
        private Mp3FileReader _backgroundReader;
        private VolumeWaveProvider _musicVolumeProvider;
        private MemoryStream _menuStream;
        private bool _isBackgroundPlaying;

        // Громкость эффектов
        private const float SfxVolume = 0.65f;
        // Громкость фоновой музыки
        private const float MusicVolume = 0.80f;

        // Единая константа для fade in/out
        private const int FadeDurationMs = 500;

        public bool IsBackgroundPlaying => _isBackgroundPlaying;

        public Sound()
        {
            InitializeSfx();
            InitializeBackgroundMusic();
        }

        private static MemoryStream LoadFromSoundPack(string fileName)
        {
            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string packPath = Path.Combine(exeDir, SoundPackFile);

                if (!File.Exists(packPath))
                    return null;

                using (var archive = ZipFile.OpenRead(packPath))
                {
                    var entry = archive.GetEntry(fileName);
                    if (entry == null) return null;

                    var ms = new MemoryStream();
                    using (var entryStream = entry.Open())
                        entryStream.CopyTo(ms);

                    ms.Position = 0;
                    return ms;
                }
            }
            catch
            {
                return null;
            }
        }

        private static MemoryStream LoadSound(string resourceName, string fileName)
        {
            var fromPack = LoadFromSoundPack(fileName);
            return fromPack ?? LoadEmbeddedToMemory(resourceName);
        }

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

        private void InitializeSfx()
        {
            try
            {
                _sfxPlayer = new WaveOutEvent();

                _moveStream = LoadSound(ResMove, "select.mp3");
                _launchStream = LoadSound(ResLaunch, "action.mp3");
                _backStream = LoadSound(ResBack, "abort.mp3");

                if (_moveStream != null) _moveReader = new Mp3FileReader(_moveStream);
                if (_launchStream != null) _launchReader = new Mp3FileReader(_launchStream);
                if (_backStream != null) _backReader = new Mp3FileReader(_backStream);
            }
            catch { }
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

        private void InitializeBackgroundMusic()
        {
            try
            {
                _menuStream = LoadSound(ResMenu, "ambient.mp3");
                if (_menuStream == null) return;

                _backgroundReader = new Mp3FileReader(_menuStream);
                var looped = new LoopStream(_backgroundReader);

                var fadeProvider = new FadeWaveProvider(looped, 0f);
                _musicVolumeProvider = new VolumeWaveProvider(fadeProvider, MusicVolume);

                _backgroundPlayer = new WaveOutEvent();
                _backgroundPlayer.Init(_musicVolumeProvider);
            }
            catch { }
        }

        public void PlayBackgroundMusic()
        {
            if (DisableMusic || _backgroundPlayer == null || _backgroundReader == null) return;

            _isBackgroundPlaying = true;
            _backgroundReader.Position = 0;
            _backgroundPlayer.Play();

            (_musicVolumeProvider.Source as FadeWaveProvider)?.FadeTo(1f, FadeDurationMs);
        }

        public void StopBackgroundMusicSafe(int fadeMs)
        {
            if (_backgroundPlayer == null) return;

            (_musicVolumeProvider.Source as FadeWaveProvider)?.FadeTo(0f, fadeMs);

            Task.Run(async () =>
            {
                await Task.Delay(fadeMs);
                _isBackgroundPlaying = false;
                _backgroundPlayer.Stop();
            });
        }

        public void StopBackgroundMusicSafe() => StopBackgroundMusicSafe(FadeDurationMs);

        private void PlaySfx(Mp3FileReader reader)
        {
            if (reader == null || _sfxPlayer == null) return;
            try
            {
                _sfxPlayer.Stop();
                reader.Position = 0;
                var sfxWithVolume = new VolumeWaveProvider(reader, SfxVolume);
                _sfxPlayer.Init(sfxWithVolume);
                _sfxPlayer.Volume = 1f;
                _sfxPlayer.Play();
            }
            catch { }
        }

        public void PlayMoveSound() => PlaySfx(_moveReader);
        public void PlayLaunchSound() => PlaySfx(_launchReader);
        public void PlayBackSound() => PlaySfx(_backReader);

        public void Dispose()
        {
            _isBackgroundPlaying = false;
            _sfxPlayer?.Stop();
            _sfxPlayer?.Dispose();
            _sfxPlayer = null;
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