using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;

namespace BimLinkManager.Execution
{
    /// <summary>
    /// Single-shot external event handler that processes a queue of LinkTasks
    /// on Revit's UI thread. UI binds directly to each task (INotifyPropertyChanged)
    /// for live progress.
    /// </summary>
    public class BatchLinkHandler : IExternalEventHandler
    {
        private readonly ExternalEvent _event;
        private readonly object _gate = new object();
        private List<LinkTask> _queue;
        private int _cancelFlag;

        public event Action BatchStarted;
        public event Action<LinkTask> TaskStarted;
        public event Action<LinkTask> TaskCompleted;
        public event Action BatchCompleted;

        public BatchLinkHandler()
        {
            _event = ExternalEvent.Create(this);
        }

        public bool IsRunning { get; private set; }

        public void Start(IEnumerable<LinkTask> tasks)
        {
            if (tasks == null) return;
            lock (_gate)
            {
                _queue = new List<LinkTask>(tasks);
                Interlocked.Exchange(ref _cancelFlag, 0);
            }
            _event.Raise();
        }

        public void Cancel()
        {
            Interlocked.Exchange(ref _cancelFlag, 1);
        }

        public string GetName()
        {
            return AppConstants.ProductName + ".BatchLinkHandler";
        }

        public void Execute(UIApplication app)
        {
            List<LinkTask> queue;
            lock (_gate)
            {
                queue = _queue;
                _queue = null;
            }
            if (queue == null || queue.Count == 0) return;

            IsRunning = true;
            try
            {
                BatchStarted?.Invoke();

                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null || uiDoc.Document == null)
                {
                    foreach (var t in queue)
                    {
                        t.Status = LinkTaskStatus.Failed;
                        t.StatusMessage = "No active Revit document.";
                    }
                    return;
                }

                var doc = uiDoc.Document;

                // Region MUST come from the host document's own cloud path — the APS API region
                // string is not guaranteed to match what ModelPathUtils expects, which produces an
                // unresolvable cloud path ("path not found"). See BUGFIX_PATH_NOT_FOUND.md.
                string hostRegion = TryGetHostCloudRegion(doc);

                var isWorkshared = doc.IsWorkshared;

                WorksetId originalActive = null;
                WorksetTable worksetTable = null;
                if (isWorkshared)
                {
                    worksetTable = doc.GetWorksetTable();
                    originalActive = worksetTable.GetActiveWorksetId();
                }

                try
                {
                    // Region comes ONLY from the host document's cloud path (mirrors WSPICT). If it
                    // is unavailable, hard-stop the whole batch — never fall back to the APS API
                    // region, which is incompatible with ConvertCloudGUIDsToCloudPath. See
                    // FIX_REGION_COPY_WSPICT.md.
                    if (string.IsNullOrEmpty(hostRegion))
                    {
                        foreach (var t in queue)
                        {
                            t.Status = LinkTaskStatus.Failed;
                            t.StatusMessage = "Host document has no cloud region. Open this from an ACC workshared/cloud model.";
                            TaskCompleted?.Invoke(t);
                        }
                        return; // do NOT continue into the loop
                    }

                    int i = 0;
                    foreach (var task in queue)
                    {
                        i++;
                        if (Volatile.Read(ref _cancelFlag) != 0)
                        {
                            task.Status = LinkTaskStatus.Cancelled;
                            task.StatusMessage = "Cancelled before start.";
                            TaskCompleted?.Invoke(task);
                            continue;
                        }

                        TaskStarted?.Invoke(task);
                        ProcessTask(doc, task, worksetTable, isWorkshared, hostRegion);
                        TaskCompleted?.Invoke(task);
                    }
                }
                finally
                {
                    if (isWorkshared && worksetTable != null && originalActive != null)
                    {
                        try { worksetTable.SetActiveWorksetId(originalActive); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("BatchLinkHandler.Execute failed", ex);
            }
            finally
            {
                IsRunning = false;
                BatchCompleted?.Invoke();
            }
        }

        private static string TryGetHostCloudRegion(Document doc)
        {
            try
            {
                if (doc != null && doc.IsModelInCloud)
                {
                    var cp = doc.GetCloudModelPath();
                    if (cp != null)
                    {
                        var rp = cp.GetType().GetProperty("Region");
                        if (rp != null) return rp.GetValue(cp) as string;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("TryGetHostCloudRegion failed: " + ex.Message);
            }
            return null;
        }

        private void ProcessTask(Document doc, LinkTask task, WorksetTable worksetTable, bool isWorkshared, string hostRegion)
        {
            var cfg = task.Configuration;
            var file = cfg?.File;
            if (file == null)
            {
                task.Status = LinkTaskStatus.Failed;
                task.StatusMessage = "Missing file reference.";
                return;
            }

            if (!file.HasCloudGuids)
            {
                task.Status = LinkTaskStatus.Skipped;
                task.StatusMessage = "Skipped: cloud GUIDs not available.";
                return;
            }

            if (!Guid.TryParse(file.ProjectGuid, out var projectGuid) || !Guid.TryParse(file.ModelGuid, out var modelGuid))
            {
                task.Status = LinkTaskStatus.Failed;
                task.StatusMessage = "Invalid project or model GUID.";
                return;
            }

            var region = hostRegion; // single source of truth, mirrors WSPICT. Guard in Execute guarantees non-empty.

            ModelPath cloudPath;
            try
            {
                cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(region, projectGuid, modelGuid);
            }
            catch (Exception ex)
            {
                task.Status = LinkTaskStatus.Failed;
                task.StatusMessage = "Could not build cloud path (region='" + region + "'): " + ex.Message;
                Log.Error("ConvertCloudGUIDsToCloudPath failed for " + file.Name, ex);
                return;
            }

            // Set target workset BEFORE creating link
            if (isWorkshared && worksetTable != null && !string.IsNullOrEmpty(cfg.WorksetName))
            {
                var target = FindUserWorkset(doc, cfg.WorksetName);
                if (target != null)
                {
                    try { worksetTable.SetActiveWorksetId(target.Id); }
                    catch (Exception ex) { Log.Warn("SetActiveWorksetId failed: " + ex.Message); }
                }
                else
                {
                    Log.Info("Workset '" + cfg.WorksetName + "' not found; using current active workset.");
                }
            }

            task.Status = LinkTaskStatus.Running;
            task.ProgressPercent = 5;

            using (var tx = new Transaction(doc, "BimLinkManager: Link " + Truncate(file.DisplayName ?? file.Name, 80)))
            {
                try
                {
                    if (tx.Start() != TransactionStatus.Started)
                    {
                        task.Status = LinkTaskStatus.Failed;
                        task.StatusMessage = "Could not start transaction.";
                        return;
                    }

                    var options = new RevitLinkOptions(false);
                    task.ProgressPercent = 30;

                    var result = RevitLinkType.Create(doc, cloudPath, options);
                    if (result == null || result.ElementId == ElementId.InvalidElementId)
                    {
                        task.Status = LinkTaskStatus.Failed;
                        task.StatusMessage = "RevitLinkType.Create returned no element. Load result: "
                            + (result?.LoadResult.ToString() ?? "null");
                        tx.RollBack();
                        return;
                    }

                    if (result.LoadResult != LinkLoadResultType.LinkLoaded)
                    {
                        task.Status = LinkTaskStatus.Failed;
                        task.StatusMessage = "Link load failed: " + result.LoadResult;
                        tx.RollBack();
                        return;
                    }

                    task.ProgressPercent = 70;

                    var linkType = doc.GetElement(result.ElementId) as RevitLinkType;
                    if (linkType != null)
                    {
                        try
                        {
                            linkType.AttachmentType = cfg.ReferenceType == LinkReferenceType.Attachment
                                ? AttachmentType.Attachment
                                : AttachmentType.Overlay;
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("Setting AttachmentType failed: " + ex.Message);
                        }
                    }

                    var placement = MapPlacement(cfg.Positioning);
                    var instance = RevitLinkInstance.Create(doc, result.ElementId, placement);
                    if (instance == null)
                    {
                        task.Status = LinkTaskStatus.Failed;
                        task.StatusMessage = "RevitLinkInstance.Create returned null.";
                        tx.RollBack();
                        return;
                    }

                    task.ProgressPercent = 100;
                    if (tx.Commit() != TransactionStatus.Committed)
                    {
                        task.Status = LinkTaskStatus.Failed;
                        task.StatusMessage = "Transaction commit failed.";
                        return;
                    }

                    task.Status = LinkTaskStatus.Succeeded;
                    task.StatusMessage = "Linked.";
                }
                catch (OperationCanceledException)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    task.Status = LinkTaskStatus.Cancelled;
                    task.StatusMessage = "Cancelled.";
                }
                catch (Exception ex)
                {
                    if (tx.HasStarted()) tx.RollBack();
                    task.Status = LinkTaskStatus.Failed;
                    task.StatusMessage = ex.Message;
                    Log.Error("Linking failed for " + file.Name, ex);
                }
            }
        }

        private static Workset FindUserWorkset(Document doc, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var coll = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset);
            foreach (var ws in coll)
            {
                if (string.Equals(ws.Name, name, StringComparison.OrdinalIgnoreCase)) return ws;
            }
            return null;
        }

        private static ImportPlacement MapPlacement(PositioningSystem positioning)
        {
            switch (positioning)
            {
                case PositioningSystem.AutoCenterToCenter:           return ImportPlacement.Centered;
                case PositioningSystem.AutoOriginToInternalOrigin:   return ImportPlacement.Origin;
                case PositioningSystem.AutoByLocation:               return ImportPlacement.Site;
                case PositioningSystem.AutoBySharedCoordinates:      return ImportPlacement.Shared;
                case PositioningSystem.InternalOriginToInternalOrigin: return ImportPlacement.Origin;
                case PositioningSystem.OriginToOrigin:               return ImportPlacement.Origin;
                default:                                             return ImportPlacement.Origin;
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "...";
        }
    }
}
