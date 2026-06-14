using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimLinkManager.Models
{
    public class AccFolder : INotifyPropertyChanged
    {
        private bool _isLoaded;
        private bool _isLoading;
        private bool _isExpanded;

        public string Id { get; set; }
        public string Name { get; set; }
        public string ProjectId { get; set; }
        public AccFolder Parent { get; set; }

        public ObservableCollection<AccFolder> Subfolders { get; } = new ObservableCollection<AccFolder>();
        public ObservableCollection<AccRvtFile> Files { get; } = new ObservableCollection<AccRvtFile>();

        public bool IsLoaded
        {
            get { return _isLoaded; }
            set { Set(ref _isLoaded, value); }
        }

        public bool IsLoading
        {
            get { return _isLoading; }
            set { Set(ref _isLoading, value); }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { Set(ref _isExpanded, value); }
        }

        public string FullPath
        {
            get
            {
                if (Parent == null) return Name ?? string.Empty;
                return Parent.FullPath + " / " + (Name ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return Name ?? Id ?? "(folder)";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
