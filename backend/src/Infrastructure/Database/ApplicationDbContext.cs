using Application.Abstractions.Data;
using Domain.Championships;
using Domain.ChipSets;
using Domain.Seasons;
using Domain.Tables;
using Domain.Users;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Championship> Championships { get; set; }

    public DbSet<ChampionshipMember> ChampionshipMembers { get; set; }

    public DbSet<Invite> Invites { get; set; }

    public DbSet<ChipSet> ChipSets { get; set; }

    public DbSet<ChipDenomination> ChipDenominations { get; set; }

    public DbSet<Season> Seasons { get; set; }

    public DbSet<PokerTable> Tables { get; set; }

    public DbSet<TablePlayer> TablePlayers { get; set; }

    public DbSet<LedgerEntry> LedgerEntries { get; set; }

    public DbSet<LedgerEntryChip> LedgerEntryChips { get; set; }

    public DbSet<FinalCount> FinalCounts { get; set; }

    public DbSet<Settlement> Settlements { get; set; }

    public DbSet<SettlementTransfer> SettlementTransfers { get; set; }

    public DbSet<TableResult> TableResults { get; set; }

    public DbSet<BlindLevel> BlindLevels { get; set; }

    public DbSet<TableClock> TableClocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // When should you publish domain events?
        //
        // 1. BEFORE calling SaveChangesAsync
        //     - domain events are part of the same transaction
        //     - immediate consistency
        // 2. AFTER calling SaveChangesAsync
        //     - domain events are a separate transaction
        //     - eventual consistency
        //     - handlers can fail

        List<IDomainEvent> domainEvents = ExtractDomainEvents();
        int result = await base.SaveChangesAsync(cancellationToken);

        await PublishDomainEventsAsync(domainEvents);

        return result;
    }

    private async Task PublishDomainEventsAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        await domainEventsDispatcher.DispatchAsync(domainEvents);
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        var domainEvents = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                List<IDomainEvent> domainEvents = entity.DomainEvents;

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();
        return domainEvents;
    }
}
