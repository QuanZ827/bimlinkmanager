using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;
using BimLinkManager.Services;

namespace BimLinkManager.Views
{
    public partial class BatchView : UserControl
    {
        public ObservableCollection<AccFolder> TopFolders { get; } = new ObservableCollection<AccFolder>();
        public ObservableCollection<CheckableFileRow> CurrentFiles { get; } = new ObservableCollection<CheckableFileRow>();
        public ObservableCollection<LinkTask> Queue { get; } = new ObservableCollection<LinkTask>();

        private MainWindow Shell => Window.GetWindow(this) as MainWindow;
        private readonly List<CheckableFileRow> _unfilteredFiles = new List<CheckableFileRow>();
        private string _currentFilter = string.Empty;
        private AccFolder _selectedFolder;
        private string _projectContext = string.Empty;
        private HashSet<string> _linkedModelGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // FolderNodeControl listens to repaint the selected tree row.
        internal event Action SelectedFolderChanged;
        internal AccFolder SelectedFolder => _selectedFolder;

        public BatchView()
        {
            InitializeComponent();
            FolderTree.ItemsSource = TopFolders;
            FileList.ItemsSource = CurrentFiles;
            QueueList.ItemsSource = Queue;
            Queue.CollectionChanged += Queue_CollectionChanged;
        }

        public void OnShellReady()
        {
            InitGlobalConfigOptions();
            RefreshLinkedModelGuids();
            UpdateCounts();
            UpdateEmptyState();
        }

        public void SetProjectContext(string text, bool isError)
        {
            _projectContext = text ?? string.Empty;
            var projectName = Shell?.CurrentProject?.Name;
            ExplorerSubtitle.Text = string.IsNullOrEmpty(projectName) ? _projectContext : projectName;
            UpdateBreadcrumb();
        }

        public void ClearTree()
        {
            TopFolders.Clear();
            CurrentFiles.Clear();
            _unfilteredFiles.Clear();
            _selectedFolder = null;
            SelectedFolderChanged?.Invoke();
            RefreshLinkedModelGuids();
            UpdateBreadcrumb();
            UpdateCounts();
            UpdateEmptyState();
        }

        public async Task LoadTopFoldersAsync(CancellationToken ct)
        {
            if (Shell?.CurrentProject == null) return;
            var folders = await Shell.Acc.GetTopFoldersAsync(Shell.CurrentProject, ct);
            TopFolders.Clear();
            foreach (var f in folders) TopFolders.Add(f);
        }

        public async Task LoadFolderContentsAsync(AccFolder folder)
        {
            if (folder == null || Shell == null) return;
            if (folder.IsLoaded || folder.IsLoading) { SelectFolder(folder); return; }

            folder.IsLoading = true;
            try
            {
                var contents = await Shell.Acc.GetFolderContentsAsync(
                    Shell.CurrentProject.Id, folder.Id, Shell.CurrentProject.Region, default);

                folder.Subfolders.Clear();
                foreach (var sub in contents.Subfolders)
                {
                    sub.Parent = folder;
                    folder.Subfolders.Add(sub);
                }
                folder.Files.Clear();
                foreach (var f in contents.Files)
                {
                    f.Folder = folder;
                    folder.Files.Add(f);
                }
                folder.IsLoaded = true;
                SelectFolder(folder);
            }
            catch (Exception ex)
            {
                Log.Error("Folder load failed: " + folder.Name, ex);
            }
            finally
            {
                folder.IsLoading = false;
            }
        }

        public void SelectFolder(AccFolder folder)
        {
            _selectedFolder = folder;
            _unfilteredFiles.Clear();
            CurrentFiles.Clear();
            if (folder != null)
            {
                foreach (var file in folder.Files)
                {
                    var row = new CheckableFileRow(file);
                    // F1: dim rows whose cloud model is already in the document
                    row.IsAlreadyLinked = file.HasCloudGuids
                        && !string.IsNullOrEmpty(file.ModelGuid)
                        && _linkedModelGuids.Contains(file.ModelGuid);
                    _unfilteredFiles.Add(row);
                }
            }
            ApplyFileFilter(_currentFilter);
            UpdateBreadcrumb();
            SelectedFolderChanged?.Invoke();
        }

