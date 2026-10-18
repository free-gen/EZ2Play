
using System;
using NAudio.Wave;

namespace EZ2Play.App
{
    /// <summary>
    /// Applies smooth fade-in and fade-out volume changes
    /// to audio samples from an underlying IWaveProvider.
    /// </summary>
    /// <remarks>
    /// Volume changes are applied progressively while
    /// audio samples are read.
    ///
    /// Supports 16-bit PCM and 32-bit IEEE floating-point
    /// audio formats.
    ///
    /// The fade duration determines the volume change
    /// applied to each processed sample.
    /// </remarks>
    internal class FadeWaveProvider : IWaveProvider
    {
        // Underlying audio provider.
        private readonly IWaveProvider _source;

        // Requested volume and current fade state.
        private float _targetVolume;
        private float _currentVolume;
        private float _fadeStep;

        /// <summary>
        /// Initializes the provider with an optional
        /// starting volume.
        /// </summary>
        public FadeWaveProvider(
            IWaveProvider source,
            float startVolume = 0f)
        {
            _source = source;
            _currentVolume = startVolume;
            _targetVolume = startVolume;
        }

        /// <summary>
        /// Gets the audio format of the underlying provider.
        /// </summary>
        public WaveFormat WaveFormat => _source.WaveFormat;

        /// <summary>
        /// Sets the target volume and calculates the
        /// per-sample volume adjustment.
        /// </summary>
        /// <param name="target">
        /// Volume level to reach.
        /// </param>
        /// <param name="milliseconds">
        /// Requested fade duration in milliseconds.
        /// </param>
        /// <param name="sampleRate">
        /// Sample rate used to calculate the fade steps.
        /// Defaults to 44100.
        /// </param>
        /// <remarks>
        /// The calculated fade step is applied during Read().
        ///
        /// The current volume is not immediately changed
        /// when a new target is requested.
        /// </remarks>
        public void FadeTo(
            float target,
            int milliseconds,
            int sampleRate = 44100)
        {
            _targetVolume = target;

            int steps = (milliseconds * sampleRate) / 1000;

            _fadeStep =
                (_targetVolume - _currentVolume) /
                Math.Max(1, steps);
        }

        /// <summary>
        /// Reads audio samples and applies the current
        /// fade volume while processing the buffer.
        /// </summary>
        /// <remarks>
        /// The volume is advanced after each processed sample.
        ///
        /// When the target volume is reached, the current
        /// volume is clamped to that target.
        ///
        /// Unsupported sample formats are passed through
        /// without applying the fade.
        /// </remarks>
        public int Read(byte[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);

            if (read <= 0)
                return read;

            int bps = WaveFormat.BitsPerSample;

            // Process signed 16-bit PCM samples.
            if (bps == 16)
            {
                int samples = read / 2;

                for (int i = 0; i < samples; i++)
                {
                    int idx = offset + i * 2;

                    // Read a little-endian 16-bit sample.
                    short s = (short)(
                        buffer[idx] |
                        (buffer[idx + 1] << 8));

                    // Apply the current fade volume.
                    s = (short)(s * _currentVolume);

                    // Write the processed sample back.
                    buffer[idx] = (byte)(s & 0xFF);
                    buffer[idx + 1] = (byte)(s >> 8);

                    // Advance the fade after this sample.
                    _currentVolume += _fadeStep;

                    // Prevent overshooting the target volume.
                    if ((_fadeStep > 0 &&
                         _currentVolume > _targetVolume) ||
                        (_fadeStep < 0 &&
                         _currentVolume < _targetVolume))
                    {
                        _currentVolume = _targetVolume;
                    }
                }
            }
            // Process 32-bit IEEE floating-point samples.
            else if (bps == 32 &&
                WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                int samples = read / 4;

                for (int i = 0; i < samples; i++)
                {
                    int idx = offset + i * 4;

                    // Read the sample and apply the fade volume.
                    float s =
                        BitConverter.ToSingle(buffer, idx) *
                        _currentVolume;

                    byte[] b = BitConverter.GetBytes(s);

                    // Write the processed sample back.
                    Array.Copy(b, 0, buffer, idx, 4);

                    // Advance the fade after this sample.
                    _currentVolume += _fadeStep;

                    // Prevent overshooting the target volume.
                    if ((_fadeStep > 0 &&
                         _currentVolume > _targetVolume) ||
                        (_fadeStep < 0 &&
                         _currentVolume < _targetVolume))
                    {
                        _currentVolume = _targetVolume;
                    }
                }
            }

            return read;
        }
    }
}
