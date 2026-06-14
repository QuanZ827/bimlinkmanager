namespace BimLinkManager.Models
{
    public enum LinkTaskStatus
    {
        Pending = 0,
        Running = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        Skipped = 5
    }
}
