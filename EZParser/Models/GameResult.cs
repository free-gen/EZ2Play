using System.ComponentModel;
using System.Windows.Media;

namespace EZParser.Models
{
    public class GameResult : INotifyPropertyChanged
    {
        private bool _isSelected;
        private ImageSource _imageSource;

        public string Name { get; set; }
        public string Price { get; set; }
        public string Type { get; set; }
        public string CoverUrl { get; set; }
        public string Status { get; set; } = "Не скачано";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ImageSource ImageSource
        {
            get => _imageSource;
            set
            {
                _imageSource = value;
                OnPropertyChanged(nameof(ImageSource));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}