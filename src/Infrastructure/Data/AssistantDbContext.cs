using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<AssistantRun> AssistantRuns => Set<AssistantRun>();
    public DbSet<AssistantRunEvent> AssistantRunEvents => Set<AssistantRunEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssistantRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.UserGoal).IsRequired();
            e.Property(r => r.Status).HasConversion<string>();
        });

        modelBuilder.Entity<AssistantRunEvent>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Action).IsRequired();
            e.Property(ev => ev.ActorType).HasConversion<string>();
        });
    }
}
