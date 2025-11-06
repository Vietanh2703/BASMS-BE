namespace BuildingBlocks.Messaging;

/// <summary>
/// Marker interface for integration events
/// All events that cross service boundaries should implement this
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    Guid EventId => Guid.NewGuid();

    /// <summary>
    /// Timestamp when the event was created
    /// </summary>
    DateTime OccurredAt => DateTime.UtcNow;
}