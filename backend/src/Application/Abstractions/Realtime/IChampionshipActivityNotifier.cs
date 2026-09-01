namespace Application.Abstractions.Realtime;

/// <summary>
/// Tells anyone watching a championship that something in it changed —
/// nothing about what, just that it is worth fetching again. Kept this coarse
/// on purpose: one signal that every screen already knows how to react to
/// (refetch through the same service call it already uses) instead of a
/// typed event per entity that changed.
/// </summary>
public interface IChampionshipActivityNotifier
{
    Task NotifyAsync(Guid championshipId, CancellationToken cancellationToken);
}
