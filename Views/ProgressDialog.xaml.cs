using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using BimLinkManager.Execution;
using BimLinkManager.Models;

namespace BimLinkManager.Views
{
    public enum LogKind { Info, Ok, Err }

    public class LogLine
    {
        public string Text { get; set; }
        public LogKind Kind { get; set; }
    }

    public partial class ProgressDialog : Window
    {
        private readonly BatchLinkHandler _handler;
        private readonly List<LinkTask> _tasks;
        private readonly ObservableCollection<LogLine> _logLines = new ObservableCollection<LogLine>();
        private int _completedCount;
        private bool _allDone;
        private LinkTask _currentTask;

        /// <summary>Raised on the UI thread once the batch finishes, so the owner can do
        /// post-run cleanup (this dialog is shown non-modally, so the caller can't do it
        /// inline after Show()).</summary>
        public event Action BatchFinished;

        public ProgressDialog(BatchLinkHandler handler, List<LinkTask> tasks)
        {
            InitializeComponent();
            _handler = handler;
            _tasks = tasks;

            LogList.ItemsSource = _logLines;
            UpdateSummary();

            _handler.TaskStarted    += OnTaskStarted;
            _handler.TaskCompleted  += OnTaskCompleted;
            _handler.BatchCompleted += OnBatchCompleted;

            Closed += (s, e) =>
            {
                _handler.TaskStarted    -= OnTaskStarted;
                _handler.TaskCompleted  -= OnTaskCompleted;
                _handler.BatchCompleted -= OnBatchCompleted;
            };

            AppendLog("> initialising revit bridge…", LogKind.Info);
        }

        private void OnTaskStarted(LinkTask task)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _currentTask = task;
                task.PropertyChanged += Task_PropertyChanged;
                CurrentFileLabel.Text = task.DisplayName;
                CurrentFilePercent.Text = "(0%)";
                PhaseLabel.Text = "LINKING MODEL " + (_completedCount + 1) + " / " + _tasks.Count;
                AppendLog("> linking " + task.DisplayName, LogKind.Info);
            }));
        }

        private void Task_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LinkTask.ProgressPercent)) return;
            if (!(sender is LinkTask t)) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentTask == t)
                {
                    CurrentFilePercent.Text = "(" + (int)t.ProgressPercent + "%)";
                }
            }));
        }

        private void OnTaskCompleted(LinkTask task)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (task != null) task.PropertyChanged -= Task_PropertyChanged;
                _completedCount++;
                var glyph = task.Status == LinkTaskStatus.Succeeded ? "OK"
                          : task.Status == LinkTaskStatus.Failed    ? "ERR"
                          : task.Status == LinkTaskStatus.Cancelled ? "..."
                          : "—";
                var kind = task.Status == LinkTaskStatus.Succeeded ? LogKind.Ok
                         : task.Status == LinkTaskStatus.Failed    ? LogKind.Err
                         : LogKind.Info;
                var msg = "> [" + glyph + "] " + task.DisplayName
                    + (string.IsNullOrEmpty(task.StatusMessage) ? "" : "  — " + task.StatusMessage);
                AppendLog(msg, kind);
                UpdateProgressBar();
                UpdateSummary();
            }));
        }

        private void OnBatchCompleted()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _allDone = true;
                _currentTask = null;
                CurrentFileLabel.Text = "(done)";
                CurrentFilePercent.Text = "";
                PhaseLabel.Text = "COMPLETE";
                ActionBtn.Content = "Close";
                ActionBtn.IsEnabled = true;
                ActionBtn.Style = (Style)FindResource("BtnPrimary");
                AppendLog("", LogKind.Info);
                AppendLog("> batch complete", LogKind.Info);
                UpdateSummary();
                ProgressFill.Width = ProgressTrack.ActualWidth;
                OverallPercent.Text = "100%";
                BatchFinished?.Invoke();
            }));
        }

        private void UpdateProgressBar()
        {
            var pct = _tasks.Count == 0 ? 1.0 : (double)_completedCount / _tasks.Count;
            ProgressFill.Width = Math.Max(0, pct * ProgressTrack.ActualWidth);
            OverallPercent.Text = ((int)(pct * 100)) + "%";
        }

        private void UpdateSummary()
        {
            var ok       = _tasks.Count(t => t.Status == LinkTaskStatus.Succeeded);
            var pending  = _tasks.Count(t => !t.IsTerminal);
            var terminal = _tasks.Count(t => t.IsTerminal);
            SucceededLabel.Text = ok + " SUCCEEDED";
            PendingLabel.Text = pending + " PENDING";
            TaskCountLabel.Text = terminal + " / " + _tasks.Count + " Complete";
        }

        private void AppendLog(string line, LogKind kind)
        {
            _logLines.Add(new LogLine { Text = line, Kind = kind });
            LogScroll.UpdateLayout();
            LogScroll.ScrollToEnd();
        }

        private void ActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_allDone)
            {
                Close();
                return;
            }
            _handler.Cancel();
            ActionBtn.IsEnabled = false;
            ActionBtn.Content = "Cancelling…";
            AppendLog("> (cancelling — current task will finish first)", LogKind.Info);
        }
    }
}
