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
            "v1.2.3\nv1.2.2",
            "7",
            "feature/test");

        var source = GitCommitInfoGenerator.SourceWriter.Write("Demo", "BuildInfo", metadata);

        Assert.Contains("public static class BuildInfo", source);
        Assert.Contains("public const string CommitHash = \"abc123\";", source);
        Assert.Contains("public const string Tags = \"v1.2.3\\nv1.2.2\";", source);
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
    public void GitMetadataReadsTagsFromNearestTaggedCommit()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".."));

        var metadata = GitCommitInfoGenerator.GitMetadata.Read(repositoryPath);

        Assert.Equal("TEST-1\nV0.0.1", metadata.Tags);
        Assert.Equal("4", metadata.Distance);
    }

    [Fact]
    public void GitMetadataFiltersTagsWithWildcardAndKeepsDistance()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".."));

        var metadata = GitCommitInfoGenerator.GitMetadata.Read(repositoryPath, "wildcard:V*");

        Assert.Equal("V0.0.1", metadata.Tags);
        Assert.Equal("4", metadata.Distance);
    }

    [Fact]
    public void GitMetadataFiltersTagsWithRegex()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".."));

        var metadata = GitCommitInfoGenerator.GitMetadata.Read(repositoryPath, "regex:^TEST-[0-9]+$");

        Assert.Equal("TEST-1", metadata.Tags);
        Assert.Equal("4", metadata.Distance);
    }

    [Fact]
    public void GitMetadataReturnsDefaultsWhenNoTagMatches()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".."));

        var metadata = GitCommitInfoGenerator.GitMetadata.Read(repositoryPath, "wildcard:no-match-*");

        Assert.Empty(metadata.Tags);
        Assert.Equal("0", metadata.Distance);
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
                        public static string Version() => Tags + "." + Distance;
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
        Assert.Contains("public const string Version = Tags + \".\" + Distance;", generated);
    }

    [Fact]
    public void GeneratorAppliesTagFilterFromCommitInfoFile()
    {
        var repositoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "..", ".."));
        var additionalText = new TestAdditionalText(
            Path.Combine(repositoryPath, "FilteredBuild.commit-info"),
            "Demo.Generated\nregex:^V[0-9].*$");
        var compilation = CSharpCompilation.Create("Demo");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GitCommitInfoGenerator())
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));

        driver = driver.RunGenerators(compilation);

        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "FilteredBuild.g.cs").SourceText.ToString();
        Assert.Contains("public const string Tags = \"V0.0.1\";", generated);
        Assert.Contains("public const string Distance = \"4\";", generated);
    }

    [Fact]
    public void GeneratorReadsInlineGetterFromCommitInfoFile()
    {
        var additionalText = new TestAdditionalText(
            Path.Combine(Environment.CurrentDirectory, "BuildInfo.commit-info"),
            "Demo.Generated\nV*\n\n[CommitInfoGetter]\npublic static string DefVersion() => $\"{BuildInfo.Tags}-{BuildInfo.Distance}\";");
        var compilation = CSharpCompilation.Create("Demo");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GitCommitInfoGenerator())
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));

        driver = driver.RunGenerators(compilation);

        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "BuildInfo.g.cs").SourceText.ToString();
        Assert.Contains("public const string DefVersion = $\"{BuildInfo.Tags}-{BuildInfo.Distance}\";", generated);
    }

    [Fact]
    public void GeneratorEmitsConfiguredClassWithoutGetters()
    {
        var additionalText = new TestAdditionalText(
            Path.Combine(Environment.CurrentDirectory, "BuildInfo.commit-info"),
            "Demo.Generated");
        var compilation = CSharpCompilation.Create("Demo");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GitCommitInfoGenerator())
            .AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));

        driver = driver.RunGenerators(compilation);

        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "BuildInfo.g.cs").SourceText.ToString();
        Assert.Contains("namespace Demo.Generated;", generated);
        Assert.Contains("public static class BuildInfo", generated);
        Assert.Contains("public const string CommitHash", generated);
    }

    [Fact]
    public void GeneratorEmitsDefaultClassWithoutCommitInfoFile()
    {
        var compilation = CSharpCompilation.Create("Demo");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new GitCommitInfoGenerator());

        driver = driver.RunGenerators(compilation);

        var generated = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(source => source.HintName == "GitCommitInfo.g.cs").SourceText.ToString();
        Assert.Contains("namespace GetVersionSG;", generated);
        Assert.Contains("public static class GitCommitInfo", generated);
        Assert.Contains("public const string Distance = \"0\";", generated);
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