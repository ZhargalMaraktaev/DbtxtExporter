using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;

namespace DbtxtExporter.Data;

public class NotPakDbContext : DbContext
{
    public NotPakDbContext(DbContextOptions<NotPakDbContext> options) : base(options) { }
    public DbSet<NotPakMp6Rep> NotPakMp6Reps => Set<NotPakMp6Rep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotPakMp6Rep>(e =>
        {
            e.ToTable("NOT_PAK_MP6_REP", "dbo");
            e.HasKey(x => x.Id);
        });
    }
}