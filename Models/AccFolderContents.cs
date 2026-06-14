using System.Collections.Generic;

namespace BimLinkManager.Models
{
    public class AccFolderContents
    {
        public List<AccFolder> Subfolders { get; set; } = new List<AccFolder>();
        public List<AccRvtFile> Files { get; set; } = new List<AccRvtFile>();
    }
}
