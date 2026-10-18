
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace EZ2Play.App
{
    /// <summary>
    /// Provides access to application UI resources
    /// stored inside the external ui.pack archive.
    /// </summary>
    /// <remarks>
    /// The archive is expected to be located in the
    /// same directory as the application executable.
    ///
    /// Resources are extracted into memory streams
    /// so callers can read them independently of the archive.
    /// </remarks>
    public static class PackLoader
    {
        private const string PackFileName = "ui.pack";

        /// <summary>
        /// Loads a file from ui.pack into a MemoryStream.
        /// </summary>
        /// <param name="fileName">
        /// Name of the resource entry inside the archive.
        /// </param>
        /// <returns>
        /// A readable MemoryStream containing the requested
        /// resource, or null if the archive or entry is missing
        /// or an error occurs.
        /// </returns>
        /// <remarks>
        /// The returned stream is positioned at the beginning.
        /// The caller is responsible for disposing of it.
        ///
        /// The ZIP archive and entry stream are closed
        /// before this method returns.
        /// </remarks>
        public static MemoryStream LoadFromPack(string fileName)
        {
            try
            {
                // Resolve the directory containing the executable.
                string exeDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location);

                string packPath = Path.Combine(
                    exeDir,
                    PackFileName);

                // An external resource pack is optional.
                if (!File.Exists(packPath))
                    return null;

                using (var archive = ZipFile.OpenRead(packPath))
                {
                    // Locate the requested entry inside the archive.
                    var entry = archive.GetEntry(fileName);

                    if (entry == null)
                        return null;

                    using (var entryStream = entry.Open())
                    {
                        // Copy the resource into an independent stream.
                        var ms = new MemoryStream();

                        entryStream.CopyTo(ms);

                        // Make the returned stream ready for reading.
                        ms.Position = 0;

                        return ms;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
