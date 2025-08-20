using Microsoft.EntityFrameworkCore;
using ActivityExplorer.Core.Models;
using System.Text.Json;

namespace ActivityExplorer.Data
{
    public class ActivityContext : DbContext
    {
        public ActivityContext(DbContextOptions<ActivityContext> options) : base(options) { }

        public DbSet<Activity> Activities { get; set; }
        public DbSet<SavedFilter> SavedFilters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Activity configuration
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Operation);
                entity.HasIndex(e => e.Workload);
                
                // Configure the AdditionalDetails JSON column
                entity.Property(e => e.AdditionalDetails)
                    .HasConversion(
                        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null));
            });

            // SavedFilter configuration
            modelBuilder.Entity<SavedFilter>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
            });
        }
    }
}