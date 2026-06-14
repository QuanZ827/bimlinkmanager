using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;

namespace BimLinkManager.Execution
{
    /// <summary>
    /// External event handler for Link List operations: reload, set workset.
    /// Manage Links uses PostCommand and does not go through this handler.
    /// </summary>
    public class LinkOpsHandler : IExternalEventHandler
    {
        public enum OpType { ReloadAll, ReloadOne, SetWorkset }

        private class Op
        {
            public OpType Type;
            public List<RevitLinkRow> Rows = new List<RevitLinkRow>();
            public string TargetWorksetName;
        }

        private readonly ExternalEvent _event;
        private readonly Queue<Op> _queue = new Queue<Op>();
        private readonly object _gate = new object();

        public event Action<string> Progress;  // status text
        public event Action OpsCompleted;
        public event Action<RevitLinkRow> RowChanged;

        public LinkOpsHandler()
        {
            _event = ExternalEvent.Create(this);
        }

        public string GetName() => "BimLinkManager.LinkOpsHandler";

        public void RequestReloadAll(IEnumerable<RevitLinkRow> rows)
        {
            if (rows == null) return;
            lock (_gate)
            {
                _queue.Enqueue(new Op { Type = OpType.ReloadAll, Rows = rows.ToList() });
            }
            _event.Raise();
        }

        public void RequestReload(RevitLinkRow row)
        {
            if (row == null) return;
            lock (_gate)
            {
                _queue.Enqueue(new Op { Type = OpType.ReloadOne, Rows = new List<RevitLinkRow> { row } });
            }
            _event.Raise();
        }

        public void RequestSetWorkset(RevitLinkRow row, string worksetName)
        {
            if (row == null) return;
            lock (_gate)
            {
                _queue.Enqueue(new Op { Type = OpType.SetWorkset, Rows = new List<RevitLinkRow> { row }, TargetWorksetName = worksetName });
            }
            _event.Raise();
        }

        public void Execute(UIApplication app)
        {
            Op op;
            lock (_gate)
            {
                if (_queue.Count == 0) return;
                op = _queue.Dequeue();
            }

            var doc = app.ActiveUIDocument?.Document;
            if (doc == null)
            {
                Progress?.Invoke("No active document");
                OpsCompleted?.Invoke();
                return;
            }

            try
            {
                switch (op.Type)
                {
                    case OpType.ReloadAll: DoReload(doc, op.Rows); break;
                    case OpType.ReloadOne: DoReload(doc, op.Rows); break;
                    case OpType.SetWorkset: DoSetWorkset(doc, op.Rows[0], op.TargetWorksetName); break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("LinkOpsHandler.Execute failed", ex);
                Progress?.Invoke("Error: " + ex.Message);
            }
            finally
            {
                OpsCompleted?.Invoke();
            }
        }

        private void DoReload(Document doc, List<RevitLinkRow> rows)
        {
            int ok = 0, fail = 0;
            foreach (var row in rows)
            {
                row.IsBusy = true;
                try
                {
                    var lt = doc.GetElement(row.LinkTypeId) as RevitLinkType;
                    if (lt == null) { fail++; continue; }
                    var result = lt.Reload();
                    if (result?.LoadResult == LinkLoadResultType.LinkLoaded)
                    {
                        row.Status = RowLinkStatus.Loaded;
                        ok++;
                    }
                    else
                    {
                        fail++;
                        Log.Warn("Reload result for " + row.Name + ": " + (result?.LoadResult.ToString() ?? "null"));
                    }
                }
                catch (Exception ex)
                {
                    fail++;
                    Log.Error("Reload failed: " + row.Name, ex);
                }
                finally
                {
                    row.IsBusy = false;
                    RowChanged?.Invoke(row);
                }
            }
            Progress?.Invoke("Reload complete · " + ok + " ok, " + fail + " failed");
        }

        private void DoSetWorkset(Document doc, RevitLinkRow row, string worksetName)
        {
            if (!doc.IsWorkshared)
            {
                Progress?.Invoke("Project is not workshared; cannot assign workset.");
                return;
            }

            row.IsBusy = true;
            try
            {
                var target = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                    .FirstOrDefault(w => string.Equals(w.Name, worksetName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    Progress?.Invoke("Workset '" + worksetName + "' not found.");
                    return;
                }

                using (var tx = new Transaction(doc, "BimLinkManager: Set link workset"))
                {
                    if (tx.Start() != TransactionStatus.Started) return;
                    var lt = doc.GetElement(row.LinkTypeId) as RevitLinkType;
                    if (lt == null) { tx.RollBack(); return; }
                    var p = lt.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                    if (p != null && !p.IsReadOnly) p.Set(target.Id.IntegerValue);

                    // Also update the instances of this link type
                    var instances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>()
                        .Where(i => i.GetTypeId() == row.LinkTypeId);
                    foreach (var inst in instances)
                    {
                        var ip = inst.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (ip != null && !ip.IsReadOnly) ip.Set(target.Id.IntegerValue);
                    }

                    tx.Commit();
                }

                row.WorksetName = target.Name;
                Progress?.Invoke("Workset updated for " + row.Name);
            }
            catch (Exception ex)
            {
                Log.Error("SetWorkset failed: " + row.Name, ex);
                Progress?.Invoke("Error: " + ex.Message);
            }
            finally
            {
                row.IsBusy = false;
                RowChanged?.Invoke(row);
            }
        }
    }
}
