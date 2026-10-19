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
    /// <summary>Development-only local demo login credentials.</summary>
    public DbSet<DemoCredential> DemoCredentials => Set<DemoCredential>();
    /// <summary>Student records filtered and guarded by the active school context.</summary>
    public DbSet<Student> Students => Set<Student>();

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
            entity.HasIndex(student => new { student.SchoolId, student.LocalId }).IsUnique();
        });
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
        foreach (var entry in ChangeTracker.Entries<TenantOwnedEntity>())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            if (!_tenantContext.SchoolId.HasValue ||
                entry.Entity.SchoolId != _tenantContext.SchoolId.Value)
            {
                throw new InvalidOperationException(
                    "Tenant-owned data can only be written in its validated school context.");
            }

            if (entry.State != EntityState.Added &&
                entry.Property(record => record.SchoolId).OriginalValue != entry.Entity.SchoolId)
            {
                throw new InvalidOperationException("A record's SchoolId is immutable.");
            }
        }
    }
}
