using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace BimLinkManager.Models
{
    /// <summary>
    /// Load state of a RevitLinkType as the Links view reports it (F4).
    /// </summary>
    public enum RowLinkStatus
    {
        Loaded = 0,
        Unloaded = 1,
        NotFound = 2
    }

    /// <summary>
    /// One row in the Link List view. Wraps a RevitLinkType element with
    /// the projected fields the UI shows.
    /// </summary>
    public class RevitLinkRow : INotifyPropertyChanged
    {
        private RowLinkStatus _status = RowLinkStatus.Unloaded;
        private string _pathType;
        private string _worksetName;
        private bool _isBusy;

        public ElementId LinkTypeId { get; set; }
        public string Name { get; set; }

        public RowLinkStatus Status
        {
            get { return _status; }
            set
            {
                if (Set(ref _status, value))
                {
                    Notify(nameof(StatusDisplay));
                    Notify(nameof(IsLoadedVisibility));
                    Notify(nameof(IsUnloadedVisibility));
                    Notify(nameof(IsNotFoundVisibility));
                }
            }
        }

        public string PathType
        {
            get { return _pathType; }
            set { Set(ref _pathType, value); }
        }

        public string WorksetName
        {
            get { return _worksetName; }
            set { Set(ref _worksetName, value); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set { Set(ref _isBusy, value); }
        }

        public string StatusDisplay
        {
            get
            {
                switch (_status)
                {
                    case RowLinkStatus.Loaded:   return "LOADED";
                    case RowLinkStatus.NotFound: return "NOT FOUND";
                    default:                     return "UNLOADED";
                }
            }
        }

        public System.Windows.Visibility IsLoadedVisibility
            => _status == RowLinkStatus.Loaded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility IsUnloadedVisibility
            => _status == RowLinkStatus.Unloaded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility IsNotFoundVisibility
            => _status == RowLinkStatus.NotFound ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
