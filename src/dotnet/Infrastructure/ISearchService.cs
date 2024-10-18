using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

public interface ISearchService
{
    Task<bool> IsChunkingComplete(DocsPerThread docsPerThread);
    Task<bool> HasIndexingStarted(string threadId);
}
