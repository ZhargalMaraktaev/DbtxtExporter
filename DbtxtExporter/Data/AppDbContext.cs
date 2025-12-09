using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;

namespace DbtxtExporter.Data
{
    public class AppDbContext : DbContext
    { 
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Nkt12Rep> Nkt12Reps => Set<Nkt12Rep>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Nkt12Rep>(e =>
            {
                e.ToTable("NKT_12_REP", "dbo");
                e.HasKey(x => x.Id);
            });
        }
    }
}
