using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BimLinkManager.Models;

namespace BimLinkManager.Views
{
    public partial class FolderNodeControl : UserControl
    {
        public static readonly DependencyProperty FolderProperty =
            DependencyProperty.Register(nameof(Folder), typeof(AccFolder), typeof(FolderNodeControl),
                new PropertyMetadata(null, OnFolderChanged));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(FolderNodeControl),
                new PropertyMetadata(false));

        public AccFolder Folder
        {
            get { return (AccFolder)GetValue(FolderProperty); }
            set { SetValue(FolderProperty, value); }
        }

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        private BatchView _owner;

        public FolderNodeControl()
        {
            InitializeComponent();
            Loaded += FolderNodeControl_Loaded;
            Unloaded += FolderNodeControl_Unloaded;
        }

        private void FolderNodeControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_owner != null) _owner.SelectedFolderChanged -= OnSelectedFolderChanged;
            _owner = FindAncestor<BatchView>(this);
            if (_owner != null) _owner.SelectedFolderChanged += OnSelectedFolderChanged;
            UpdateSelectionVisual();
        }

        private void FolderNodeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_owner != null) _owner.SelectedFolderChanged -= OnSelectedFolderChanged;
            _owner = null;
        }

        private void OnSelectedFolderChanged()
        {
            UpdateSelectionVisual();
        }

        private void UpdateSelectionVisual()
        {
            IsSelected = Folder != null && _owner != null && ReferenceEquals(_owner.SelectedFolder, Folder);
        }

        private static void OnFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as FolderNodeControl;
            if (ctrl == null) return;
            ctrl.Bind(e.NewValue as AccFolder);
        }

        private void Bind(AccFolder folder)
        {
            if (folder == null)
            {
                NameText.Text = string.Empty;
                SubfolderList.ItemsSource = null;
                FileChildrenList.ItemsSource = null;
                return;
            }

            NameText.Text = folder.Name ?? "(folder)";
            SubfolderList.ItemsSource = folder.Subfolders;
            FileChildrenList.ItemsSource = folder.Files;
            ApplyExpandedState();
            UpdateSelectionVisual();
        }

        private void ApplyExpandedState()
        {
            var folder = Folder;
            if (folder == null) return;
            ChildrenWrap.Visibility = folder.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            ExpandChevronRotate.Angle = folder.IsExpanded ? 90 : 0;
        }

        private void HeaderBorder_Click(object sender, MouseButtonEventArgs e)
        {
            var folder = Folder;
            if (folder == null) return;

            // Toggle expanded state
            folder.IsExpanded = !folder.IsExpanded;
            ApplyExpandedState();

            // Lazy load on first expand
            if (folder.IsExpanded && !folder.IsLoaded && !folder.IsLoading)
            {
                var batchView = FindAncestor<BatchView>(this);
                if (batchView != null)
                {
                    _ = batchView.LoadFolderContentsAsync(folder);
                }
            }
            else if (folder.IsExpanded && folder.IsLoaded)
            {
                // Already loaded — notify BatchView so it can update file table
                var batchView = FindAncestor<BatchView>(this);
                batchView?.SelectFolder(folder);
            }

            e.Handled = true;
        }

        private void FileNode_Click(object sender, MouseButtonEventArgs e)
        {
            // Single-clicking a file in the tree could focus that file in the
            // table; kept as a no-op (table shows files of the selected
            // folder, not individual file).
            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject from) where T : DependencyObject
        {
            var current = from;
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
