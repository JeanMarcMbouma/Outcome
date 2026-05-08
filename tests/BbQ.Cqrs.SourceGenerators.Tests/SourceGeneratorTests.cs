using BbQ.Cqrs;
using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Crs.SourceGenerators.Tests
{

    record Query : IQuery<int>;

    internal class QueryHandler : IQueryHandler<Query, int>
    {
        public Task<int> Handle(Query request, CancellationToken cancellationToken)
        {
            return Task.FromResult(42);
        }
    }

    record Command: ICommand;
    internal class CommandHandler : ICommandHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    record CommandWithResult : ICommand<string>;
    internal class CommandWithResultHandler : ICommandHandler<CommandWithResult, string>
    {
        public Task<string> Handle(CommandWithResult request, CancellationToken cancellationToken)
        {
            return Task.FromResult("Hello, World!");
        }
    }

    public class SourceGeneratorTests
    {

        [Test]
        public async Task TestQueryHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddBbQMediator();
            services.AddBbQCqrsSourceGeneratorsTestsCqrs();

            using var sp = services.BuildServiceProvider();

            var mediator = sp.GetRequiredService<IQueryDispatcher>();
            // Act
            var result = await mediator.Dispatch(new Query());
            // Assert
            Assert.That(result, Is.EqualTo(42));
        }

        [Test]
        public async Task TestCommandHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddBbQMediator();
            services.AddBbQCqrsSourceGeneratorsTestsCqrs();

            using var sp = services.BuildServiceProvider();
            var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();
            // Act & Assert - should not throw
            await commandDispatcher.Dispatch(new Command());
        }

        [Test]
        public async Task TestCommandWithResultHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddBbQMediator();
            services.AddBbQCqrsSourceGeneratorsTestsCqrs();
            using var sp = services.BuildServiceProvider();
            var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();
            // Act
            var result = await commandDispatcher.Dispatch(new CommandWithResult());
            // Assert
            Assert.That(result, Is.EqualTo("Hello, World!"));
        }



        [Test]
        public void TestSourceGeneratorRegistration()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddBbQMediator();
            services.AddBbQCqrsSourceGeneratorsTestsCqrs();
            using var sp = services.BuildServiceProvider();
            // Act & Assert - verify handlers are registered
            Assert.That(sp.GetService<IRequestHandler<Query, int>>(), Is.Not.Null);
            Assert.That(sp.GetService<IRequestHandler<Command>>(), Is.Not.Null);
            Assert.That(sp.GetService<IRequestHandler<CommandWithResult, string>>(), Is.Not.Null);
        }

        // test that all types can be called via Mediator without throwing exceptions
        [Test]
        public async Task TestMediatorIntegration()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddBbQMediator(typeof(SourceGeneratorTests).Assembly);
            using var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();
            // Act & Assert - should not throw
            var queryResult = await mediator.Send(new Query());
            Assert.That(queryResult, Is.EqualTo(42));
            await mediator.Send(new Command());
            var commandWithResult = await mediator.Send(new CommandWithResult());
            Assert.That(commandWithResult, Is.EqualTo("Hello, World!"));
        }
    }
}
