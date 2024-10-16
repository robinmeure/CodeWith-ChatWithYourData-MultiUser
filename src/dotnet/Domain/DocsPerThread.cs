namespace Domain
{
    public class DocsPerThread
    {
        public string Id { get; set; }
        public string ThreadId { get; set; }
        public string UserId { get; set; }
        public string DocumentName { get; set; }
        public string DocumentUri { get; set; }
        public bool Deleted { get; set; }

    }
}