        public void ApplyFileFilter(string text)
        {
            _currentFilter = text ?? string.Empty;
            CurrentFiles.Clear();
            foreach (var row in _unfilteredFiles)
            {
                if (string.IsNullOrEmpty(_currentFilter)
                    || (row.File.DisplayName ?? string.Empty).IndexOf(_currentFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    CurrentFiles.Add(row);
                }
            }
            UpdateCounts();
            UpdateEmptyState();
        }

        // ============================================================
        // F1 — already-linked detection
        // ============================================================

        private void RefreshLinkedModelGuids()
        {
            // Active document from the shell (F1 already-linked detection).
            var doc = Shell?.UiApp?.ActiveUIDocument?.Document;
            _linkedModelGuids = LinkListService.GetLinkedCloudModelGuids(doc);
        }

        private void ReapplyAlreadyLinkedFlags()
        {
            foreach (var row in _unfilteredFiles)
            {
                var linked = row.File.HasCloudGuids
                    && !string.IsNullOrEmpty(row.File.ModelGuid)
                    && _linkedModelGuids.Contains(row.File.ModelGuid);
                row.IsAlreadyLinked = linked;
                if (linked) row.IsChecked = false;
            }
        }

        // ============================================================
        // Header / toolbar
        // ============================================================

        private void UpdateBreadcrumb()
        {
            var path = _selectedFolder?.FullPath ?? string.Empty;
            FolderBreadcrumb.Text = string.IsNullOrEmpty(path) ? "/" : "/ " + path + " /";
        }

        private void RefreshFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFolder == null) return;
            _selectedFolder.IsLoaded = false;
            RefreshLinkedModelGuids();
            _ = LoadFolderContentsAsync(_selectedFolder);
        }

        // ============================================================
        // Global config
        // ============================================================

        private void InitGlobalConfigOptions()
        {
            GlobalPositioning.Items.Clear();
            foreach (var item in new[]
            {
                new ComboItem(PositioningSystem.AutoOriginToInternalOrigin,    "Auto - Internal Origin"),
                new ComboItem(PositioningSystem.AutoCenterToCenter,            "Auto - Center to Center"),
                new ComboItem(PositioningSystem.AutoBySharedCoordinates,       "Auto - Project Base Point"),
                new ComboItem(PositioningSystem.InternalOriginToInternalOrigin,"Manual - Center"),
                new ComboItem(PositioningSystem.OriginToOrigin,                "Origin to Origin")
            })
            {
                GlobalPositioning.Items.Add(item);
            }
            GlobalPositioning.DisplayMemberPath = "Display";
            GlobalPositioning.SelectedIndex = 0;
        }

