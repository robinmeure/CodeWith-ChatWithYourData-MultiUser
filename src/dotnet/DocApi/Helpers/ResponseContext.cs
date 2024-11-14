using System.Text.Json.Serialization;

namespace WebApi.Helpers
{
    public record ResponseContext(
       [property: JsonPropertyName("followup_questions")] string[] FollowupQuestions)
    {
    }
}
