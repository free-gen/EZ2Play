using System.ComponentModel;
using System.Windows.Media;

namespace EZParser.Models
{
    public class GridResult : INotifyPropertyChanged
    {
        private bool _isSelected;
        private ImageSource _imageSource;

        public int Id { get; set; }
        public string Url { get; set; }
        public string Thumb { get; set; }
        public string Style { get; set; }
        public string Author { get; set; }
        public string Name { get; set; } // Добавлено для отображения в UI

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