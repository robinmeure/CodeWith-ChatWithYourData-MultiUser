using Azure.Search.Documents.Indexes;
using Infrastructure.Implementations.AISearch;
using Infrastructure.Implementations.SemanticKernel.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Implementations.SemanticKernel.Agents
{
    public class IncoseAgent
    {
        public ChatCompletionAgent CreateAgent(Kernel kernel, string agentName)
        {
            // Clone kernel instance to allow for agent specific plug-in definition
            Kernel agentKernel = kernel.Clone();

            if (!agentKernel.Plugins.Contains("get_incose_rules"))
            {
                agentKernel.Plugins.AddFromType<IncoseGuideLinesTool>("get_incose_rules");
            }

            string instruction = $"""
                1.	Fetch INCOSE Rules:
                •	Use the get_incose_rules function to retrieve the INCOSE ruleset for requirements validation.
                """;

            return
                new ChatCompletionAgent()
                {
                    Name = agentName,
                    Instructions = instruction,
                    Description = "Agent responsible for fetching the INCODE guidelines and the template to be used to do the validation.",
                    Kernel = agentKernel,

                    Arguments = new KernelArguments(
                        new OpenAIPromptExecutionSettings()
                        {
                            //ResponseFormat = typeof(GuideLines),
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
                            ServiceId = "completion",
                        })
                };
        }
    }
}

   
