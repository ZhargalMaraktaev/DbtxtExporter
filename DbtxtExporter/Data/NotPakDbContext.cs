using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;

namespace DbtxtExporter.Data;

public class NotPakDbContext : DbContext
{
    public NotPakDbContext(DbContextOptions<NotPakDbContext> options) : base(options) { }
    public DbSet<NotPakRep> NotPakMp6Reps => Set<NotPakRep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotPakRep>(e =>
        {
            e.ToTable("NOT_PAK_REP", "dbo");
            e.HasKey(x => x.Id);
        });
    }
}