using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RevitUI = Autodesk.Revit.UI;
using BimLinkManager.Execution;
using BimLinkManager.Models;
using BimLinkManager.Services;

namespace BimLinkManager.Views
{
    public partial class LinkListView : UserControl
    {
        public ObservableCollection<RevitLinkRow> Rows { get; } = new ObservableCollection<RevitLinkRow>();

        private readonly LinkOpsHandler _ops;
        private MainWindow Shell => Window.GetWindow(this) as MainWindow;

        public LinkListView()
        {
            InitializeComponent();
            LinkRows.ItemsSource = Rows;

            _ops = new LinkOpsHandler();
            _ops.Progress     += msg => Dispatcher.BeginInvoke(new Action(() => Shell?.SetStatus(msg)));
            _ops.RowChanged   += _   => Dispatcher.BeginInvoke(new Action(UpdateEmptyState));
            _ops.OpsCompleted += ()  => Dispatcher.BeginInvoke(new Action(Refresh));
        }

        public void OnShellReady()
        {
            // No-op for now; could prefetch on first show
        }

        public void Refresh()
        {
            var doc = Shell?.UiApp?.ActiveUIDocument?.Document;
            Rows.Clear();
            if (doc == null) { UpdateEmptyState(); return; }
            foreach (var row in LinkListService.Enumerate(doc))
            {
                Rows.Add(row);
            }
            UpdateEmptyState();
        }

        public void ApplyFileFilter(string text)
        {
            // For now, search in header filters BatchView only.
            // Could extend to LinkList in a future iteration.
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LinkCountText.Text = Rows.Count.ToString();
        }

        private void ReloadAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Rows.Count == 0) return;
            Shell?.SetStatus("RELOADING " + Rows.Count + " LINK(S)…");
            _ops.RequestReloadAll(Rows);
        }

        private void ManageLinksBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cmdId = RevitUI.RevitCommandId.LookupPostableCommandId(RevitUI.PostableCommand.ManageLinks);
                Shell?.UiApp.PostCommand(cmdId);
                Shell?.SetStatus("MANAGE LINKS DIALOG WILL OPEN AFTER BimLinkManager CLOSES");

                // Posted commands only execute after modal dialogs close.
                // Close the BimLinkManager window so the user can see the native dialog.
                Shell?.Close();
            }
            catch (Exception ex)
            {
                Shell?.SetStatusError("Manage Links failed: " + ex.Message);
            }
        }

        private void WorksetCombo_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is ComboBox cb)) return;
            if (!(cb.Tag is RevitLinkRow row)) return;
            var newName = cb.Text;
            if (string.IsNullOrWhiteSpace(newName)) return;
            if (string.Equals(newName, row.WorksetName, StringComparison.Ordinal)) return;
            _ops.RequestSetWorkset(row, newName);
        }
    }

    public class BoolInverter : IValueConverter
    {
        public static readonly BoolInverter Instance = new BoolInverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : (object)true;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b) ? !b : (object)true;
    }
}
