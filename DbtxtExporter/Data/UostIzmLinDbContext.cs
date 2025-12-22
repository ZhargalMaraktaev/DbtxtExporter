using DbtxtExporter.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbtxtExporter.Data
{
    public class UostIzmLinDbContext : DbContext
    {
        public UostIzmLinDbContext(DbContextOptions<UostIzmLinDbContext> options) : base(options) { }
        public DbSet<UostIzmLinRep> UostIzmLinReps => Set<UostIzmLinRep>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UostIzmLinRep>(e =>
            {
                e.ToTable("UOST_IzmLin_REP", "dbo");
                e.HasKey(x => x.id);
            });
        }
    }
}
