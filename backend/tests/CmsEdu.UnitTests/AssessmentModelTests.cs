using CmsEdu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.UnitTests;

public class AssessmentModelTests
{
    [Fact]
    public void MigrationSnapshotMatchesCurrentModel()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
