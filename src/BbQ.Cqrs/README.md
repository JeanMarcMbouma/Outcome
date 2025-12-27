# BbQ.Cqrs

A lightweight, extensible CQRS (Command Query Responsibility Segregation) implementation that integrates seamlessly with `Outcome` for comprehensive error handling.

## ✨ Features

- **Type-safe mediator** for commands and queries with compile-time checking
- **Pipeline behaviors** for cross-cutting concerns (logging, validation, caching)
- **Extensible behavior pipeline** with customizable middleware support
- **Test utilities** with `TestMediator` and `StubHandler` for isolated testing
- **Comprehensive documentation** on all interfaces and classes with XML comments
- **Seamless integration** with `Outcome<T>` for advanced error management
- **Source generators** for automatic handler and behavior registration (opt-in via attributes)

## 🔧 Source Generators

BbQ.Cqrs includes Roslyn source generators that can automatically detect and register your handlers and behaviors, reducing boilerplate code.

### Important: Register IMediator First

**Always call `AddBbQMediator()` before using the generated registration methods** to ensure `IMediator` is registered:

```csharp
// Option 1: Use assembly scanning for everything (uses reflection)
services.AddBbQMediator(typeof(Program).Assembly);

// Option 2: Register IMediator only, then use source generators (recommended)
services.AddBbQMediator(Array.Empty<Assembly>());  // Just register IMediator
services.AddYourAssemblyNameHandlers();  // Use generated method - compile-time, no reflection
services.AddYourAssemblyNameBehaviors(); // Use generated method
```

**Important Note on Assembly Scanning:**
When you pass assemblies to `AddBbQMediator()`, it uses reflection-based assembly scanning to discover and register handlers. This approach:
- Uses runtime reflection, which is slower than compile-time source generation
- Should be used sparingly, primarily when reusing queries and handlers from external libraries
- For your own application code, prefer using source generators (generated `AddXxxHandlers()` methods) for better performance and compile-time safety

**Recommended Pattern:**
```csharp
// For your application's handlers (preferred - uses source generators)
services.AddBbQMediator(Array.Empty<Assembly>());  // Register IMediator only
services.AddMyAppHandlers();  // Use generated method - compile-time, no reflection
services.AddMyAppBehaviors();  // Use generated method for behaviors

// For library handlers (when needed - uses reflection)
services.AddBbQMediator(typeof(SharedLibrary).Assembly);  // Assembly scanning for libraries
```

The generated methods support customizable lifetimes:

```csharp
// Customize handler and behavior lifetimes
services.AddBbQMediator(typeof(Program).Assembly);
services.AddYourAssemblyNameHandlers(ServiceLifetime.Transient);
services.AddYourAssemblyNameBehaviors(ServiceLifetime.Singleton);

// Or use the combined method
services.AddBbQMediator(typeof(Program).Assembly);
services.AddYourAssemblyNameCqrs(
    handlersLifetime: ServiceLifetime.Scoped,
    behaviorsLifetime: ServiceLifetime.Scoped
);
```

### Automatic Handler Registration

The source generator automatically detects all handlers implementing `IRequestHandler<,>` and `IRequestHandler<>` and generates extension methods to register them:

```csharp
// The generator creates methods like:
services.AddYourAssemblyNameHandlers();  // Registers all detected handlers
```

All handlers for requests implementing `ICommand<T>` or `IQuery<T>` are automatically detected without needing attributes.

### Behavior Registration with Order

Mark your behaviors with the `[Behavior]` attribute to enable automatic registration in the specified order:

```csharp
[Behavior(Order = 1)]  // Executes first (outermost)
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> { }

[Behavior(Order = 2)]  // Executes second
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> { }

[Behavior(Order = 3)]  // Executes third (closest to handler)
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> { }
```

Then register them:

```csharp
services.AddYourAssemblyNameBehaviors();  // Registers behaviors in order
// Or register everything at once:
services.AddYourAssemblyNameCqrs();  // Registers both handlers and behaviors
```

**Note:** Only behaviors with the `[Behavior]` attribute are automatically registered by the generator.

