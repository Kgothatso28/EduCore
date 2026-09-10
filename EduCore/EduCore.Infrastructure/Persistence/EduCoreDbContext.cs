using Microsoft.EntityFrameworkCore;

namespace EduCore.Infrastructure.Persistence;

public class EduCoreDbContext : DbContext
{
    public EduCoreDbContext(DbContextOptions<EduCoreDbContext> options) : base(options)
    {
    }

    // DbSets will be added during migration of modules
}
