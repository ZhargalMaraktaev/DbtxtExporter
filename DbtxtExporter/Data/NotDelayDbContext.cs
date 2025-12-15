using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;

namespace DbtxtExporter.Data;

public class NotDelayDbContext : DbContext
{
    public NotDelayDbContext(DbContextOptions<NotDelayDbContext> options) : base(options) { }

    public DbSet<NewNkt12Delay> NewNkt12Delay { get; set; }
    public DbSet<NewNkt3Delay> NewNkt3Delay { get; set; }
    public DbSet<NewNc9Delay> NewNc9Delay { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewNkt12Delay>(entity =>
        {
            entity.ToTable("NEW_NKT12", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Veracity).HasColumnType("decimal(6,2)");
        });

        modelBuilder.Entity<NewNkt3Delay>(entity =>
        {
            entity.ToTable("NEW_NKT3", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Veracity).HasColumnType("decimal(6,2)");
        });

        modelBuilder.Entity<NewNc9Delay>(entity =>
        {
            entity.ToTable("NEW_NC9", "dbo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Veracity).HasColumnType("decimal(6,2)");
        });
    }
}