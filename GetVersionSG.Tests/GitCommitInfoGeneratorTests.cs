using GetVersionSG;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using Xunit;

namespace GetVersionSG.Tests;

public sealed class GitCommitInfoGeneratorTests
{
    [Fact]
    public void SourceWriterEmitsAllMetadataAsConstants()
    {
        var metadata = new GitCommitInfoGenerator.GitMetadata(
            "abc123",
            "v1.2.3",
            "7",
            "feature/test");

        var source = GitCommitInfoGenerator.SourceWriter.Write("Demo", "BuildInfo", metadata);

        Assert.Contains("public static class BuildInfo", source);
        Assert.Contains("public const string CommitHash = \"abc123\";", source);
        Assert.Contains("public const string Tag = \"v1.2.3\";", source);
        Assert.Contains("public const string Distance = \"7\";", source);
        Assert.Contains("public const string BranchName = \"feature/test\";", source);
    }

    [Fact]
    public void SourceWriterEscapesCSharpStringValues()
    {
        var metadata = new GitCommitInfoGenerator.GitMetadata(
            "hash\"with\\characters",
            "",
            "0",
            "HEAD");

        var source = GitCommitInfoGenerator.SourceWriter.Write("Demo", "BuildInfo", metadata);

        Assert.Contains("public const string CommitHash = \"hash\\\"with\\\\characters\";", source);
    }

    [Fact]
    public void GeneratorReadsCommitInfoNamespaceAndClassName()
    {
        var additionalText = new TestAdditionalText(
            Path.Combine(Environment.CurrentDirectory, "BuildInfo.commit-info"),
            "Demo.Generated");
        var compilation = CSharpCompilation.Create(
            "Demo",
            new[]
            {
                CSharpSyntaxTree.ParseText("""
                    using GetVersionSG;

                    public static class CustomValues
                    {
                        [CommitInfoGetter]
                        public static string Version() => Tag + "." + Distance;
                    }
                    """)
            });
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GitCommitInfoGenerator())
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));

        driver = driver.RunGenerators(compilation);

        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "BuildInfo.g.cs").SourceText.ToString();
        Assert.Contains("namespace Demo.Generated;", generated);
        Assert.Contains("public static class BuildInfo", generated);
        Assert.Contains("public const string Version = Tag + \".\" + Distance;", generated);
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly SourceText sourceText;

        public TestAdditionalText(string path, string content)
        {
            Path = path;
            sourceText = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default)
        {
            return sourceText;
        }
    }
}