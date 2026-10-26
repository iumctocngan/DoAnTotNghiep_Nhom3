using CmsEdu.Application.Common.Interfaces;
using CmsEdu.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence.Repositories;

public class NotebookRepository(AppDbContext db) : INotebookRepository
{
    public Task<List<Notebook>> GetAllAsync(string teacherId, CancellationToken cancellationToken) =>
        db.Notebooks
            .AsNoTracking()
            .Where(n => n.TeacherId == teacherId)
            .OrderByDescending(n => n.UpdatedAt)
            .Include(n => n.Documents)
            .ToListAsync(cancellationToken);

    public Task<Notebook?> GetByIdAsync(Guid id, string teacherId, CancellationToken cancellationToken) =>
        db.Notebooks
            .Include(n => n.Documents)
            .FirstOrDefaultAsync(n => n.Id == id && n.TeacherId == teacherId, cancellationToken);

    public Task<Document?> GetDocumentAsync(Guid notebookId, Guid documentId, string teacherId, CancellationToken cancellationToken) =>
        db.Documents
            .Include(d => d.Notebook)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.NotebookId == notebookId && d.Notebook.TeacherId == teacherId, cancellationToken);

    public async Task AddNotebookAsync(Notebook notebook, CancellationToken cancellationToken)
    {
        await db.Notebooks.AddAsync(notebook, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNotebookAsync(Notebook notebook, CancellationToken cancellationToken)
    {
        notebook.UpdatedAt = DateTime.UtcNow;
        db.Notebooks.Update(notebook);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNotebookAsync(Notebook notebook, CancellationToken cancellationToken)
    {
        db.Notebooks.Remove(notebook);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        await db.Documents.AddAsync(document, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        db.Documents.Update(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
    }
}
