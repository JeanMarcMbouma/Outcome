namespace BbQ.Events;

/// <summary>
/// Defines strategies for handling backpressure when event ingestion outpaces projection processing.
/// </summary>
/// <remarks>
/// Backpressure strategies control how the projection engine behaves when the internal
/// event queue reaches capacity, preventing unbounded memory growth.
/// 
/// Strategy comparison:
/// - **Block**: Highest reliability, may slow down event publishers
/// - **DropNewest**: Preserves older events, useful for historical data
/// - **DropOldest**: Preserves recent events, useful for real-time systems
/// 
/// Choose a strategy based on your application's requirements:
/// - Use Block for critical projections where no data loss is acceptable
/// - Use DropNewest for debugging or when older events are more valuable
/// - Use DropOldest for real-time dashboards where recent data matters most
/// </remarks>
public enum BackpressureStrategy
{
    /// <summary>
    /// Block event publishers when the queue is full, applying backpressure upstream.
    /// </summary>
    /// <remarks>
    /// This is the most conservative strategy that ensures no events are dropped.
    /// Publishers will wait until queue space is available before continuing.
    /// 
    /// Use this when:
    /// - Data loss is unacceptable
    /// - Projection processing must keep pace with ingestion
    /// - You can tolerate slower event publishing
    /// 
    /// Trade-offs:
    /// - ✅ No data loss
    /// - ✅ Applies natural backpressure to producers
    /// - ❌ May slow down event publishers
    /// - ❌ Can create cascading delays in the system
    /// </remarks>
    Block = 0,

    /// <summary>
    /// Drop newest events when the queue is full, preserving older events.
    /// </summary>
    /// <remarks>
    /// When the queue reaches capacity, newly incoming events are dropped
    /// while older events in the queue continue to be processed.
    /// 
    /// Use this when:
    /// - Historical event order is important
    /// - You're debugging and want to see earlier events
    /// - Older events have higher business value
    /// 
    /// Trade-offs:
    /// - ✅ Preserves historical events
    /// - ✅ No publisher blocking
    /// - ❌ Recent events may be lost
    /// - ⚠️ Recommended for debugging only
    /// </remarks>
    DropNewest = 1,

    /// <summary>
    /// Drop oldest events when the queue is full, preserving newer events.
    /// </summary>
    /// <remarks>
    /// When the queue reaches capacity, the oldest events in the queue are
    /// dropped to make room for newer events.
    /// 
    /// Use this when:
    /// - Recent events are more important than old ones
    /// - You're building real-time dashboards or monitoring
    /// - Historical completeness is less critical
    /// 
    /// Trade-offs:
    /// - ✅ Always processes most recent events
    /// - ✅ No publisher blocking
    /// - ✅ Good for real-time systems
    /// - ❌ Historical events may be lost
    /// - ⚠️ May skip important state transitions
    /// </remarks>
    DropOldest = 2
}
