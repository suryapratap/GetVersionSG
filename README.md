# GetVersionSG

`GetVersionSG` is a Roslyn source generator that exposes Git metadata as compile-time constants.

## Usage

Add the `GetVersionSG` NuGet package to a project and create a file named `<ClassName>.commit-info` in that project's root directory. The first line is the namespace for the generated class:

```text
MyBuildInfo.commit-info
MyCompany.Product.Build
wildcard:v*
```

The package automatically includes root `.commit-info` files as compiler additional files. For the example above, every build generates `MyCompany.Product.Build.MyBuildInfo` with these public constants:

```csharp
MyBuildInfo.CommitHash
MyBuildInfo.Tags
MyBuildInfo.Distance
MyBuildInfo.BranchName
MyBuildInfo.Version
```

The values are read from the repository containing the `.commit-info` file. `Tags` contains all matching tags on the nearest matching tagged commit, separated by newlines; `Distance` is the number of commits from that commit. Untagged repositories, or repositories with no matching tag, use empty tags and a distance of `0`, while detached HEAD uses `HEAD` as the branch name.

An optional second line filters tags. Use `wildcard:<pattern>` (`*` matches any number of characters and `?` matches one character) or `regex:<pattern>`:

```text
MyCompany.Product.Build
regex:^v[0-9]+\.[0-9]+\.[0-9]+$
```

If no `.commit-info` file is present, the generator still emits `GetVersionSG.GitCommitInfo` with empty commit and tag values, a distance of `0`, and branch name `HEAD`.

Additional compile-time constants can be defined with parameterless static string methods marked with `CommitInfoGetter`. The method can be placed directly after the tag filter in the `.commit-info` file:

```text
MyCompany.Product.Build
wildcard:v*

[CommitInfoGetter]
public static string Version() => $"{MyBuildInfo.Tags}-{MyBuildInfo.Distance}";
```

The method body must return a C# constant expression using the generated class and metadata names. Alternatively, place the method in a separate helper class (a C# method and constant cannot have the same name in one class):

```csharp
using GetVersionSG;

namespace MyCompany.Product.Build;

public static class BuildInfoGetters
{
    [CommitInfoGetter]
    public static string Version() => MyBuildInfo.Tags + "." + MyBuildInfo.Distance;
}
```

This adds `MyBuildInfo.Version` as a `public const string`. Interpolated strings and other C# constant expressions are supported; the expression is copied into the generated class rather than executed during the build.

## Working sample

The `Samples/Basic` project demonstrates the complete setup, including a `.commit-info` file, a `CommitInfoGetter`, and usage of the generated constants from an executable. From the repository root, run:

```text
dotnet run --project Samples/Basic/GetVersionSG.Sample.csproj
```

The sample references the generator project as an analyzer so it can be built directly from this repository. A packaged application uses the `GetVersionSG` package instead; the generated API is the same.