        private void GlobalPositioning_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(GlobalPositioning.SelectedItem is ComboItem item)) return;
            if (!(item.Value is PositioningSystem ps)) return;
            foreach (var t in Queue) t.Configuration.Positioning = ps;
        }

        private void GlobalReferenceType_Checked(object sender, RoutedEventArgs e)
        {
            // Pill toggle replaces the old combo; checking propagates to every
            // queue item exactly like the old SelectionChanged did.
            if (Queue == null) return;
            var rt = ReferenceEquals(sender, RefAttachment) ? LinkReferenceType.Attachment : LinkReferenceType.Overlay;
            foreach (var t in Queue) t.Configuration.ReferenceType = rt;
        }

        // F2 — only the button applies the default workset to existing items.
        private void ApplyAllBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = GlobalWorkset.Text ?? string.Empty;
            foreach (var t in Queue) t.Configuration.WorksetName = name;
        }

        private LinkReferenceType CurrentReferenceType()
        {
            return RefAttachment.IsChecked == true ? LinkReferenceType.Attachment : LinkReferenceType.Overlay;
        }

        private PositioningSystem CurrentPositioning()
        {
            return (GlobalPositioning.SelectedItem as ComboItem)?.Value is PositioningSystem p
                ? p : PositioningSystem.AutoOriginToInternalOrigin;
        }

        // ============================================================
        // File selection
        // ============================================================

        private void HeaderCheckAll_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb)) return;
            var target = cb.IsChecked == true;
            foreach (var row in CurrentFiles)
            {
                if (row.IsAlreadyLinked) continue;   // F1: select-all skips linked rows
                row.IsChecked = target;
            }
            UpdateCounts();
        }

        private void RowCheck_Click(object sender, RoutedEventArgs e)
        {
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            var selected = CurrentFiles.Count(r => r.IsChecked);
            SelectedCountText.Text = selected.ToString();
            AddToQueueBtn.IsEnabled = selected > 0;
            QueueCountText.Text = Queue.Count.ToString();
            StartBatchBtn.IsEnabled = Queue.Count > 0 && Shell != null && !Shell.BatchHandler.IsRunning;

            // Header checkbox reflects the selectable (non-linked) rows only.
            var selectable = CurrentFiles.Count(r => !r.IsAlreadyLinked);
            if (selectable == 0 || selected == 0)
                HeaderCheckAll.IsChecked = false;
            else if (selected >= selectable)
                HeaderCheckAll.IsChecked = true;
            else
                HeaderCheckAll.IsChecked = null;
        }

        private void UpdateEmptyState()
        {
            EmptyHint.Visibility = CurrentFiles.Count == 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void AddToQueueBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Shell == null) return;
            EnqueueChecked();
        }

        private void QueueOneBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b)) return;
            if (!(b.Tag is CheckableFileRow row)) return;
            if (row.IsAlreadyLinked) return;   // F1
            EnqueueSingleFile(row.File);
        }

        // ============================================================
        // Queue (F3: items are LinkTask from enqueue time)
        // ============================================================

        private void EnqueueChecked()
        {
            var defaultPos = CurrentPositioning();
            var defaultRef = CurrentReferenceType();
            var defaultWorkset = GlobalWorkset.Text;
            foreach (var row in CurrentFiles.Where(r => r.IsChecked).ToList())
            {
                if (row.IsAlreadyLinked) continue;   // F1
                if (Queue.Any(q => string.Equals(q.Configuration.File.VersionUrn, row.File.VersionUrn, StringComparison.OrdinalIgnoreCase))) continue;
                var cfg = new LinkConfiguration { File = row.File, Positioning = defaultPos, ReferenceType = defaultRef };
                if (!string.IsNullOrEmpty(defaultWorkset)) cfg.WorksetName = defaultWorkset;   // F2
                Queue.Add(new LinkTask(cfg));
                row.IsChecked = false;
            }
            UpdateCounts();
        }

        private void EnqueueSingleFile(AccRvtFile file)
        {
            if (file == null) return;
            if (file.HasCloudGuids && !string.IsNullOrEmpty(file.ModelGuid)
                && _linkedModelGuids.Contains(file.ModelGuid)) return;   // F1
            if (Queue.Any(q => string.Equals(q.Configuration.File.VersionUrn, file.VersionUrn, StringComparison.OrdinalIgnoreCase))) return;
            var cfg = new LinkConfiguration { File = file, Positioning = CurrentPositioning(), ReferenceType = CurrentReferenceType() };
            var defaultWorkset = GlobalWorkset.Text;
            if (!string.IsNullOrEmpty(defaultWorkset)) cfg.WorksetName = defaultWorkset;   // F2
            Queue.Add(new LinkTask(cfg));
            UpdateCounts();
        }

        private void RemoveFromQueueBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            if (!(btn.Tag is LinkTask task)) return;
            Queue.Remove(task);
            UpdateCounts();
        }

        private void DismissErrorBanner_Click(object sender, RoutedEventArgs e)
        {
            ErrorBanner.Visibility = System.Windows.Visibility.Collapsed;
        }

        // ============================================================
        // Overall progress (F3 aggregate)
        // ============================================================

        private void Queue_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (LinkTask t in e.OldItems) t.PropertyChanged -= QueueTask_PropertyChanged;
            if (e.NewItems != null)
                foreach (LinkTask t in e.NewItems) t.PropertyChanged += QueueTask_PropertyChanged;
            UpdateOverallProgress();
        }

        private void QueueTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LinkTask.Status)) return;
            UpdateOverallProgress();
        }

        private void UpdateOverallProgress()
        {
            var total = Queue.Count;
            var terminal = Queue.Count(t => t.IsTerminal);
            OverallProgressLabel.Text = terminal + " / " + total + " Complete";
            OverallBarScale.ScaleX = total == 0 ? 0 : (double)terminal / total;
        }

        // ============================================================
        // Start batch
        // ============================================================

        private void StartBatchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Shell == null || Queue.Count == 0 || Shell.BatchHandler.IsRunning) return;

            // Re-run leftovers (Failed / Cancelled / Skipped) from a clean slate.
            foreach (var task in Queue)
            {
                if (task.Status == LinkTaskStatus.Pending) continue;
                task.Status = LinkTaskStatus.Pending;
                task.StatusMessage = string.Empty;
                task.ProgressPercent = 0;
            }

            var tasks = Queue.ToList();
            var dlg = new ProgressDialog(Shell.BatchHandler, tasks) { Owner = Shell };

            // Show NON-MODALLY. The handler is an IExternalEventHandler whose Execute must
            // run on Revit's UI thread; ShowDialog() would block that thread, the
            // ExternalEvent would never fire, and the batch would hang (mirrors WSPICT,
            // which raises the event and returns without a modal dialog). Post-run cleanup
            // must therefore happen when the batch completes, not after a modal returns.
            dlg.BatchFinished += () =>
            {
                for (int i = Queue.Count - 1; i >= 0; i--)
                {
                    if (Queue[i].Status == LinkTaskStatus.Succeeded) Queue.RemoveAt(i);
                }
                // F1: the run just added links — refresh the dim/disable flags.
                RefreshLinkedModelGuids();
                ReapplyAlreadyLinkedFlags();
                UpdateCounts();
            };

            dlg.Show();
            Shell.BatchHandler.Start(tasks);
        }
    }

    internal class ComboItem
    {
        public object Value { get; }
        public string Display { get; }
        public ComboItem(object value, string display) { Value = value; Display = display; }
        public override string ToString() => Display;
    }

    public class PercentToScaleConverter : IValueConverter
    {
        public static readonly PercentToScaleConverter Instance = new PercentToScaleConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value is double d ? d : 0;
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            return pct / 100.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new BoolToVisibilityConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.Data.Binding.DoNothing;
    }

    // Shared bool-inverting converter for BatchView's already-linked checkbox
    // (IsEnabled = !IsAlreadyLinked).
    public class BoolInverter : IValueConverter
    {
        public static readonly BoolInverter Instance = new BoolInverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : (object)true;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : (object)true;
    }
}
