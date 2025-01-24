namespace Domain.Chat
{
    public record MessageRequest
    {
        public required string Message { get; set; }
    }
}
