using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbtxtExporter.Data
{
    public class UrzBilDbContext : DbContext
    {
        public UrzBilDbContext(DbContextOptions<UrzBilDbContext> options) : base(options) { }
        public DbSet<UrzBilRep> UrzBilReps => Set<UrzBilRep>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UrzBilRep>(e =>
            {
                e.ToTable("URZ_BIL_REP", "dbo");
                e.HasKey(x => x.Id);
            });
        }
    }
}
