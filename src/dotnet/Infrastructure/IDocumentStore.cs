using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure;

public interface IDocumentStore
{
    
    Task<string> AddDocumentAsync(string document, string threadId, string folder);
    Task DeleteDocumentAsync(string documentName, string folder);
    Task<bool> DocumentExistsAsync(string documentName, string folder);
    Task<IEnumerable<string>> GetDocumentsAsync(string threadId);
    Task UpdateDocumentAsync(string documentName, string documentUri);
}
