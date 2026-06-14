using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimLinkManager.Models
{
    public class LinkConfiguration : INotifyPropertyChanged
    {
        private string _worksetName;
        private PositioningSystem _positioning = PositioningSystem.AutoOriginToInternalOrigin;
        private LinkReferenceType _referenceType = LinkReferenceType.Overlay;

        public AccRvtFile File { get; set; }

        public PositioningSystem Positioning
        {
            get { return _positioning; }
            set { Set(ref _positioning, value); }
        }

        public LinkReferenceType ReferenceType
        {
            get { return _referenceType; }
            set { Set(ref _referenceType, value); }
        }

        public string WorksetName
        {
            get { return _worksetName; }
            set { Set(ref _worksetName, value); }
        }

        public string DisplayName
        {
            get { return File?.DisplayName ?? File?.Name ?? "(file)"; }
        }

        public string FolderPathDisplay
        {
            get { return File?.Folder?.FullPath ?? string.Empty; }
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
