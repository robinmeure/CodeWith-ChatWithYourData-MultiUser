using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Chat
{
    public class Settings
    {
        public bool AllowInitialPromptRewrite { get; set; }

        public bool AllowFollowUpPrompts { get; set; }

        public bool AllowInitialPromptToHelpUser { get; set; }
        public bool UseSemanticRanker {get;set;}

        public List<PredefinedPrompt>? PredefinedPrompts { get; set; }

        public double Temperature { get; set; }
        public int Seed { get; set; }
    }

    public record PredefinedPrompt
    { 
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Prompt { get; set; }
    }
}
