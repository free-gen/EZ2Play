
using System.Windows.Media;

namespace EZ2Play.App
{
    /// <summary>
    /// Stores information about a game shortcut, including its
    /// display name, icon, source path, and source type.
    /// </summary>
    public class ShortcutInfo
    {
        public string Name { get; set; }
        public ImageSource Icon { get; set; }
        public string FullPath { get; set; }
        public string SourceType { get; set; }

        public string DisplayName => Name?.Replace(';', ':');
    }
}
