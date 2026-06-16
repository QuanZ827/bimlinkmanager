using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimLinkManager.Diagnostics;
using BimLinkManager.Execution;
using BimLinkManager.Models;
using BimLinkManager.Services;

namespace BimLinkManager.Views
{
    public partial class MainWindow : Window
    {
        public UIApplication UiApp { get; }
        public TokenService Tokens { get; }
        public AccDataService Acc { get; }
        public BatchLinkHandler BatchHandler => App.BatchHandler;

        public AccHub CurrentHub { get; private set; }
        public AccProject CurrentProject { get; private set; }
        public ObservableCollection<string> WorksetNames { get; } = new ObservableCollection<string>();

        // Bound by the brand header; the string itself lives in AppConstants only.
        public string BrandSubtitle
        {
            get { return AppConstants.BrandSubtitle; }
        }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public MainWindow(UIApplication uiApp)
        {
            InitializeComponent();
            DataContext = this;
            UiApp = uiApp;
            Tokens = new TokenService();
            Acc = new AccDataService(Tokens);

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            StateChanged += MainWindow_StateChanged;

            SearchBox.GotFocus += (s, e) => SearchPlaceholder.Visibility = System.Windows.Visibility.Collapsed;
            SearchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(SearchBox.Text))
                    SearchPlaceholder.Visibility = System.Windows.Visibility.Visible;
            };
        }

        // ============================================================
        // Window chrome (custom title bar)
        // ============================================================

        private void ChromeDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            try { DragMove(); } catch { }
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            try
            {
                if (MaxBtnIcon == null) return;
                var key = WindowState == WindowState.Maximized ? "IconWinRestore" : "IconWinMaximize";
                MaxBtnIcon.Data = (Geometry)FindResource(key);
            }
            catch { }

            // Add padding when maximized so content doesn't disappear behind the screen edge
            if (WindowState == WindowState.Maximized)
            {
                BorderThickness = new Thickness(8);
            }
            else
            {
                BorderThickness = new Thickness(0);
            }
        }

        // ============================================================
        // Public API for child views
        // ============================================================

        public void SetStatus(string text) { Log.Info("[status] " + text); }
        public void SetStatusError(string text) { Log.Warn("[status] " + text); }

        public void SetProjectLabel(string text, bool isError)
        {
            BatchPane?.SetProjectContext(text, isError);
            if (isError)
            {
                ShowErrorToast("Heads up", text);
            }
            else
            {
                HideErrorToast();
            }
        }

        public void ShowErrorToast(string title, string message)
        {
            ErrorToastTitle.Text = title ?? "Heads up";
            ErrorToastMessage.Text = message ?? string.Empty;
            ErrorToast.Visibility = System.Windows.Visibility.Visible;
        }

        public void HideErrorToast()
        {
            ErrorToast.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void DismissErrorToast_Click(object sender, RoutedEventArgs e)
        {
            HideErrorToast();
        }

        // ============================================================
        // Lifecycle
        // ============================================================

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWorksetsFromDocument();
            BatchPane.OnShellReady();
            await AutoDetectAndLoadAsync();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            try { _cts.Cancel(); } catch { }
            try { Acc.Dispose(); } catch { }
        }

        private void LoadWorksetsFromDocument()
        {
            WorksetNames.Clear();
            try
            {
                var doc = UiApp.ActiveUIDocument?.Document;
                if (doc == null || !doc.IsWorkshared) return;
                var coll = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset);
                foreach (var ws in coll.OrderBy(w => w.Name))
                {
                    WorksetNames.Add(ws.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("LoadWorksetsFromDocument failed: " + ex.Message);
            }
        }

        public async Task AutoDetectAndLoadAsync()
        {
            try
            {
                var doc = UiApp.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    SetProjectLabel("(no active document)", true);
                    return;
                }

                var pathName = doc.PathName ?? string.Empty;
                var (_, projectName) = AccDataService.ParseCloudPathName(pathName);

                if (string.IsNullOrEmpty(projectName))
                {
                    SetProjectLabel("Not a cloud project", true);
                    return;
                }

                List<AccHub> hubs;
                try { hubs = await Acc.GetHubsAsync(_cts.Token); }
                catch (Exception ex)
                {
                    SetProjectLabel("ACC error: " + ex.Message, true);
                    return;
                }

                if (hubs.Count == 0)
                {
                    SetProjectLabel("No ACC hubs", true);
                    return;
                }

                // Match the host project by name ACROSS ALL HUBS. Revit's C4R ProjectGUID is
                // not the APS project id, so name-matching is the canonical resolution
                // (mirrors WSPICT). NEVER fall back to a first hub — a wrong project yields
                // wrong GUIDs and "path not found" in Create. Hard-fail if not found.
                CurrentProject = await Acc.FindProjectByNameAcrossHubsAsync(projectName, _cts.Token);
                if (CurrentProject == null)
                {
                    SetProjectLabel("Project '" + projectName + "' not found in any ACC hub", true);
                    return;
                }
                // Keep CurrentHub consistent with the matched project (for display / region).
                CurrentHub = hubs.FirstOrDefault(h => string.Equals(h.Id, CurrentProject.HubId, StringComparison.OrdinalIgnoreCase));
                SetProjectLabel((CurrentHub?.Name ?? "ACC") + " / " + CurrentProject.Name, false);
                await BatchPane.LoadTopFoldersAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Log.Error("AutoDetectAndLoadAsync failed", ex);
                SetProjectLabel("Detection failed: " + ex.Message, true);
            }
        }

        // ============================================================
        // Header actions
        // ============================================================

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            BatchPane.ClearTree();
            await AutoDetectAndLoadAsync();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            BatchPane.ApplyFileFilter(SearchBox.Text);
        }
    }
}
