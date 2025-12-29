using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BbQ.Events.SourceGenerators
{
    /// <summary>
    /// Incremental source generator that detects event handlers, subscribers, and projections
    /// and generates registration code.
    /// </summary>
    [Generator]
    public class EventHandlerRegistrationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register syntax provider for event handlers and subscribers
            var handlers = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsEventHandlerCandidate(s),
                    transform: static (ctx, _) => GetEventHandlerInfo(ctx))
                .Where(static m => m is not null);

            // Register syntax provider for projections
            var projections = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsProjectionCandidate(s),
                    transform: static (ctx, _) => GetProjectionInfo(ctx))
                .Where(static m => m is not null);

            // Collect all handlers and projections
            var allHandlers = handlers.Collect();
            var allProjections = projections.Collect();
            
            // Get compilation to access assembly name
            var compilation = context.CompilationProvider;

            // Generate registration extension methods with assembly name
            context.RegisterSourceOutput(
                compilation.Combine(allHandlers.Combine(allProjections)),
                static (spc, data) =>
                {
                    var (compilation, handlersAndProjections) = data;
                    var (handlers, projections) = handlersAndProjections;
                    GenerateRegistrationExtensions(spc, compilation, handlers, projections);
                });
        }

        private static bool IsEventHandlerCandidate(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   !classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                   classDecl.BaseList != null;
        }

        private static bool IsProjectionCandidate(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   !classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                   classDecl.AttributeLists.Count > 0 &&
                   classDecl.BaseList != null;
        }

        private static EventHandlerInfo? GetEventHandlerInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                return null;

            // Check if this class implements IEventHandler<TEvent> or IEventSubscriber<TEvent>
            var interfaces = classSymbol.AllInterfaces;
            
            foreach (var iface in interfaces)
            {
                var interfaceName = iface.ToDisplayString();
                
                // Check for IEventHandler<TEvent>
                if (interfaceName.StartsWith("BbQ.Events.IEventHandler<") && iface.TypeArguments.Length == 1)
                {
                    var eventType = iface.TypeArguments[0];
                    
                    return new EventHandlerInfo
                    {
                        HandlerTypeName = classSymbol.ToDisplayString(),
                        EventTypeName = eventType.ToDisplayString(),
                        IsEventHandler = true,
                        Namespace = classSymbol.ContainingNamespace.ToDisplayString()
                    };
                }
                // Check for IEventSubscriber<TEvent>
                else if (interfaceName.StartsWith("BbQ.Events.IEventSubscriber<") && iface.TypeArguments.Length == 1)
                {
                    var eventType = iface.TypeArguments[0];
                    
                    return new EventHandlerInfo
                    {
                        HandlerTypeName = classSymbol.ToDisplayString(),
                        EventTypeName = eventType.ToDisplayString(),
                        IsEventSubscriber = true,
                        Namespace = classSymbol.ContainingNamespace.ToDisplayString()
                    };
                }
            }

            return null;
        }

        private static ProjectionInfo? GetProjectionInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                return null;

            // Check for [Projection] attribute
            var attributes = classSymbol.GetAttributes();
            var hasProjectionAttr = attributes.Any(a => 
                a.AttributeClass?.ToDisplayString() == "BbQ.Events.ProjectionAttribute");

            if (!hasProjectionAttr)
                return null;

            // Collect all event types this projection handles
            var interfaces = classSymbol.AllInterfaces;
            var eventTypes = new List<EventTypeInfo>();
            
            foreach (var iface in interfaces)
            {
                var interfaceName = iface.ToDisplayString();
                
                // Check for IProjectionHandler<TEvent>
                if (interfaceName.StartsWith("BbQ.Events.IProjectionHandler<") && iface.TypeArguments.Length == 1)
                {
                    var eventType = iface.TypeArguments[0];
                    eventTypes.Add(new EventTypeInfo
                    {
                        EventTypeName = eventType.ToDisplayString(),
                        IsPartitioned = false
                    });
                }
                // Check for IPartitionedProjectionHandler<TEvent>
                else if (interfaceName.StartsWith("BbQ.Events.IPartitionedProjectionHandler<") && iface.TypeArguments.Length == 1)
                {
                    var eventType = iface.TypeArguments[0];
                    eventTypes.Add(new EventTypeInfo
                    {
                        EventTypeName = eventType.ToDisplayString(),
                        IsPartitioned = true
                    });
                }
            }

            if (eventTypes.Count == 0)
                return null;

            return new ProjectionInfo
            {
                ProjectionTypeName = classSymbol.ToDisplayString(),
                EventTypes = eventTypes,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString()
            };
        }

        private static void GenerateRegistrationExtensions(
            SourceProductionContext context,
            Compilation compilation,
            IEnumerable<EventHandlerInfo?> handlers,
            IEnumerable<ProjectionInfo?> projections)
        {
            var validHandlers = handlers.Where(h => h != null).Cast<EventHandlerInfo>().ToList();
            var validProjections = projections.Where(p => p != null).Cast<ProjectionInfo>().ToList();

            if (validHandlers.Count == 0 && validProjections.Count == 0)
                return;

            // Create a unique class name based on assembly name
            var assemblyName = compilation.AssemblyName ?? "Unknown";
            var safeName = string.Concat(assemblyName.Where(c => char.IsLetterOrDigit(c) || c == '_'));
            var className = $"Generated{safeName}EventRegistrationExtensions";

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using BbQ.Events;");
            sb.AppendLine();
            sb.AppendLine("namespace BbQ.Events.DependencyInjection");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Auto-generated extension methods for registering event handlers and subscribers in {assemblyName}.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Registers all event handlers and subscribers discovered in {assemblyName}.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"services\">The service collection to register with</param>");
            sb.AppendLine("        /// <param name=\"handlersLifetime\">The lifetime to use for handler instances (default: Scoped)</param>");
            sb.AppendLine("        /// <returns>The service collection for chaining</returns>");
            sb.AppendLine("        public static IServiceCollection Add" + safeName + "EventHandlers(");
            sb.AppendLine("            this IServiceCollection services,");
            sb.AppendLine("            ServiceLifetime handlersLifetime = ServiceLifetime.Scoped)");
            sb.AppendLine("        {");
            
            // Register each handler
            foreach (var handler in validHandlers)
            {
                if (handler.IsEventHandler)
                {
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IEventHandler<{handler.EventTypeName}>), typeof({handler.HandlerTypeName}), handlersLifetime));");
                }
                else if (handler.IsEventSubscriber)
                {
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IEventSubscriber<{handler.EventTypeName}>), typeof({handler.HandlerTypeName}), handlersLifetime));");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            
            // Generate AddProjections method if there are projections
            if (validProjections.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Registers all projections discovered in {assemblyName}.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine("        /// <param name=\"services\">The service collection to register with</param>");
                sb.AppendLine("        /// <param name=\"handlersLifetime\">The lifetime to use for projection instances (default: Scoped)</param>");
                sb.AppendLine("        /// <returns>The service collection for chaining</returns>");
                sb.AppendLine("        public static IServiceCollection Add" + safeName + "Projections(");
                sb.AppendLine("            this IServiceCollection services,");
                sb.AppendLine("            ServiceLifetime handlersLifetime = ServiceLifetime.Scoped)");
                sb.AppendLine("        {");
                
                // Register each projection
                foreach (var projection in validProjections)
                {
                    sb.AppendLine($"            // Register projection: {projection.ProjectionTypeName}");
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({projection.ProjectionTypeName}), typeof({projection.ProjectionTypeName}), handlersLifetime));");
                    
                    // Register for each event type it handles
                    foreach (var eventType in projection.EventTypes)
                    {
                        if (eventType.IsPartitioned)
                        {
                            sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IPartitionedProjectionHandler<{eventType.EventTypeName}>), sp => sp.GetRequiredService<{projection.ProjectionTypeName}>(), handlersLifetime));");
                            sb.AppendLine($"            ProjectionHandlerRegistry.Register(typeof({eventType.EventTypeName}), typeof(IPartitionedProjectionHandler<{eventType.EventTypeName}>));");
                        }
                        else
                        {
                            sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IProjectionHandler<{eventType.EventTypeName}>), sp => sp.GetRequiredService<{projection.ProjectionTypeName}>(), handlersLifetime));");
                            sb.AppendLine($"            ProjectionHandlerRegistry.Register(typeof({eventType.EventTypeName}), typeof(IProjectionHandler<{eventType.EventTypeName}>));");
                        }
                    }
                    sb.AppendLine();
                }
                
                sb.AppendLine("            return services;");
                sb.AppendLine("        }");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource($"{className}.g.cs", sb.ToString());
        }

        private class EventHandlerInfo
        {
            public string HandlerTypeName { get; set; } = "";
            public string EventTypeName { get; set; } = "";
            public bool IsEventHandler { get; set; }
            public bool IsEventSubscriber { get; set; }
            public string Namespace { get; set; } = "";
        }

        private class ProjectionInfo
        {
            public string ProjectionTypeName { get; set; } = "";
            public List<EventTypeInfo> EventTypes { get; set; } = new();
            public string Namespace { get; set; } = "";
        }

        private class EventTypeInfo
        {
            public string EventTypeName { get; set; } = "";
            public bool IsPartitioned { get; set; }
        }
    }
}
