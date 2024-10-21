using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

public interface IDocumentRegistry
{
    Task<string> AddDocumentToThreadAsync(DocsPerThread docsPerThread);
    Task<List<DocsPerThread>> GetDocsPerThreadAsync(string threadId);
    Task<bool> RemoveDocumentFromThreadAsync(List<DocsPerThread> documents);
    Task<bool> RemoveDocumentAsync(DocsPerThread document);

}
