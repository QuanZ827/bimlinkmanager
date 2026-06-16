using System;
using System.Collections.Generic;
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
        /// Normalized names (trimmed, trailing ".rvt" stripped, case-insensitive) of every
        /// RevitLinkType already in the document. Name-based BY DESIGN: extracting a cloud
        /// ModelGUID from an UNLOADED link is unreliable across Revit versions and usually returns
        /// nothing, so the old GUID approach silently matched nothing and no rows were dimmed.
        /// Element-name matching is the dependable signal (mirrors WSPICT).
        /// </summary>
        public static HashSet<string> GetLinkedModelNames(Document doc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc == null) return names;

            try
            {
                var coll = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType));
                foreach (Element el in coll)
                {
                    var n = NormalizeModelName(el?.Name);
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
                }
            }
            catch (Exception ex)
            {
                Log.Error("LinkListService.GetLinkedModelNames failed", ex);
            }

            return names;
        }

        /// <summary>
        /// Trim and strip a trailing ".rvt" (case-insensitive) so an ACC file name and a
        /// RevitLinkType element name compare equal (mirrors WSPICT's NormalizeModelName).
        /// </summary>
        public static string NormalizeModelName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var n = name.Trim();
            if (n.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 4).Trim();
            return n;
        }
    }
}
