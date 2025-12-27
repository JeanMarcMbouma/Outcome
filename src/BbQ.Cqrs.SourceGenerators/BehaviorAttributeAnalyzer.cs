using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace BbQ.Cqrs.SourceGenerators
{
    /// <summary>
    /// Analyzer that reports a diagnostic when [Behavior] attribute is applied to a class
    /// with more than 2 type parameters.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BehaviorAttributeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "BBQCQRS001";

        private static readonly LocalizableString Title = "Behavior attribute on class with incompatible type parameter count";
        private static readonly LocalizableString MessageFormat = "The [Behavior] attribute cannot be used on '{0}' because it has {1} type parameters. Behaviors must have exactly 2 type parameters to match IPipelineBehavior<TRequest, TResponse>.";
        private static readonly LocalizableString Description = "The [Behavior] attribute can only be used on classes with exactly 2 type parameters that implement IPipelineBehavior<TRequest, TResponse>. Classes with additional type parameters cannot be automatically registered and must be registered manually.";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            
            // Check if the class has any attributes
            if (classDeclaration.AttributeLists.Count == 0)
                return;

            var semanticModel = context.SemanticModel;
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            
            if (classSymbol == null)
                return;

            // Check if the class has [Behavior] attribute
            var hasBehaviorAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "BbQ.Cqrs.BehaviorAttribute");

            if (!hasBehaviorAttribute)
                return;

            // Check if the class implements IPipelineBehavior
            var implementsPipelineBehavior = classSymbol.AllInterfaces.Any(i =>
                i.ToDisplayString().StartsWith("BbQ.Cqrs.IPipelineBehavior<"));

            if (!implementsPipelineBehavior)
                return;

            // Check the number of type parameters
            var typeParameterCount = classSymbol.TypeParameters.Length;

            if (typeParameterCount != 2)
            {
                // Find the attribute syntax node to report the diagnostic at the right location
                var attributeSyntax = classDeclaration.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(attr =>
                    {
                        var attrSymbol = semanticModel.GetSymbolInfo(attr).Symbol?.ContainingType;
                        return attrSymbol?.ToDisplayString() == "BbQ.Cqrs.BehaviorAttribute";
                    });

                var location = attributeSyntax?.GetLocation() ?? classDeclaration.Identifier.GetLocation();

                var diagnostic = Diagnostic.Create(
                    Rule,
                    location,
                    classSymbol.Name,
                    typeParameterCount);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
