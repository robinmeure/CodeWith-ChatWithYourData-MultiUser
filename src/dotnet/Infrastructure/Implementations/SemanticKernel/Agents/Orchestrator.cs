using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Implementations.SemanticKernel.Tools;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace Infrastructure.Implementations.SemanticKernel.Agents
{
    public class Orchestrator
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
                    Instructions = $"""
                    Formatting re-enabled
                    You are orchestrator to make sure all the neccesary information is available to validate a document regarding compliancy rules.
                    This is the plan you need to follow:
                    1. Retrieve all the requirements for a given document (get_requirements), make sure all the requirements are extracted. Consolidate and refactor the input to have an uniform markdown table format.
                    2. Retrieve a list of the INCOSE guidelines (get_incose_rules) For every requirement you process, validate these according to the incose guidelines and follow the instructions of each guidelines to decide whether or not a requirement has passed the incose validation.
                    3. For each requirement, write the result of the validation in a markdown table format. Use the template (get_incose_template) to write the table that covers every requirement with its INCOSE assessment.
                    IMPORTANT: 
                    - Use the template (get_incose_template) to write the table that covers every requirement with its INCOSE assessment.
                    - Make sure all requirements have been checked and the output document covers all the requirements with their assessment.
                    - You might get feedback from the Reviewer agent, make sure you process this feedback and update the document accordingly. 
                    Keep in mind that the most important job is make sure that all requirements have been checked and the output document covers all the requirements with their assessment.
                    DO NOT MODIFY THE REQUIREMENTS, ONLY VALIDATE THESE AGAINST THE GUIDELINES.

                """,
                    Kernel = kernel,
                    Arguments = new KernelArguments(
                        new AzureOpenAIPromptExecutionSettings()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                            ServiceId = "completion"
                        })
                };
        }
    }
}
