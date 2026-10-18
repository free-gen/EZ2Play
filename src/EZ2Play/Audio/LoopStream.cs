
using NAudio.Wave;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides continuous audio playback by restarting
    /// an MP3 source when it reaches the end.
    /// </summary>
    /// <remarks>
    /// Wraps an existing IWaveProvider and reads audio
    /// data into the caller's buffer.
    ///
    /// When the underlying provider is an Mp3FileReader,
    /// reaching the end resets its position to zero.
    ///
    /// Other provider types are not automatically restarted.
    /// </remarks>
    internal class LoopStream : IWaveProvider
    {
        // Underlying audio source.
        private readonly IWaveProvider _source;

        /// <summary>
        /// Initializes the looping provider with
        /// the specified audio source.
        /// </summary>
        public LoopStream(IWaveProvider source)
        {
            _source = source;
        }

        /// <summary>
        /// Gets the audio format of the underlying source.
        /// </summary>
        public WaveFormat WaveFormat => _source.WaveFormat;

        /// <summary>
        /// Reads audio data and restarts the MP3 source
        /// whenever it reaches the end of the stream.
        /// </summary>
        /// <remarks>
        /// Reading continues until the requested number
        /// of bytes has been read or the source cannot
        /// be restarted.
        ///
        /// Already-read bytes remain in the output buffer
        /// when the source is restarted.
        ///
        /// The original behavior is preserved, including
        /// the absence of an additional empty-stream guard.
        /// </remarks>
        public int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                // Read the remaining number of requested bytes.
                int read = _source.Read(
                    buffer,
                    offset + totalRead,
                    count - totalRead);

                if (read == 0)
                {
                    // Restart MP3 playback from the beginning.
                    if (_source is Mp3FileReader mp3Reader)
                        mp3Reader.Position = 0;
                    else
                        break;
                }

                // Track the total number of bytes read.
                totalRead += read;
            }

            return totalRead;
        }
    }
}
