using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BimLinkManager.Diagnostics;

namespace BimLinkManager.Services
{
    /// <summary>
    /// Read-only helper for the Batch Link view: reports which cloud models are
    /// already linked into the active document so their rows can be dimmed (F1).
    /// </summary>
    public static class LinkListService
    {
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
                        // Per-element tolerance: skip links whose path can't be read.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("LinkListService.GetLinkedCloudModelGuids failed", ex);
            }

            return guids;
        }
    }
}
