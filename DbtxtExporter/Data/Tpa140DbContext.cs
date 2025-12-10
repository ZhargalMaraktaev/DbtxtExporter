using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;

namespace DbtxtExporter.Data;

public class Tpa140DbContext : DbContext
{
    public Tpa140DbContext(DbContextOptions<Tpa140DbContext> options) : base(options) { }
    public DbSet<Tpa140Nc9Rep> Tpa140Nc9Reps => Set<Tpa140Nc9Rep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tpa140Nc9Rep>(e =>
        {
            e.ToTable("TPA140_NC9_REP", "dbo");
            e.HasKey(x => x.Id);
        });
    }
}