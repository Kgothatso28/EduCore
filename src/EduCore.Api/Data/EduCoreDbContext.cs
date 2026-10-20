using EduCore.Api.Domain;
using EduCore.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EduCore.Api.Data;

/// <summary>Maps the school directory and enforces tenant boundaries for persisted data.</summary>
public sealed class EduCoreDbContext(
    DbContextOptions<EduCoreDbContext> options,
    TenantContext tenantContext) : DbContext(options)
{
    private readonly TenantContext _tenantContext = tenantContext;

    /// <summary>School directory records, which are shared across tenant requests.</summary>
    public DbSet<School> Schools => Set<School>();
    /// <summary>Identity-to-school role assignments used to validate tenant access.</summary>
    public DbSet<SchoolMembership> SchoolMemberships => Set<SchoolMembership>();
    /// <summary>Platform-wide grants kept separate from school-scoped roles.</summary>
    public DbSet<PlatformAdministratorGrant> PlatformAdministratorGrants =>
        Set<PlatformAdministratorGrant>();
    /// <summary>Append-only records of platform administrative changes.</summary>
    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();
    /// <summary>Authentication outcomes recorded by EduCore-managed sign-in flows.</summary>
    public DbSet<PlatformLoginEvent> PlatformLoginEvents => Set<PlatformLoginEvent>();
    /// <summary>Development-only local demo login credentials.</summary>
    public DbSet<DemoCredential> DemoCredentials => Set<DemoCredential>();
    /// <summary>Student records filtered and guarded by the active school context.</summary>
    public DbSet<Student> Students => Set<Student>();
    /// <summary>School-owned class groups used by principal and teacher dashboards.</summary>
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    /// <summary>School profiles linked to trusted teacher identities.</summary>
    public DbSet<Teacher> Teachers => Set<Teacher>();
    /// <summary>Teacher-to-class subject assignments used to authorize class rosters.</summary>
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();

    /// <summary>Defines relational constraints and applies a fail-closed tenant filter.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("Schools");
            entity.HasKey(school => school.Id);
            entity.Property(school => school.Id).ValueGeneratedNever();
            entity.Property(school => school.Name).HasMaxLength(200).IsRequired();
            entity.Property(school => school.Slug).HasMaxLength(80).IsRequired();
            entity.HasIndex(school => school.Slug).IsUnique();
            entity.Property(school => school.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<SchoolMembership>(entity =>
        {
            entity.ToTable("SchoolMemberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.Id).ValueGeneratedNever();
            entity.Property(membership => membership.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(membership => membership.Role).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(membership => new { membership.SchoolId, membership.SubjectId })
                .IsUnique();
            entity.HasOne<School>()
                .WithMany()
                .HasForeignKey(membership => membership.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformAdministratorGrant>(entity =>
        {
            entity.ToTable("PlatformAdministratorGrants");
            entity.HasKey(grant => grant.Id);
            entity.Property(grant => grant.Id).ValueGeneratedNever();
            entity.Property(grant => grant.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(grant => grant.Permission).HasMaxLength(80).IsRequired();
            entity.HasIndex(grant => new { grant.SubjectId, grant.Permission }).IsUnique();
        });

        modelBuilder.Entity<PlatformAuditEvent>(entity =>
        {
            entity.ToTable("PlatformAuditEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TargetType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.TargetId).HasMaxLength(200);
            entity.Property(item => item.Details).HasMaxLength(1000);
            entity.HasIndex(item => item.CreatedUtc);
            entity.HasIndex(item => new { item.Action, item.CreatedUtc });
        });

        modelBuilder.Entity<PlatformLoginEvent>(entity =>
        {
            entity.ToTable("PlatformLoginEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.AccountIdentifier).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => item.CreatedUtc);
            entity.HasIndex(item => new { item.AccountIdentifier, item.CreatedUtc });
        });

        modelBuilder.Entity<DemoCredential>(entity =>
        {
            entity.ToTable("DemoCredentials");
            entity.HasKey(credential => credential.Id);
            entity.Property(credential => credential.Id).ValueGeneratedNever();
            entity.Property(credential => credential.UserName).HasMaxLength(120).IsRequired();
            entity.Property(credential => credential.NormalizedUserName)
                .HasMaxLength(120).IsRequired();
            entity.Property(credential => credential.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(credential => credential.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(credential => credential.NormalizedUserName).IsUnique();
            entity.HasIndex(credential => credential.SubjectId).IsUnique();
        });

        modelBuilder.Entity<TenantOwnedEntity>().UseTpcMappingStrategy();
        modelBuilder.Entity<TenantOwnedEntity>()
            .HasKey(record => record.Id);
        modelBuilder.Entity<TenantOwnedEntity>()
            .Property(record => record.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<TenantOwnedEntity>()
            .Property(record => record.SchoolId)
            .IsRequired();
        // An unvalidated or missing school context intentionally matches no tenant-owned rows.
        modelBuilder.Entity<TenantOwnedEntity>()
            .HasQueryFilter(record =>
                record.SchoolId == _tenantContext.SchoolId.GetValueOrDefault() &&
                _tenantContext.SchoolId.HasValue);
        modelBuilder.Entity<TenantOwnedEntity>()
            .HasOne<School>()
            .WithMany()
            .HasForeignKey(record => record.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.Property(student => student.LocalId).HasMaxLength(80).IsRequired();
            entity.Property(student => student.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(student => student.LastName).HasMaxLength(100).IsRequired();
            entity.Property(student => student.SubjectId).HasMaxLength(200);
            entity.HasIndex(student => new { student.SchoolId, student.LocalId }).IsUnique();
            entity.HasIndex(student => new { student.SchoolId, student.SubjectId })
                .IsUnique()
                .HasFilter("[SubjectId] IS NOT NULL");
        });

        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("SchoolClasses");
            entity.HasKey(schoolClass => new { schoolClass.SchoolId, schoolClass.Id });
            entity.Property(schoolClass => schoolClass.Id).ValueGeneratedNever();
            entity.Property(schoolClass => schoolClass.SchoolId).IsRequired();
            entity.Property(schoolClass => schoolClass.Name).HasMaxLength(120).IsRequired();
            entity.Property(schoolClass => schoolClass.GradeLevel).HasMaxLength(40).IsRequired();
            entity.HasIndex(schoolClass => new { schoolClass.SchoolId, schoolClass.Name })
                .IsUnique();
            entity.HasOne<School>()
                .WithMany()
                .HasForeignKey(schoolClass => schoolClass.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(schoolClass =>
                schoolClass.SchoolId == _tenantContext.SchoolId.GetValueOrDefault() &&
                _tenantContext.SchoolId.HasValue);
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("Teachers");
            entity.HasKey(teacher => new { teacher.SchoolId, teacher.Id });
            entity.Property(teacher => teacher.Id).ValueGeneratedNever();
            entity.Property(teacher => teacher.SchoolId).IsRequired();
            entity.Property(teacher => teacher.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(teacher => teacher.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(teacher => teacher.LastName).HasMaxLength(100).IsRequired();
            entity.HasIndex(teacher => new { teacher.SchoolId, teacher.SubjectId }).IsUnique();
            entity.HasOne<School>()
                .WithMany()
                .HasForeignKey(teacher => teacher.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(teacher =>
                teacher.SchoolId == _tenantContext.SchoolId.GetValueOrDefault() &&
                _tenantContext.SchoolId.HasValue);
        });

        modelBuilder.Entity<TeachingAssignment>(entity =>
        {
            entity.ToTable("TeachingAssignments");
            entity.HasKey(assignment => new { assignment.SchoolId, assignment.Id });
            entity.Property(assignment => assignment.Id).ValueGeneratedNever();
            entity.Property(assignment => assignment.SchoolId).IsRequired();
            entity.Property(assignment => assignment.SubjectName).HasMaxLength(120).IsRequired();
            entity.HasIndex(assignment => new
                {
                    assignment.SchoolId, assignment.TeacherId, assignment.ClassId,
                    assignment.SubjectName
                })
                .IsUnique();
            entity.HasOne<Teacher>()
                .WithMany()
                .HasForeignKey(assignment => new { assignment.SchoolId, assignment.TeacherId })
                .HasPrincipalKey(teacher => new { teacher.SchoolId, teacher.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SchoolClass>()
                .WithMany()
                .HasForeignKey(assignment => new { assignment.SchoolId, assignment.ClassId })
                .HasPrincipalKey(schoolClass => new { schoolClass.SchoolId, schoolClass.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<School>()
                .WithMany()
                .HasForeignKey(assignment => assignment.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(assignment =>
                assignment.SchoolId == _tenantContext.SchoolId.GetValueOrDefault() &&
                _tenantContext.SchoolId.HasValue);
        });

        modelBuilder.Entity<Student>()
            .HasOne<SchoolClass>()
            .WithMany()
            .HasForeignKey(student => new { student.SchoolId, student.ClassId })
            .HasPrincipalKey(schoolClass => new { schoolClass.SchoolId, schoolClass.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Validates tenant ownership before synchronously persisting tracked changes.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>Validates tenant ownership before asynchronously persisting tracked changes.</summary>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ValidateTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Rejects tenantless, cross-school, or reassigned tenant-owned writes.</summary>
    private void ValidateTenantWrites()
    {
        ValidateAppendOnly<PlatformAuditEvent>();
        ValidateAppendOnly<PlatformLoginEvent>();
        ValidateTenantWrites(
            ChangeTracker.Entries<TenantOwnedEntity>(),
            record => record.SchoolId);
        ValidateTenantWrites(
            ChangeTracker.Entries<SchoolClass>(),
            record => record.SchoolId);
        ValidateTenantWrites(
            ChangeTracker.Entries<Teacher>(),
            record => record.SchoolId);
        ValidateTenantWrites(
            ChangeTracker.Entries<TeachingAssignment>(),
            record => record.SchoolId);
    }

    /// <summary>Prevents application code from editing or deleting recorded security history.</summary>
    private void ValidateAppendOnly<TEntity>() where TEntity : class
    {
        if (ChangeTracker.Entries<TEntity>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Security history is append-only.");
        }
    }

    /// <summary>Rejects writes outside the active tenant and prevents reassignment of persisted records.</summary>
    private void ValidateTenantWrites<TEntity>(
        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity>> entries,
        Func<TEntity, Guid> schoolIdFor)
        where TEntity : class
    {
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            var schoolId = schoolIdFor(entry.Entity);
            if (!_tenantContext.SchoolId.HasValue ||
                schoolId != _tenantContext.SchoolId.Value)
            {
                throw new InvalidOperationException(
                    "Tenant-owned data can only be written in its validated school context.");
            }

            if (entry.State != EntityState.Added &&
                (Guid)entry.Property("SchoolId").OriginalValue! != schoolId)
            {
                throw new InvalidOperationException("A record's SchoolId is immutable.");
            }
        }
    }
}
