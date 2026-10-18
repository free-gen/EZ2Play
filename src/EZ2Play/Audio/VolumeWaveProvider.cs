
using System;
using NAudio.Wave;

namespace EZ2Play.App
{
    /// <summary>
    /// Applies volume scaling to audio samples provided
    /// by an underlying IWaveProvider.
    /// </summary>
    /// <remarks>
    /// Supports 16-bit PCM and 32-bit IEEE floating-point
    /// audio formats.
    ///
    /// Volume scaling is performed directly in the buffer
    /// returned by the underlying provider.
    /// </remarks>
    internal sealed class VolumeWaveProvider : IWaveProvider
    {
        // Maximum volume level, where 2.0 is approximately +6 dB.
        private const float MaxVolume = 2f;

        private readonly IWaveProvider _source;
        private float _volume;

        /// <summary>
        /// Gets the underlying audio provider.
        /// </summary>
        internal IWaveProvider Source => _source;

        /// <summary>
        /// Gets or sets the volume multiplier.
        /// Values are restricted to the range from 0 to 2.
        /// </summary>
        public float Volume
        {
            get => _volume;
            set => _volume = Math.Max(0f, Math.Min(MaxVolume, value));
        }

        public VolumeWaveProvider(IWaveProvider source, float volume)
        {
            _source = source
                ?? throw new ArgumentNullException(nameof(source));

            _volume = Math.Max(0f, Math.Min(MaxVolume, volume));
        }

        /// <summary>
        /// Gets the audio format of the underlying provider.
        /// </summary>
        public WaveFormat WaveFormat => _source.WaveFormat;

        /// <summary>
        /// Reads audio data from the underlying provider
        /// and applies the current volume multiplier.
        /// </summary>
        /// <remarks>
        /// When volume is effectively zero, the read portion
        /// of the buffer is cleared.
        ///
        /// When volume is effectively one, samples are returned
        /// without additional processing.
        ///
        /// For supported audio formats, the resulting samples
        /// are clamped to their permitted ranges.
        /// </remarks>
        public int Read(byte[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);

            if (read <= 0) return read;

            // Silence the samples when volume is effectively zero.
            if (_volume <= 0.001f)
            {
                Array.Clear(buffer, offset, read);
                return read;
            }

            // Skip scaling when the volume is effectively unchanged.
            if (_volume >= 0.999f && _volume <= 1.001f)
                return read;

            int bps = WaveFormat.BitsPerSample;

            // Scale signed 16-bit PCM samples.
            if (bps == 16)
            {
                int sampleCount = read / 2;

                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = offset + i * 2;

                    // Read a little-endian 16-bit sample.
                    short s = (short)(
                        buffer[idx] |
                        (buffer[idx + 1] << 8));

                    int scaled = (int)(s * _volume);

                    // Prevent overflow beyond the Int16 range.
                    scaled = Math.Max(
                        short.MinValue,
                        Math.Min(short.MaxValue, scaled));

                    // Write the scaled sample back to the buffer.
                    buffer[idx] = (byte)(scaled & 0xFF);
                    buffer[idx + 1] = (byte)(scaled >> 8);
                }
            }
            // Scale 32-bit IEEE floating-point samples.
            else if (bps == 32 &&
                WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
            {
                int sampleCount = read / 4;

                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = offset + i * 4;

                    float s =
                        BitConverter.ToSingle(buffer, idx) * _volume;

                    // Keep floating-point samples in the
                    // supported normalized audio range.
                    s = Math.Max(-1f, Math.Min(1f, s));

                    byte[] b = BitConverter.GetBytes(s);

                    Array.Copy(b, 0, buffer, idx, 4);
                }
            }

            return read;
        }
    }
}
