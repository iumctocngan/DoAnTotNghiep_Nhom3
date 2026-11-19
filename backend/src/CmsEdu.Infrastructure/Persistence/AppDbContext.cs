using CmsEdu.Domain.Entities;
using CmsEdu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CmsEdu.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<StudentRemark> StudentRemarks => Set<StudentRemark>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // --- Module 3: AI Finance & Customer Care ---
    public DbSet<StudentRenewalPrediction> StudentRenewalPredictions => Set<StudentRenewalPrediction>();
    public DbSet<CskhChatSession> CskhChatSessions => Set<CskhChatSession>();
    public DbSet<CskhChatMessage> CskhChatMessages => Set<CskhChatMessage>();
    public DbSet<CskhInteraction> CskhInteractions => Set<CskhInteraction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ClassChatConversation> ClassChatConversations => Set<ClassChatConversation>();
    public DbSet<ClassChatMessage> ClassChatMessages => Set<ClassChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AssessmentRubric>().Property(x => x.Name).HasMaxLength(200);
        builder.Entity<AssessmentRubric>().HasOne(x => x.Class).WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssessmentRubric>().HasOne<LessonClo>().WithMany().HasForeignKey(x => x.LessonCloId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssessmentRubric>().HasIndex(x => x.ClassId);
        builder.Entity<AssessmentRubric>().HasIndex(x => new { x.ClassId, x.LessonCloId }).IsUnique().HasFilter("[LessonCloId] IS NOT NULL");
        builder.Entity<LessonClo>().Property(x => x.FileName).HasMaxLength(255);
        builder.Entity<LessonClo>().HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<LessonClo>().HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<LessonClo>().HasIndex(x => new { x.ClassId, x.LessonId, x.Id });
        builder.Entity<Assessment>().HasOne(x => x.Enrollment).WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Assessment>().HasOne(x => x.Rubric).WithMany().HasForeignKey(x => x.RubricId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Assessment>().Property(x => x.Score).HasPrecision(5, 2);
        builder.Entity<Assessment>().HasIndex(x => new { x.EnrollmentId, x.Date });
        builder.Entity<Assessment>().HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Assessment>().HasIndex(x => new { x.SessionId, x.RubricId, x.EnrollmentId })
            .IsUnique().HasFilter("[SessionId] IS NOT NULL");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
