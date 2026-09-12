using BbQ.Outcome.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace BbQ.Outcome.SourceGenerators.Tests;

[TestFixture]
public sealed class OutcomeGeneratorDiagnosticTests
{
    private static (GeneratorDriverRunResult Run, Compilation Output) Generate(string source, DocumentationMode documentationMode = DocumentationMode.Parse)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, documentationMode: documentationMode);
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Outcome<int>).Assembly.Location).Distinct(StringComparer.Ordinal);
        var references = paths.Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("GeneratorFixture", new[] { CSharpSyntaxTree.ParseText(source, options) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { new OutcomeSourceGenerator().AsSourceGenerator() }, parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return (driver.GetRunResult(), output);
    }

    [TestCase("""[ErrorCode("DUP")] First, [ErrorCode("DUP")] Second""")]
    [TestCase("""[ErrorCode("")] First""")]
    [TestCase("""[ErrorResourceKey(" ")] First""")]
    [TestCase("First = 1, Alias = 1")]
    public void InvalidDefinitionsProduceAnActionableDiagnostic(string members)
    {
        var (run, _) = Generate("using BbQ.Outcome; namespace Fixture; [QbqOutcome] public enum Code { " + members + " }");
        Assert.That(run.Diagnostics.Count(diagnostic => diagnostic.Id == "BBQOUT001"), Is.EqualTo(1));
        Assert.That(run.Results.Single().Exception, Is.Null);
        Assert.That(run.Results.Single().GeneratedSources, Is.Empty);
    }

    [TestCase("public class Container<T> { [QbqOutcome] public enum Code { First } }")]
    [TestCase("public class Container { [QbqOutcome] private enum Code { First } }")]
    public void UnsupportedContainingTypesProduceDiagnostics(string definition)
    {
        var (run, _) = Generate("using BbQ.Outcome; namespace Fixture; " + definition);
        Assert.That(run.Diagnostics.Any(diagnostic => diagnostic.Id == "BBQOUT001"), Is.True);
        Assert.That(run.Results.Single().Exception, Is.Null);
    }

    [Test]
    public void GlobalNamespaceNestedTypesAndAttributeAliasesGenerateCompilableCode()
    {
        var (run, output) = Generate("""
            using BbQ.Outcome;
            using Mark = BbQ.Outcome.QbqOutcomeAttribute;
            [Mark] public enum GlobalCode { @event }
            namespace Outer.Inner {
                public class Container {
                    [Mark] public enum Code { First }
                }
                public static class Messages { public const string Text = "A \"quoted\" error"; }
                [Mark] public enum Described {
                    [System.ComponentModel.Description(Messages.Text)] First
                }
            }
            """);
        Assert.That(run.Results.Single().GeneratedSources.Length, Is.EqualTo(3));
        Assert.That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(string.Join("\n", run.Results.Single().GeneratedSources.Select(source => source.SourceText.ToString())),
            Does.Contain("Container_CodeErrors").And.Contain("quoted"));
    }

    [Test]
    public void EqualShortNamesUseDistinctHintNamesAndFullyQualifiedSymbols()
    {
        var (run, output) = Generate("""
            using BbQ.Outcome;
            namespace One { [QbqOutcome] public enum Status { First } }
            namespace Two { [QbqOutcome] public enum Status { First } }
            """);
        var sources = run.Results.Single().GeneratedSources;
        Assert.That(sources.Select(source => source.HintName).Distinct().Count(), Is.EqualTo(2));
        Assert.That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [TestCase(DocumentationMode.Parse)]
    [TestCase(DocumentationMode.None)]
    public void XmlDescriptionsSurviveWithOrWithoutDocumentationEmission(DocumentationMode mode)
    {
        var (run, output) = Generate("""
            using BbQ.Outcome;
            namespace Fixture;
            [QbqOutcome] public enum Code {
                /// <summary>A documented &amp; escaped error.</summary>
                First
            }
            """, mode);
        Assert.That(run.Results.Single().GeneratedSources.Single().SourceText.ToString(), Does.Contain("A documented & escaped error."));
        Assert.That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
    }
}
