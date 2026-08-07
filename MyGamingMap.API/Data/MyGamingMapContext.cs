using Microsoft.EntityFrameworkCore;
using MyGamingMap.API.Models.Entities;

namespace MyGamingMap.API.Data;

public class MyGamingMapContext(DbContextOptions<MyGamingMapContext> options) : DbContext(options)
{
    public DbSet<Game> Games { get; set; }

    public DbSet<Collection> Collections { get; set; }

    public DbSet<Company> Companies { get; set; }

    public DbSet<FailedLookup> FailedLookups { get; set; }

    public DbSet<Franchise> Franchises { get; set; }

    public DbSet<GameEngine> GameEngines { get; set; }

    public DbSet<GameMapping> GameMappings { get; set; }

    public DbSet<GameMode> GameModes { get; set; }

    public DbSet<GameType> GameTypes { get; set; }

    public DbSet<Genre> Genres { get; set; }

    public DbSet<InvolvedCompany> InvolvedCompanies { get; set; }

    public DbSet<PlayerPerspective> PlayerPerspectives { get; set; }

    public DbSet<ReleaseDate> ReleaseDates { get; set; }

    public DbSet<Region> Regions { get; set; }

    public DbSet<Screenshot> Screenshots { get; set; }

    public DbSet<Theme> Themes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Collection>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<FailedLookup>()
            .HasIndex(f => new { f.Name, f.Platform })
            .IsUnique();

        modelBuilder.Entity<Franchise>()
            .HasIndex(f => f.Name)
            .IsUnique();

        modelBuilder.Entity<GameEngine>()
            .HasIndex(c => c.Name);

        modelBuilder.Entity<GameMapping>()
            .HasOne(gm => gm.Game)
            .WithMany(g => g.Mappings)
            .HasForeignKey(gm => gm.IGDBId);

        modelBuilder.Entity<GameMode>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<GameType>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<GameType>()
            .Property(g => g.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Genre>()
            .HasIndex(g => g.Name)
            .IsUnique();

        modelBuilder.Entity<PlayerPerspective>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<Screenshot>()
            .HasOne(s => s.Game)
            .WithMany(g => g.Screenshots)
            .HasForeignKey(s => s.GameId);

        modelBuilder.Entity<Theme>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<InvolvedCompany>()
            .HasOne(ic => ic.Game)
            .WithMany(g => g.InvolvedCompanies)
            .HasForeignKey(ic => ic.GameId);
    }
}