
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace EZ2Play.App
{
    /// <summary>
    /// Manages application sound effects and looping
    /// background music using NAudio.
    /// </summary>
    /// <remarks>
    /// Sound assets are loaded from ui.pack when available
    /// and fall back to embedded assembly resources.
    ///
    /// Sound effects use independent playback voices.
    /// Background music uses volume and fade providers
    /// to control playback levels.
    /// </remarks>
    public class Sound : IDisposable
    {
        // Disable background music for debugging.
        public static bool DisableMusic { get; set; } = false;

        // Default volume levels.
        private const float SfxVolume = 0.65f;
        private const float MusicVolume = 0.80f;

        // Default duration of background music fades.
        public const int FadeDurationMs = 1000;

        // Embedded audio resource names.
        private const string ResMove = "EZ2Play.Assets.Focus.mp3";
        private const string ResLaunch = "EZ2Play.Assets.Invoke.mp3";
        private const string ResBack = "EZ2Play.Assets.Back.mp3";
        private const string ResEvent = "EZ2Play.Assets.Event.mp3";
        private const string ResTab = "EZ2Play.Assets.Tab.mp3";
        private const string ResMenu = "EZ2Play.Assets.Ambient.mp3";

        // Maximum number of simultaneously active sound effects.
        private const int MaxSfxVoices = 3;

        // Preloaded sound effect data.
        private byte[] _moveData;
        private byte[] _launchData;
        private byte[] _backData;
        private byte[] _eventData;
        private byte[] _tabData;

        // Active sound effect playback instances.
        private readonly object _sfxLock = new object();

        private readonly List<SfxVoice> _sfxVoices =
            new List<SfxVoice>();

        // Background music playback resources.
        private WaveOutEvent _backgroundPlayer;
        private Mp3FileReader _backgroundReader;
        private VolumeWaveProvider _musicVolumeProvider;
        private MemoryStream _menuStream;

        // Synchronization and cancellation of delayed music stops.
        private readonly object _musicStopLock = new object();
        private CancellationTokenSource _pendingMusicStopCts;

        public Sound()
        {
            InitializeSfx();
            InitializeBackgroundMusic();
        }

        /// <summary>
        /// Loads all sound effects into memory.
        /// </summary>
        /// <remarks>
        /// Loading failures are ignored to preserve
        /// the original optional-audio behavior.
        /// </remarks>
        private void InitializeSfx()
        {
            try
            {
                _moveData = LoadSoundBytes(ResMove, "Focus.mp3");
                _launchData = LoadSoundBytes(ResLaunch, "Invoke.mp3");
                _backData = LoadSoundBytes(ResBack, "Back.mp3");
                _eventData = LoadSoundBytes(ResEvent, "Event.mp3");
                _tabData = LoadSoundBytes(ResTab, "Tab.mp3");
            }
            catch
            {
            }
        }

        /// <summary>
        /// Initializes the looping background music
        /// and its playback providers.
        /// </summary>
        private void InitializeBackgroundMusic()
        {
            try
            {
                _menuStream = LoadSound(
                    ResMenu,
                    "Ambient.mp3");

                if (_menuStream == null)
                    return;

                _backgroundReader = new Mp3FileReader(_menuStream);

                // Restart the music when the source reaches its end.
                var looped = new LoopStream(_backgroundReader);

                // Music starts with zero fade volume.
                var fadeProvider = new FadeWaveProvider(looped, 0f);

                // Apply the configured music volume separately.
                _musicVolumeProvider = new VolumeWaveProvider(
                    fadeProvider,
                    MusicVolume);

                _backgroundPlayer = new WaveOutEvent();
                _backgroundPlayer.Init(_musicVolumeProvider);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Loads an audio resource from ui.pack or
        /// falls back to the embedded assembly resource.
        /// </summary>
        private static MemoryStream LoadSound(
            string resourceName,
            string fileName)
        {
            return PackLoader.LoadFromPack(fileName)
                ?? LoadEmbeddedToMemory(resourceName);
        }

        /// <summary>
        /// Loads an audio resource and returns its
        /// complete contents as a byte array.
        /// </summary>
        private static byte[] LoadSoundBytes(
            string resourceName,
            string fileName)
        {
            using (var stream = LoadSound(resourceName, fileName))
            {
                return stream?.ToArray();
            }
        }

        /// <summary>
        /// Copies an embedded audio resource into
        /// an independently readable memory stream.
        /// </summary>
        private static MemoryStream LoadEmbeddedToMemory(
            string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (var res = assembly.GetManifestResourceStream(resourceName))
            {
                if (res == null)
                    return null;

                var ms = new MemoryStream();

                res.CopyTo(ms);
                ms.Position = 0;

                return ms;
            }
        }

        // Public sound effect entry points.
        public void PlayMoveSound() => PlaySfx(_moveData);
        public void PlayLaunchSound() => PlaySfx(_launchData);
        public void PlayBackSound() => PlaySfx(_backData);
        public void PlayEventSound() => PlaySfx(_eventData);
        public void PlayTabSound() => PlaySfx(_tabData);

        /// <summary>
        /// Plays a sound effect using an independent
        /// playback voice.
        /// </summary>
        /// <remarks>
        /// No more than MaxSfxVoices are retained.
        /// When the limit is reached, the oldest voice
        /// is removed and disposed.
        ///
        /// Each completed voice releases its own resources
        /// through the PlaybackStopped event.
        /// </remarks>
        private void PlaySfx(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            SfxVoice voice = null;
            SfxVoice oldestVoice = null;

            try
            {
                voice = new SfxVoice(data);

                voice.Player.PlaybackStopped +=
                    (s, e) => ReleaseSfxVoice(voice);

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

        /// <summary>
        /// Removes a completed sound effect voice
        /// and disposes its audio resources.
        /// </summary>
        private void ReleaseSfxVoice(SfxVoice voice)
        {
            if (voice == null)
                return;

            lock (_sfxLock)
            {
                _sfxVoices.Remove(voice);
            }

            voice.Dispose();
        }

        /// <summary>
        /// Cancels a previously scheduled background music stop.
        /// </summary>
        /// <remarks>
        /// Used when music playback is requested again
        /// before a pending fade-out has completed.
        /// </remarks>
        private void CancelPendingMusicStop()
        {
            lock (_musicStopLock)
            {
                if (_pendingMusicStopCts == null)
                    return;

                _pendingMusicStopCts.Cancel();
                _pendingMusicStopCts = null;
            }
        }

        /// <summary>
        /// Starts background music playback and fades
        /// its volume toward the normal playback level.
        /// </summary>
        public void PlayBackgroundMusic(
            int fadeMs = FadeDurationMs)
        {
            CancelPendingMusicStop();

            if (DisableMusic ||
                _backgroundPlayer == null ||
                _backgroundReader == null)
            {
                return;
            }

            _backgroundReader.Position = 0;
            _backgroundPlayer.Play();

            (_musicVolumeProvider.Source as FadeWaveProvider)
                ?.FadeTo(1f, fadeMs);
        }

        /// <summary>
        /// Fades out background music and schedules
        /// playback to stop after the fade duration.
        /// </summary>
        /// <remarks>
        /// The pending stop uses a CancellationTokenSource.
        ///
        /// If playback is requested again before the delay
        /// finishes, the scheduled stop is cancelled.
        ///
        /// The stop operation is synchronized so an outdated
        /// request cannot stop a newer playback session.
        /// </remarks>
        public void StopBackgroundMusicSafe(
            int fadeMs = FadeDurationMs)
        {
            if (_backgroundPlayer == null)
                return;

            CancellationTokenSource stopCts;

            lock (_musicStopLock)
            {
                _pendingMusicStopCts?.Cancel();

                stopCts = new CancellationTokenSource();
                _pendingMusicStopCts = stopCts;
            }

            (_musicVolumeProvider.Source as FadeWaveProvider)
                ?.FadeTo(0f, fadeMs);

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(fadeMs, stopCts.Token);

                    lock (_musicStopLock)
                    {
                        // A new Play or Stop may have been
                        // requested while waiting.
                        if (stopCts.IsCancellationRequested ||
                            !ReferenceEquals(
                                _pendingMusicStopCts,
                                stopCts))
                        {
                            return;
                        }

                        _pendingMusicStopCts = null;
                        _backgroundPlayer?.Stop();
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when music is requested again
                    // before fade-out completes.
                }
                finally
                {
                    stopCts.Dispose();
                }
            });
        }

        /// <summary>
        /// Represents an individual sound effect
        /// playback instance.
        /// </summary>
        /// <remarks>
        /// Each instance owns its MP3 reader, memory stream,
        /// and WaveOutEvent player.
        ///
        /// This class remains nested inside Sound.
        /// </remarks>
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

                Player.Init(
                    new VolumeWaveProvider(_reader, SfxVolume));

                Player.Volume = 1f;
            }

            /// <summary>
            /// Stops playback and disposes the
            /// resources owned by this voice.
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                Player?.Stop();
                Player?.Dispose();
                _reader?.Dispose();
                _stream?.Dispose();
            }
        }

        /// <summary>
        /// Releases sound effect voices, background music
        /// playback resources, and loaded audio data.
        /// </summary>
        public void Dispose()
        {
            CancelPendingMusicStop();

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
            _eventData = null;
            _tabData = null;

            _backgroundPlayer?.Stop();
            _backgroundPlayer?.Dispose();
            _backgroundReader?.Dispose();
            _menuStream?.Dispose();
        }
    }
}
