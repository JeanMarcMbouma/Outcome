// -------------------------------
// Core contracts (Outcome-centric)
// -------------------------------
namespace BbQ.Cqrs;

/// <summary>
/// The core mediator interface for the CQRS pattern.
/// 
/// Provides a single, strongly-typed entry point for sending commands and queries.
/// All request/response pairs are resolved at compile-time with full type safety.
/// </summary>
/// <remarks>
/// The mediator is responsible for:
/// - Resolving the appropriate handler for a request
/// - Building and executing the pipeline of behaviors
/// - Passing the request through all behaviors in order
/// - Invoking the handler at the terminal of the pipeline
/// 
/// No reflection is used at runtime; all types are bound at compile-time.
/// </remarks>
public interface IMediator : ISender, IStreamer;
