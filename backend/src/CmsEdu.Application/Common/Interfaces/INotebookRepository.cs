using CmsEdu.Domain.Entities;

namespace CmsEdu.Application.Common.Interfaces;

public interface INotebookRepository
{
    Task<List<Notebook>> GetAllAsync(string teacherId, CancellationToken cancellationToken);
    Task<Notebook?> GetByIdAsync(Guid id, string teacherId, CancellationToken cancellationToken);
    Task<Document?> GetDocumentAsync(Guid notebookId, Guid documentId, string teacherId, CancellationToken cancellationToken);
    Task AddNotebookAsync(Notebook notebook, CancellationToken cancellationToken);
    Task UpdateNotebookAsync(Notebook notebook, CancellationToken cancellationToken);
    Task DeleteNotebookAsync(Notebook notebook, CancellationToken cancellationToken);
    Task AddDocumentAsync(Document document, CancellationToken cancellationToken);
    Task UpdateDocumentAsync(Document document, CancellationToken cancellationToken);
    Task DeleteDocumentAsync(Document document, CancellationToken cancellationToken);
}
