using System;
using System.IO;
using System.Media;

namespace EZ2Play
{
    public class Sound : IDisposable
    {
        private SoundPlayer _moveSoundPlayer;
        private SoundPlayer _launchSoundPlayer;
        private SoundPlayer _backSoundPlayer;
        private MemoryStream _moveStream;
        private MemoryStream _launchStream;
        private MemoryStream _backStream;

        public Sound()
        {
            InitializeMoveSound();
            InitializeLaunchSound();
            InitializeBackSound();
        }

        private void InitializeMoveSound()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "EZ2Play.src.move.wav";
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;
            _moveStream = new MemoryStream();
            stream.CopyTo(_moveStream);
            _moveStream.Position = 0;
            _moveSoundPlayer = new SoundPlayer(_moveStream);
        }

        private void InitializeLaunchSound()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "EZ2Play.src.launch.wav";
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;
            _launchStream = new MemoryStream();
            stream.CopyTo(_launchStream);
            _launchStream.Position = 0;
            _launchSoundPlayer = new SoundPlayer(_launchStream);
        }

        private void InitializeBackSound()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "EZ2Play.src.back.wav";
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;
            _backStream = new MemoryStream();
            stream.CopyTo(_backStream);
            _backStream.Position = 0;
            _backSoundPlayer = new SoundPlayer(_backStream);
        }

        public void PlayMoveSound()
        {
            if (_moveSoundPlayer != null)
            {
                _moveSoundPlayer.Play();
            }
        }

        public void PlayLaunchSound()
        {
            if (_launchSoundPlayer != null)
            {
                _launchSoundPlayer.Play();
            }
        }

        public void PlayBackSound()
        {
            if (_backSoundPlayer != null)
            {
                _backSoundPlayer.Play();
            }
        }

        public void Dispose()
        {
            _moveSoundPlayer?.Dispose();
            _launchSoundPlayer?.Dispose();
            _backSoundPlayer?.Dispose();
            _moveStream?.Dispose();
            _launchStream?.Dispose();
            _backStream?.Dispose();
        }
    }
} 