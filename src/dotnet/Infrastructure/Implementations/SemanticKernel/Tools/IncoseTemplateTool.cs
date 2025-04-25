using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Implementations.SemanticKernel.Tools
{
    public class IncoseTemplateTool
    {
        [KernelFunction("get_incose_template")]
        [Description("Retrieves a template for an INCOSE validation")]

        public string Template()
        {
            Console.WriteLine("--- Template Tool Invoked ---");

            return $$"""
            [
                {
                  "ID": "R00.001",
                  "Requirement": "locked rotor and locking moving parts",
                  "Description":"locked rotor and locking moving parts",              
                  "Remarks": "No product requirement created as limits are not critical",
                  "INCOSE Assessment":"{Pass/Evaluate/Rewrite/Unable}",
                  "INCOSE Notes":""
                }
            ]
            """;
        }
    }
}
