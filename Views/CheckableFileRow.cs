using System.ComponentModel;
using System.Runtime.CompilerServices;
using BimLinkManager.Models;

namespace BimLinkManager.Views
{
    public class CheckableFileRow : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isAlreadyLinked;

        public AccRvtFile File { get; }

        public CheckableFileRow(AccRvtFile file)
        {
            File = file;
        }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        // F1: the cloud model is already linked into the active document.
        public bool IsAlreadyLinked
        {
            get { return _isAlreadyLinked; }
            set
            {
                if (_isAlreadyLinked != value)
                {
                    _isAlreadyLinked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAlreadyLinked)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
