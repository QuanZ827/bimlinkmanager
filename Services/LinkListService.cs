using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;

namespace BimLinkManager.Services
{
    /// <summary>
    /// Enumerates RevitLinkType elements in the current document and
    /// projects them into RevitLinkRow data for the Link List view.
    /// All write operations go through LinkOpsHandler via ExternalEvent.
    /// </summary>
    public static class LinkListService
    {
        public static List<RevitLinkRow> Enumerate(Document doc)
        {
            var rows = new List<RevitLinkRow>();
            if (doc == null) return rows;

            try
            {
                var coll = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();

                foreach (var lt in coll)
                {
                    if (lt == null) continue;

                    var row = new RevitLinkRow
                    {
                        LinkTypeId = lt.Id,
                        Name = TryGetName(lt),
                        Status = TryGetStatus(doc, lt),
                        PathType = TryGetPathType(lt),
                        WorksetName = TryGetWorksetName(doc, lt)
                    };
                    rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                Log.Error("LinkListService.Enumerate failed", ex);
            }

            return rows.OrderBy(r => r.Name).ToList();
        }

        /// <summary>
        /// Model GUIDs ("D" format, case-insensitive) of every cloud model already
        /// linked into the document. Used by BatchView to dim/disable rows (F1).
        /// </summary>
        public static HashSet<string> GetLinkedCloudModelGuids(Document doc)
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc == null) return guids;

            try
            {
                var coll = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();

                foreach (var lt in coll)
                {
                    if (lt == null) continue;
                    try
                    {
                        var extRef = lt.GetExternalFileReference();
                        if (extRef == null) continue;
                        var modelPath = extRef.GetAbsolutePath();
                        if (modelPath == null || !modelPath.CloudPath) continue;
                        guids.Add(modelPath.GetModelGUID().ToString("D"));
                    }
                    catch
                    {
                        // Same per-element tolerance as the other helpers below.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("LinkListService.GetLinkedCloudModelGuids failed", ex);
            }

            return guids;
        }

        private static string TryGetName(RevitLinkType lt)
        {
            try
            {
                var name = lt.Name;
                if (string.IsNullOrEmpty(name)) return "(unnamed link)";
                return name;
            }
            catch { return "(unnamed link)"; }
        }

        private static RowLinkStatus TryGetStatus(Document doc, RevitLinkType lt)
        {
            try
            {
                switch (lt.GetLinkedFileStatus())
                {
                    case LinkedFileStatus.Loaded:   return RowLinkStatus.Loaded;
                    case LinkedFileStatus.NotFound: return RowLinkStatus.NotFound;
                    default:                        return RowLinkStatus.Unloaded;
                }
            }
            catch
            {
                return TryIsLoaded(doc, lt) ? RowLinkStatus.Loaded : RowLinkStatus.Unloaded;
            }
        }

        private static bool TryIsLoaded(Document doc, RevitLinkType lt)
        {
            try
            {
                return RevitLinkType.IsLoaded(doc, lt.Id);
            }
            catch { return false; }
        }

        private static string TryGetPathType(RevitLinkType lt)
        {
            try
            {
                var extRef = lt.GetExternalFileReference();
                if (extRef == null) return "Local";
                var modelPath = extRef.GetAbsolutePath();
                if (modelPath == null) return "Local";

                if (modelPath.CloudPath) return "Cloud (ACC)";

                var pathType = extRef.PathType;
                switch (pathType)
                {
                    case PathType.Relative:        return "Relative";
                    case PathType.Absolute:        return "Absolute";
                    case PathType.Server:          return "BIM 360 (legacy)";
                    default:                        return pathType.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("PathType detection failed: " + ex.Message);
                return "Unknown";
            }
        }

        private static string TryGetWorksetName(Document doc, RevitLinkType lt)
        {
            try
            {
                if (!doc.IsWorkshared) return string.Empty;
                var worksetParam = lt.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (worksetParam == null) return string.Empty;
                var worksetId = worksetParam.AsInteger();
                var ws = doc.GetWorksetTable().GetWorkset(new WorksetId(worksetId));
                return ws?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
