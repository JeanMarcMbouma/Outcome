# BbQ.Cqrs

A lightweight, extensible CQRS (Command Query Responsibility Segregation) implementation that integrates seamlessly with `Outcome` for comprehensive error handling.

## ✨ Features

- **Type-safe mediator** for commands and queries with compile-time checking
- **Pipeline behaviors** for cross-cutting concerns (logging, validation, caching)
- **Built-in logging behavior** to track all request/response flows
- **Test utilities** with `TestMediator` and `StubHandler` for isolated testing
- **Comprehensive documentation** on all interfaces and classes with XML comments
- **Seamless integration** with `Outcome<T>` for advanced error management

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
- Scans assemblies for all `IRequestHandler<,>` implementations
- Registers handlers with scoped lifetime
- Registers the built-in `LoggingBehavior`

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

```csharp
Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
    where TRequest : IRequest<TResponse>;
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

### IRequestHandler<TRequest, TResponse>
Implements the actual logic for handling a request.

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

## 🧭 Pipeline Behaviors

Behaviors execute in registration order: **first registered = outermost = executes first**.

### Built-in: LoggingBehavior

Automatically registered by `AddBbQMediator()`:

```csharp
services.AddBbQMediator(typeof(Program).Assembly);
// LoggingBehavior is already included
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
