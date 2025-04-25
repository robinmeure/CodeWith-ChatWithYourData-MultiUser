using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace Infrastructure.Implementations.SemanticKernel.Agents
{
    public class Reviewer
    {
        public ChatCompletionAgent CreateAgent(Kernel kernel, string agentName)
        {
            // Clone kernel instance to allow for agent specific plug-in definition
            Kernel agentKernel = kernel.Clone();

            // Create the agent
            return
                new ChatCompletionAgent()
                {
                    Name = agentName,
                    Instructions =
                    """
                    The document you are about to review is a markdown table with the requirements and the INCOSE assessment.
                    ITS NOT YOUR JOB TO ACT ON THE ASSESSMENT output, but to review the document and provide feedback to the Orchestrator agent.
                    It's not your job to correct the document, but to provide feedback to the Orchestrator agent.

                    Never directly perform the correction or provide example.
                    Once the content has been updated in a subsequent response, you will review the content again until satisfactory.
                    If you feel think the quality of the document is satisfactory, you can stop the process.
                    Make sure that the template is used to write the document and that all the requirements and the validations are covered (get_incose_template)
                    The output MUST be in table markdown.

                    RULES:
                    - Only identify suggestions that are specific and actionable.
                    - Verify previous suggestions have been addressed.
                    - Never repeat previous suggestions.
                    """,
                    Kernel = kernel,
                    Arguments = new KernelArguments(
                        new AzureOpenAIPromptExecutionSettings()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                            ServiceId = "reasoning"
                        })
                };
        }
    }
}