## 💾 Installation

```bash
dotnet add package BbQ.Cqrs
```

## 🚀 Quick Start

### 1. Register the Mediator

```csharp
// Program.cs
services.AddBbQMediator(
    typeof(CreateUserCommandHandler).Assembly,  // Your handlers assembly
    typeof(Program).Assembly  // Or current assembly if handlers are local
);
```

The `AddBbQMediator()` method automatically:
- Registers `IMediator` as a singleton
- Scans assemblies for all `IRequestHandler<,>` and `IRequestHandler<>` implementations
- Registers handlers with scoped lifetime by default
- You can customize the handler lifetime by passing a `ServiceLifetime` parameter

### 2. Define Commands and Queries

```csharp
// Commands (state-modifying operations)
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
    public string Name { get; set; }
}

// Queries (read-only operations)
public class GetUserByIdQuery : IQuery<Outcome<User>>
{
    public Guid UserId { get; set; }
}
```

### 3. Implement Handlers

```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    private readonly IUserRepository _repository;

    public CreateUserCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var user = new User { Email = request.Email, Name = request.Name };
        await _repository.AddAsync(user, ct);
        return Outcome<User>.From(user);
    }
}
```

### 4. Use in Your Application

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var command = new CreateUserCommand 
        { 
            Email = request.Email, 
            Name = request.Name 
        };

        var result = await _mediator.Send(command, ct);

        return result.Match(
            onSuccess: user => CreatedAtAction(nameof(GetUser), new { id = user.Id }, user),
            onError: errors => BadRequest(new { errors })
        );
    }
}
```

---

## 🧩 Core Components

### IMediator
The central dispatcher for sending commands and queries through the pipeline.

**Generic Send (with response):**
```csharp
Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
    where TRequest : IRequest<TResponse>;
```

**Fire-and-forget Send (no response):**
```csharp
Task Send(IRequest request, CancellationToken ct = default);
```

### ICommandDispatcher
A specialized dispatcher for commands that provides clear separation between command and query operations.

```csharp
public interface ICommandDispatcher
{
    Task<TResponse> Dispatch<TResponse>(ICommand<TResponse> command, CancellationToken ct = default);
    Task Dispatch(ICommand<Unit> command, CancellationToken ct = default);
}
```

**Benefits:**
- Explicit separation between commands (state-changing) and queries (read-only)
- Better discoverability and documentation
- Type safety with compile-time checking
- No runtime scanning or hidden magic – uses dependency injection, with reflection only for initial pipeline construction (cached for subsequent calls)

**Example usage:**
```csharp
public class UserController
{
    private readonly ICommandDispatcher _commandDispatcher;
    
    public async Task<IActionResult> CreateUser(CreateUserCommand command, CancellationToken ct)
    {
        var result = await _commandDispatcher.Dispatch(command, ct);
        return result.Match(
            onSuccess: user => Ok(user),
            onError: errors => BadRequest(errors)
        );
    }
}
```

### IQueryDispatcher
A specialized dispatcher for queries that provides clear separation between command and query operations.

```csharp
public interface IQueryDispatcher
{
    Task<TResponse> Dispatch<TResponse>(IQuery<TResponse> query, CancellationToken ct = default);
}
```

**Example usage:**
```csharp
public class UserController
{
    private readonly IQueryDispatcher _queryDispatcher;
    
