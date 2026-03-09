using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace EZ2Play.App
{
    public static class PackLoader
    {
        private const string PackFileName = "ui.pack";

        // Загружает файл из ui.pack в MemoryStream.
        public static MemoryStream LoadFromPack(string fileName)
        {
            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string packPath = Path.Combine(exeDir, PackFileName);
                if (!File.Exists(packPath)) return null;

                using (var archive = ZipFile.OpenRead(packPath))
                {
                    var entry = archive.GetEntry(fileName);
                    if (entry == null) return null;

                    using (var entryStream = entry.Open())
                    {
                        var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
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