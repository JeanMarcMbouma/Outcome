using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BbQ.Cqrs.SourceGenerators
{
    /// <summary>
    /// Incremental source generator that detects CQRS handlers and behaviors
    /// and generates registration code.
    /// </summary>
    [Generator]
    public class HandlerRegistrationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register syntax provider for all handlers (commands, queries, and fire-and-forget)
            var handlers = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsHandlerCandidate(s),
                    transform: static (ctx, _) => GetHandlerInfo(ctx))
                .Where(static m => m is not null);

            // Register syntax provider for behaviors with [Behavior] attribute
            var behaviors = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsBehaviorCandidate(s),
                    transform: static (ctx, _) => GetBehaviorInfo(ctx))
                .Where(static m => m is not null);

            // Collect all handlers and behaviors
            var allHandlers = handlers.Collect();
            var allBehaviors = behaviors.Collect();
            
            // Get compilation to access assembly name
            var compilation = context.CompilationProvider;

            // Generate registration extension methods with assembly name
            context.RegisterSourceOutput(
                compilation.Combine(allHandlers.Combine(allBehaviors)),
                static (spc, data) =>
                {
                    var (compilation, handlersAndBehaviors) = data;
                    var (handlers, behaviors) = handlersAndBehaviors;
                    GenerateRegistrationExtensions(spc, compilation, handlers, behaviors);
                });
        }

        private static bool IsHandlerCandidate(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   !classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                   classDecl.BaseList != null;
        }

        private static bool IsBehaviorCandidate(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   !classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                   classDecl.AttributeLists.Count > 0;
        }

        private static HandlerInfo? GetHandlerInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                return null;

            // Check if this class implements IRequestHandler<,>, IRequestHandler<>, or IStreamHandler<,>
            var interfaces = classSymbol.AllInterfaces;
            
            foreach (var iface in interfaces)
            {
                var interfaceName = iface.ToDisplayString();
                
                // Check for IStreamHandler<TRequest, TItem>
                if (interfaceName.StartsWith("BbQ.Cqrs.IStreamHandler<") && iface.TypeArguments.Length == 2)
                {
                    var requestType = iface.TypeArguments[0];
                    var itemType = iface.TypeArguments[1];
                    
                    // Check if request implements IStreamQuery
                    var requestInterfaces = requestType.AllInterfaces;
                    bool isStreamQuery = requestInterfaces.Any(i => i.ToDisplayString().StartsWith("BbQ.Cqrs.IStreamQuery<"));
                    
                    if (isStreamQuery)
                    {
                        return new HandlerInfo
                        {
                            HandlerTypeName = classSymbol.ToDisplayString(),
                            RequestTypeName = requestType.ToDisplayString(),
                            ResponseTypeName = itemType.ToDisplayString(),
                            IsStreamHandler = true,
                            IsQuery = true,
                            Namespace = classSymbol.ContainingNamespace.ToDisplayString()
                        };
                    }
                }
                // Check for IRequestHandler<TRequest, TResponse>
                else if (interfaceName.StartsWith("BbQ.Cqrs.IRequestHandler<") && iface.TypeArguments.Length == 2)
                {
                    var requestType = iface.TypeArguments[0];
                    var responseType = iface.TypeArguments[1];
                    
                    // Check if request implements ICommand or IQuery
                    var requestInterfaces = requestType.AllInterfaces;
                    bool isCommand = requestInterfaces.Any(i => i.ToDisplayString().StartsWith("BbQ.Cqrs.ICommand<"));
                    bool isQuery = requestInterfaces.Any(i => i.ToDisplayString().StartsWith("BbQ.Cqrs.IQuery<"));
                    
                    if (isCommand || isQuery)
                    {
                        return new HandlerInfo
                        {
                            HandlerTypeName = classSymbol.ToDisplayString(),
                            RequestTypeName = requestType.ToDisplayString(),
                            ResponseTypeName = responseType.ToDisplayString(),
                            IsCommand = isCommand,
                            IsQuery = isQuery,
                            Namespace = classSymbol.ContainingNamespace.ToDisplayString()
                        };
                    }
                }
                // Check for IRequestHandler<TRequest> (fire-and-forget)
                else if (interfaceName.StartsWith("BbQ.Cqrs.IRequestHandler<") && iface.TypeArguments.Length == 1)
                {
                    var requestType = iface.TypeArguments[0];
                    
                    // Check if request implements ICommand (fire-and-forget commands)
                    var requestInterfaces = requestType.AllInterfaces;
                    bool hasIRequest = requestInterfaces.Any(i => i.ToDisplayString() == "BbQ.Cqrs.IRequest");
                    
                    if (hasIRequest)
                    {
                        return new HandlerInfo
                        {
                            HandlerTypeName = classSymbol.ToDisplayString(),
                            RequestTypeName = requestType.ToDisplayString(),
                            ResponseTypeName = "BbQ.Cqrs.Unit",
                            IsCommand = true,
                            IsQuery = false,
                            IsFireAndForget = true,
                            Namespace = classSymbol.ContainingNamespace.ToDisplayString()
                        };
                    }
                }
            }

            return null;
        }

        private static BehaviorInfo? GetBehaviorInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                return null;

            // Check for [Behavior] attribute
            var attributes = classSymbol.GetAttributes();
            var behaviorAttr = attributes.FirstOrDefault(a => 
                a.AttributeClass?.ToDisplayString() == "BbQ.Cqrs.BehaviorAttribute");

            if (behaviorAttr == null)
                return null;

            // Check if this class implements IPipelineBehavior<,> or IStreamPipelineBehavior<,>
            var interfaces = classSymbol.AllInterfaces;
            var pipelineInterface = interfaces.FirstOrDefault(i => 
                i.ToDisplayString().StartsWith("BbQ.Cqrs.IPipelineBehavior<"));
            var streamPipelineInterface = interfaces.FirstOrDefault(i => 
                i.ToDisplayString().StartsWith("BbQ.Cqrs.IStreamPipelineBehavior<"));

            if (pipelineInterface == null && streamPipelineInterface == null)
                return null;

            bool isStreamBehavior = streamPipelineInterface != null;

            // Extract Order property from attribute
            int order = 0;
            var orderArg = behaviorAttr.NamedArguments.FirstOrDefault(a => a.Key == "Order");
            if (orderArg.Key == "Order" && orderArg.Value.Value is int orderValue)
            {
                order = orderValue;
            }

            // Determine if the behavior is generic
            bool isGeneric = classSymbol.IsGenericType;
            string behaviorTypeName;
            
            if (isGeneric)
            {
                // For generic types, we need the open generic type definition
                // e.g., "BbQ.Cqrs.LoggingBehavior<,>" instead of "BbQ.Cqrs.LoggingBehavior<TRequest, TResponse>"
                var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
                var typeName = classSymbol.Name;
                var arity = classSymbol.Arity;
                var typeParams = string.Join(",", Enumerable.Repeat(string.Empty, arity));
                behaviorTypeName = $"{namespaceName}.{typeName}<{typeParams}>";
            }
            else
            {
                behaviorTypeName = classSymbol.ToDisplayString();
            }

            return new BehaviorInfo
            {
                BehaviorTypeName = behaviorTypeName,
                Order = order,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                IsGeneric = isGeneric,
                IsStreamBehavior = isStreamBehavior
            };
        }

        private static void GenerateRegistrationExtensions(
            SourceProductionContext context,
            Compilation compilation,
            IEnumerable<HandlerInfo?> handlers,
            IEnumerable<BehaviorInfo?> behaviors)
        {
            var validHandlers = handlers.Where(h => h != null).Cast<HandlerInfo>().ToList();
            var validBehaviors = behaviors.Where(b => b != null).Cast<BehaviorInfo>()
                .OrderBy(b => b.Order).ToList();

            if (validHandlers.Count == 0 && validBehaviors.Count == 0)
                return;

            // Create a unique class name based on assembly name
            var assemblyName = compilation.AssemblyName ?? "Unknown";
            var safeName = string.Concat(assemblyName.Where(c => char.IsLetterOrDigit(c) || c == '_'));
            var className = $"Generated{safeName}RegistrationExtensions";

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using BbQ.Cqrs;");
            sb.AppendLine();
            sb.AppendLine("namespace BbQ.Cqrs.DependencyInjection");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Auto-generated extension methods for registering CQRS handlers and behaviors in {assemblyName}.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static class {className}");
            sb.AppendLine("    {");

            // Generate AddGeneratedHandlers method with lifetime parameter
            if (validHandlers.Count > 0)
            {
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Registers all auto-detected command handlers and query handlers from {assemblyName}.");
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("        /// Also registers IMediator as singleton if not already registered.");
                }
                else
                {
                    sb.AppendLine("        /// Note: You must call AddBbQMediator() before calling this method to register IMediator.");
                }
                sb.AppendLine("        /// </summary>");
                sb.AppendLine("        /// <param name=\"services\">The service collection</param>");
                sb.AppendLine("        /// <param name=\"handlersLifetime\">The lifetime to use for handler registrations (default: Scoped)</param>");
                sb.AppendLine("        /// <returns>The service collection for chaining</returns>");
                sb.AppendLine($"        public static IServiceCollection Add{safeName}Handlers(this IServiceCollection services, ServiceLifetime handlersLifetime = ServiceLifetime.Scoped)");
                sb.AppendLine("        {");
                
                // Only register IMediator if we're in BbQ.Cqrs assembly (where Mediator is accessible)
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("            // Register IMediator as singleton if not already registered");
                    sb.AppendLine("            services.TryAddSingleton<IMediator, Mediator>();");
                    sb.AppendLine();
                }

                foreach (var handler in validHandlers)
                {
                    if (handler.IsStreamHandler)
                    {
                        sb.AppendLine($"            // Register stream handler: {handler.HandlerTypeName}");
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IStreamHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>), typeof({handler.HandlerTypeName}), handlersLifetime));");
                    }
                    else if (handler.IsFireAndForget)
                    {
                        sb.AppendLine($"            // Register fire-and-forget handler: {handler.HandlerTypeName}");
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IRequestHandler<{handler.RequestTypeName}>), typeof({handler.HandlerTypeName}), handlersLifetime));");
                    }
                    else
                    {
                        sb.AppendLine($"            // Register {(handler.IsCommand ? "command" : "query")} handler: {handler.HandlerTypeName}");
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IRequestHandler<{handler.RequestTypeName}, {handler.ResponseTypeName}>), typeof({handler.HandlerTypeName}), handlersLifetime));");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("            return services;");
                sb.AppendLine("        }");
            }

            // Generate AddGeneratedBehaviors method
            if (validBehaviors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Registers all behaviors marked with [Behavior] attribute from {assemblyName} in order.");
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("        /// Also registers IMediator as singleton if not already registered.");
                }
                else
                {
                    sb.AppendLine("        /// Note: You must call AddBbQMediator() before calling this method to register IMediator.");
                }
                sb.AppendLine("        /// </summary>");
                sb.AppendLine("        /// <param name=\"services\">The service collection</param>");
                sb.AppendLine("        /// <param name=\"behaviorsLifetime\">The lifetime to use for behavior registrations (default: Scoped)</param>");
                sb.AppendLine("        /// <returns>The service collection for chaining</returns>");
                sb.AppendLine($"        public static IServiceCollection Add{safeName}Behaviors(this IServiceCollection services, ServiceLifetime behaviorsLifetime = ServiceLifetime.Scoped)");
                sb.AppendLine("        {");
                
                // Only register IMediator if we're in BbQ.Cqrs assembly (where Mediator is accessible)
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("            // Register IMediator as singleton if not already registered");
                    sb.AppendLine("            services.TryAddSingleton<IMediator, Mediator>();");
                    sb.AppendLine();
                }

                foreach (var behavior in validBehaviors)
                {
                    if (behavior.IsStreamBehavior)
                    {
                        sb.AppendLine($"            // Register stream behavior (Order = {behavior.Order}): {behavior.BehaviorTypeName}");
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IStreamPipelineBehavior<,>), typeof({behavior.BehaviorTypeName}), behaviorsLifetime));");
                    }
                    else
                    {
                        sb.AppendLine($"            // Register behavior (Order = {behavior.Order}): {behavior.BehaviorTypeName}");
                        sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof(IPipelineBehavior<,>), typeof({behavior.BehaviorTypeName}), behaviorsLifetime));");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("            return services;");
                sb.AppendLine("        }");
            }

            // Generate AddGeneratedCqrs method that combines both
            if (validHandlers.Count > 0 || validBehaviors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Registers all auto-detected handlers and behaviors from {assemblyName}.");
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("        /// Also registers IMediator as singleton if not already registered.");
                }
                else
                {
                    sb.AppendLine("        /// Note: You must call AddBbQMediator() before calling this method to register IMediator.");
                }
                sb.AppendLine("        /// </summary>");
                sb.AppendLine("        /// <param name=\"services\">The service collection</param>");
                sb.AppendLine("        /// <param name=\"handlersLifetime\">The lifetime to use for handler registrations (default: Scoped)</param>");
                sb.AppendLine("        /// <param name=\"behaviorsLifetime\">The lifetime to use for behavior registrations (default: Scoped)</param>");
                sb.AppendLine("        /// <returns>The service collection for chaining</returns>");
                sb.AppendLine($"        public static IServiceCollection Add{safeName}Cqrs(this IServiceCollection services, ServiceLifetime handlersLifetime = ServiceLifetime.Scoped, ServiceLifetime behaviorsLifetime = ServiceLifetime.Scoped)");
                sb.AppendLine("        {");
                
                // Only register IMediator if we're in BbQ.Cqrs assembly (where Mediator is accessible)
                if (assemblyName == "BbQ.Cqrs")
                {
                    sb.AppendLine("            // Register IMediator as singleton if not already registered");
                    sb.AppendLine("            services.TryAddSingleton<IMediator, Mediator>();");
                    sb.AppendLine();
                }
                
                if (validHandlers.Count > 0)
                {
                    sb.AppendLine($"            services.Add{safeName}Handlers(handlersLifetime);");
                }
                
                if (validBehaviors.Count > 0)
                {
                    sb.AppendLine($"            services.Add{safeName}Behaviors(behaviorsLifetime);");
                }
                
                sb.AppendLine();
                sb.AppendLine("            return services;");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource($"{className}.g.cs", sb.ToString());
        }

        private class HandlerInfo
        {
            public string HandlerTypeName { get; set; } = string.Empty;
            public string RequestTypeName { get; set; } = string.Empty;
            public string ResponseTypeName { get; set; } = string.Empty;
            public bool IsCommand { get; set; }
            public bool IsQuery { get; set; }
            public bool IsFireAndForget { get; set; }
            public bool IsStreamHandler { get; set; }
            public string Namespace { get; set; } = string.Empty;
        }

        private class BehaviorInfo
        {
            public string BehaviorTypeName { get; set; } = string.Empty;
            public int Order { get; set; }
            public string Namespace { get; set; } = string.Empty;
            public bool IsGeneric { get; set; }
            public bool IsStreamBehavior { get; set; }
        }
    }
}