    public async Task<IActionResult> GetUser(GetUserByIdQuery query, CancellationToken ct)
    {
        var result = await _queryDispatcher.Dispatch(query, ct);
        return result.Match(
            onSuccess: user => Ok(user),
            onError: errors => NotFound(errors)
        );
    }
}
```

### ICommand<TResponse>
Marker interface for state-modifying operations (create, update, delete).

```csharp
public class CreateUserCommand : ICommand<Outcome<User>>
{
    public string Email { get; set; }
}
```

### IQuery<TResponse>
Marker interface for read-only operations (should be idempotent).

```csharp
public class GetUserByIdQuery : IQuery<Outcome<User>>
{
    public Guid UserId { get; set; }
}
```

### IRequest (Fire-and-Forget)
Marker interface for operations that don't return a meaningful value. Useful for notifications, events, or background tasks.

```csharp
public class SendUserNotificationCommand : IRequest
{
    public string UserId { get; set; }
    public string Message { get; set; }
}
```

### IRequestHandler<TRequest, TResponse>
Implements the actual logic for handling a request with a response.

```csharp
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(request.UserId, ct);
        return user == null 
            ? UserErrorsErrors.NotFoundError.ToOutcome<User>()
            : Outcome<User>.From(user);
    }
}
```

### IRequestHandler<TRequest> (Fire-and-Forget)
Implements logic for fire-and-forget operations with no return value.

```csharp
public class SendUserNotificationHandler : IRequestHandler<SendUserNotificationCommand>
{
    private readonly INotificationService _notificationService;

    public async Task Handle(SendUserNotificationCommand request, CancellationToken ct)
    {
        await _notificationService.SendAsync(request.UserId, request.Message, ct);
        // No return value needed
    }
}
```

### IPipelineBehavior<TRequest, TResponse>
Middleware for cross-cutting concerns. Behaviors form a chain around the handler.

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        _logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next(request, ct);
        _logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

---

## 🔁 Error Handling with Outcome

Define error codes with the source generator:

```csharp
[QbqOutcome]
public enum UserErrorCode
{
    [System.ComponentModel.Description("Email is already in use")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    EmailAlreadyExists,

    [System.ComponentModel.Description("User not found")]
    NotFound,

    [System.ComponentModel.Description("Invalid email format")]
    [ErrorSeverity(ErrorSeverity.Validation)]
    InvalidEmail
}
```

Use in handlers:

```csharp
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        // Check if email already exists
        if (await _repository.ExistsByEmailAsync(request.Email, ct))
        {
            return UserErrorCodeErrors.EmailAlreadyExistsError.ToOutcome<User>();
        }

        var user = new User { Email = request.Email, Name = request.Name };
        await _repository.AddAsync(user, ct);
        return Outcome<User>.From(user);
    }
}
```

---

## 🔥 Fire-and-Forget Commands

For operations that don't return a meaningful value (notifications, events, background tasks), use the fire-and-forget pattern with `IRequest` and `IRequestHandler<TRequest>`.

### Define a Fire-and-Forget Command

```csharp
public class SendUserNotificationCommand : IRequest
{
    public string UserId { get; set; }
    public string Message { get; set; }

    public SendUserNotificationCommand(string userId, string message)
    {
        UserId = userId;
        Message = message;
    }
}
```

### Implement a Fire-and-Forget Handler

```csharp
public class SendUserNotificationHandler : IRequestHandler<SendUserNotificationCommand>
{
    private readonly INotificationService _notificationService;

    public SendUserNotificationHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Handle(SendUserNotificationCommand request, CancellationToken ct)
    {
        await _notificationService.SendAsync(request.UserId, request.Message, ct);
        // No return value needed - just perform the side effect
    }
}
```

### Use Fire-and-Forget Commands

```csharp
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendNotification(
        string userId, 
        string message, 
        CancellationToken ct)
    {
        // Send the command - no response value to check
        await _mediator.Send(new SendUserNotificationCommand(userId, message), ct);
        
        // Just return success
        return Ok();
    }
}
```

### Benefits

- **Cleaner API**: No need to return `Unit` or dummy values
- **Clear Intent**: Developers immediately see this is a fire-and-forget operation
- **Type-Safe**: `IRequest` (non-generic) clearly indicates void-like behavior
- **Composable**: Works seamlessly with pipeline behaviors
- **Testable**: Can be tested with `TestMediator`

### Behaviors with Fire-and-Forget

Fire-and-forget commands work with all behaviors, just like regular commands:

```csharp
// Register the mediator and handlers
services.AddBbQMediator(typeof(Program).Assembly);

// Register behaviors for both fire-and-forget and regular requests
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

---

## 🧭 Pipeline Behaviors

Behaviors execute in registration order: **first registered = outermost = executes first**.

