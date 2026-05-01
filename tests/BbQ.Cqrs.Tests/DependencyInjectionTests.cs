using BbQ.Cqrs.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BbQ.Cqrs.Tests
{
    public class DependencyInjectionTests
    {
        [Test]
        public void ServiceCollectionExtensions_AddBbQMediator_RegistersMediatorAndAliases()
        {
            // Arrange
            var services = new ServiceCollection();
            // Act
            services.AddBbQMediator();
            var serviceProvider = services.BuildServiceProvider();
            // Assert
            var mediator = serviceProvider.GetService<IMediator>();
            var sender = serviceProvider.GetService<ISender>();
            var streamer = serviceProvider.GetService<IStreamer>();
            Assert.Multiple(() =>
            {
                Assert.That(mediator, Is.Not.Null, "IMediator should be registered");
                Assert.That(sender, Is.Not.Null, "ISender should be registered");
                Assert.That(streamer, Is.Not.Null, "IStreamer should be registered");
            });

            Assert.That(mediator, Is.SameAs(sender));
            Assert.That(mediator, Is.SameAs(streamer));
        }
    }
}
