namespace BimLinkManager.Models
{
    public class AccProject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string HubId { get; set; }
        public string Region { get; set; }

        public override string ToString()
        {
            return Name ?? Id ?? "(unknown project)";
        }
    }
}