### Built-in: LoggingBehavior

To use logging behavior, register it explicitly:

```csharp
services.AddBbQMediator(typeof(Program).Assembly);

// Add logging behavior
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

### Custom: ValidationBehavior

```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        var validationResult = await _validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await next(request, ct);
    }
}

// Register it
services.AddBbQMediator(typeof(Program).Assembly);
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

### Custom: CachingBehavior

```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICacheable
{
    private readonly IMemoryCache _cache;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        var cacheKey = $"{typeof(TRequest).Name}_{request.GetCacheKey()}".ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out TResponse cachedResult))
        {
            return cachedResult;
        }

        var result = await next(request, ct);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }
}

// Mark queries as cacheable
public class GetUserByIdQuery : IQuery<Outcome<User>>, ICacheable
{
    public Guid UserId { get; set; }
    public string GetCacheKey() => UserId.ToString();
}
```

### Custom: PerformanceBehavior

```csharp
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, Task<TResponse>> next)
    {
        var timer = Stopwatch.StartNew();
        var response = await next(request, ct);
        timer.Stop();

        if (timer.ElapsedMilliseconds > 1000)
        {
            _logger.LogWarning(
                "Long-running request: {RequestType} took {ElapsedMilliseconds}ms",
                typeof(TRequest).Name,
                timer.ElapsedMilliseconds);
        }

        return response;
    }
}
```

### Behavior Registration Order

```csharp
services.AddBbQMediator(typeof(Program).Assembly);

// These execute in this order:
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));      // 1st (outermost)
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));   // 2nd
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));  // 3rd
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));      // 4th (innermost)
// Then the handler executes at the terminal
```

---

## 🧪 Unit Testing

Use `TestMediator` and `StubHandler` for isolated handler testing:

### Test a Handler

```csharp
[TestFixture]
public class CreateUserCommandHandlerTests
{
    private Mock<IUserRepository> _mockRepository;
    private CreateUserCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _mockRepository = new Mock<IUserRepository>();
        _handler = new CreateUserCommandHandler(_mockRepository.Object);
    }

    [Test]
    public async Task Handle_WithValidRequest_CreatesUser()
    {
        // Arrange
        var command = new CreateUserCommand { Email = "test@example.com", Name = "Test" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
```

### Test a Behavior

```csharp
[TestFixture]
public class ValidationBehaviorTests
{
    [Test]
    public async Task Handle_WithInvalidRequest_ShortCircuits()
    {
        // Arrange
        var request = new CreateUserCommand { Email = "invalid" };
        var handler = new StubHandler<CreateUserCommand, Outcome<User>>(
            async (req, ct) => throw new InvalidOperationException("Should not reach handler")
        );

        var mockValidator = new Mock<IValidator<CreateUserCommand>>();
        mockValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(
                new[] { new ValidationFailure("Email", "Invalid") }));

        var behavior = new ValidationBehavior<CreateUserCommand, Outcome<User>>(mockValidator.Object);
        var mediator = new TestMediator<CreateUserCommand, Outcome<User>>(
            handler, 
            new[] { behavior });

        // Act & Assert
        Assert.ThrowsAsync<ValidationException>(async () => 
            await mediator.Send(request, CancellationToken.None));
    }
}
```

---

## 🔗 Integration with Outcome

Commands and queries typically return `Outcome<T>` for comprehensive error handling:

```csharp
// Success case
public class GetUserByIdQuery : IQuery<Outcome<User>>
{
    public Guid UserId { get; set; }
}

// In handler
public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Outcome<User>>
{
    public async Task<Outcome<User>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _repository.GetByIdAsync(request.UserId, ct);
        
        if (user == null)
        {
            return UserErrorsErrors.NotFoundError.ToOutcome<User>();
        }

        return Outcome<User>.From(user);
    }
}
```

---

## 📚 Learn More

- [BbQ.Outcome Documentation](../Outcome/README.md) - Functional error handling
- [Strongly Typed Errors Guide](../../STRONGLY_TYPED_ERRORS.md) - Best practices
