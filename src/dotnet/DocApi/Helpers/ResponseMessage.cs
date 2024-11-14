using System.Text.Json.Serialization;

namespace WebApi.Helpers
{
    public record ResponseMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content)
    {
    }
}
