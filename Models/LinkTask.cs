using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimLinkManager.Models
{
    public class LinkTask : INotifyPropertyChanged
    {
        private LinkTaskStatus _status = LinkTaskStatus.Pending;
        private string _statusMessage = string.Empty;
        private double _progressPercent;

        public LinkConfiguration Configuration { get; }

        public LinkTask(LinkConfiguration config)
        {
            Configuration = config;
        }

        public string DisplayName
        {
            get { return Configuration?.DisplayName ?? "(unknown)"; }
        }

        public LinkTaskStatus Status
        {
            get { return _status; }
            set
            {
                if (Set(ref _status, value))
                {
                    Notify(nameof(StatusDisplay));
                    Notify(nameof(IsTerminal));
                }
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set { Set(ref _statusMessage, value); }
        }

        public double ProgressPercent
        {
            get { return _progressPercent; }
            set { Set(ref _progressPercent, value); }
        }

        public string StatusDisplay
        {
            get
            {
                switch (_status)
                {
                    case LinkTaskStatus.Pending:   return "PENDING";
                    case LinkTaskStatus.Running:   return "LINKING";
                    case LinkTaskStatus.Succeeded: return "LINKED";
                    case LinkTaskStatus.Failed:    return "FAILED";
                    case LinkTaskStatus.Cancelled: return "CANCELLED";
                    case LinkTaskStatus.Skipped:   return "SKIPPED";
                    default: return _status.ToString().ToUpperInvariant();
                }
            }
        }

        public bool IsTerminal
        {
            get
            {
                return _status == LinkTaskStatus.Succeeded
                    || _status == LinkTaskStatus.Failed
                    || _status == LinkTaskStatus.Cancelled
                    || _status == LinkTaskStatus.Skipped;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            Notify(propertyName);
            return true;
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
