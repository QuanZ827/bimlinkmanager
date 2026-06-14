using System;

namespace BimLinkManager.Models
{
    public class AccRvtFile
    {
        public string ItemId { get; set; }
        public string VersionUrn { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public long SizeBytes { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public string ModifiedBy { get; set; }
        public int VersionNumber { get; set; }

        public string ProjectGuid { get; set; }
        public string ModelGuid { get; set; }
        public string Region { get; set; }
        public string ProjectId { get; set; }

        public string FolderId { get; set; }
        public AccFolder Folder { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (SizeBytes <= 0) return "-";
                double size = SizeBytes;
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int unit = 0;
                while (size >= 1024 && unit < units.Length - 1)
                {
                    size /= 1024;
                    unit++;
                }
                return size.ToString("0.#") + " " + units[unit];
            }
        }

        public string ModifiedDisplay
        {
            get
            {
                if (ModifiedUtc == DateTime.MinValue) return "-";
                var local = ModifiedUtc.ToLocalTime();
                return local.ToString("yyyy-MM-dd HH:mm");
            }
        }

        // F5: compact relative timestamp for the MODIFIED column
        // (ModifiedDisplay stays available as the row tooltip).
        public string ModifiedRelativeDisplay
        {
            get
            {
                if (ModifiedUtc == DateTime.MinValue) return "-";
                var delta = DateTime.UtcNow - ModifiedUtc;
                if (delta < TimeSpan.Zero) delta = TimeSpan.Zero;
                if (delta.TotalHours < 1) return (int)delta.TotalMinutes + "m ago";
                if (delta.TotalHours < 24) return (int)delta.TotalHours + "h ago";
                if (delta.TotalDays < 7) return (int)delta.TotalDays + "d ago";
                return ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd");
            }
        }

        // F5: "v{n}" for the VER column
        public string VersionDisplay
        {
            get { return VersionNumber > 0 ? "v" + VersionNumber : "-"; }
        }

        public bool HasCloudGuids
        {
            get { return !string.IsNullOrEmpty(ProjectGuid) && !string.IsNullOrEmpty(ModelGuid); }
        }

        public override string ToString()
        {
            return DisplayName ?? Name ?? ItemId ?? "(file)";
        }
    }
}
