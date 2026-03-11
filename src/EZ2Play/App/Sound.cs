using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NAudio.Wave;

namespace EZ2Play.App
{
    // --------------- Класс управления громкостью волны ---------------

    internal sealed class VolumeWaveProvider : IWaveProvider
    {
        // Максимальная громкость (2.0 = +6dB)
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

            // Полная тишина
            if (_volume <= 0.001f)
            {
                Array.Clear(buffer, offset, read);
                return read;
            }

            // Громкость без изменений (оптимизация)
            if (_volume >= 0.999f && _volume <= 1.001f)
                return read;

            int bps = WaveFormat.BitsPerSample;

            // 16-bit PCM
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
            // 32-bit Float
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

    // --------------- Класс управления звуком (SFX + музыка) ---------------

    public class Sound : IDisposable
    {
        // --------------- Настройки ---------------

        // Отключение фоновой музыки (для отладки)
        public static bool DisableMusic { get; set; } = false;

        // Громкость эффектов (0.0 - 2.0)
        private const float SfxVolume = 0.65f;

        // Громкость фоновой музыки (0.0 - 2.0)
        private const float MusicVolume = 0.80f;

        // Длительность fade in/out (мс)
        public const int FadeDurationMs = 1000;

        // Ресурсы звуковых файлов
        private const string ResMove = "EZ2Play.Assets.select.mp3";
        private const string ResLaunch = "EZ2Play.Assets.action.mp3";
        private const string ResBack = "EZ2Play.Assets.abort.mp3";
        private const string ResMenu = "EZ2Play.Assets.ambient.mp3";

        // --------------- Поля SFX ---------------

        private Mp3FileReader _moveReader;
        private Mp3FileReader _launchReader;
        private Mp3FileReader _backReader;

        private MemoryStream _moveStream;
        private MemoryStream _launchStream;
        private MemoryStream _backStream;

        private WaveOutEvent _sfxPlayer;

        // --------------- Поля музыки ---------------

        private WaveOutEvent _backgroundPlayer;
        private Mp3FileReader _backgroundReader;
        private VolumeWaveProvider _musicVolumeProvider;
        private MemoryStream _menuStream;

        private bool _isBackgroundPlaying;

        // --------------- Публичные свойства ---------------

        public bool IsBackgroundPlaying => _isBackgroundPlaying;

        // --------------- Конструктор ---------------

        public Sound()
        {
            InitializeSfx();
            InitializeBackgroundMusic();
        }

        // --------------- Инициализация SFX ---------------

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

        // --------------- Инициализация музыки ---------------

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

        // --------------- Загрузка звуков ---------------

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

        // --------------- Воспроизведение SFX ---------------

        public void PlayMoveSound() => PlaySfx(_moveReader);
        public void PlayLaunchSound() => PlaySfx(_launchReader);
        public void PlayBackSound() => PlaySfx(_backReader);

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

        // --------------- Управление фоновой музыкой ---------------

        public void PlayBackgroundMusic(int fadeMs = FadeDurationMs)
        {
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

            (_musicVolumeProvider.Source as FadeWaveProvider)?.FadeTo(0f, fadeMs);

            Task.Run(async () =>
            {
                await Task.Delay(fadeMs);
                _isBackgroundPlaying = false;
                _backgroundPlayer.Stop();
            });
        }

        // --------------- Вспомогательные классы ---------------

        // Провайдер с плавным изменением громкости (fade in/out)
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

                // 16-bit PCM
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
                // 32-bit Float
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

        // Провайдер с зацикливанием потока (для фоновой музыки)
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

        // --------------- Очистка ресурсов ---------------

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