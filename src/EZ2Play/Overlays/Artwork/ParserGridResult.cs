
using System.ComponentModel;
using System.Windows.Media;

namespace EZ2Play.App
{
    /// <summary>
    /// Represents an artwork result returned by SteamGridDB.
    /// </summary>
    /// <remarks>
    /// Stores the artwork metadata and its decoded thumbnail.
    ///
    /// Implements INotifyPropertyChanged so WPF bindings
    /// can update the displayed image when asynchronous
    /// thumbnail loading completes.
    /// </remarks>
    public class ParserGridResult : INotifyPropertyChanged
    {
        // Decoded thumbnail displayed in the parser.
        private ImageSource _imageSource;

        /// <summary>
        /// Gets or sets the SteamGridDB artwork identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the URL of the original artwork.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the thumbnail download URL.
        /// </summary>
        public string Thumb { get; set; }

        /// <summary>
        /// Gets or sets the original artwork width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the original artwork height in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the decoded thumbnail image.
        /// </summary>
        /// <remarks>
        /// Raises PropertyChanged after every assignment,
        /// including when the assigned reference is unchanged.
        ///
        /// This preserves the original WPF binding behavior.
        /// </remarks>
        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                _imageSource = value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(ImageSource)));
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
