using Microsoft.EntityFrameworkCore;
using QuoteHub.Api.Models;

namespace QuoteHub.Api.Data;

public class QuoteDbContext(DbContextOptions<QuoteDbContext> options) : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(q => q.Id);

            entity.Property(q => q.Text)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(q => q.Author)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(q => q.Category)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(q => q.Category);

            // Global Query Filter: automatically appends "WHERE IsDeleted = 0" to every query
            entity.HasQueryFilter(q => !q.IsDeleted);

            // Seed initial data through migrations
            entity.HasData(
                new Quote
                {
                    Id = 1,
                    Text = "Like what you do, and then you will do your best.",
                    Author = "Katherine Johnson",
                    Category = "Space",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Quote
                {
                    Id = 2,
                    Text = "The most dangerous phrase in the language is, 'We've always done it this way.'",
                    Author = "Grace Hopper",
                    Category = "Engineering",
                    CreatedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Quote
                {
                    Id = 3,
                    Text = "Simplicity is prerequisite for reliability.",
                    Author = "Edsger W. Dijkstra",
                    Category = "Engineering",
                    CreatedAtUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                }
            );
        });
    }
}
