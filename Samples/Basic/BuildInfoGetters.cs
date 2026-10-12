using GetVersionSG;

namespace GetVersionSG.Sample;

public static class BuildInfoGetters
{
    [CommitInfoGetter]
    public static string DefVersion() => $"{BuildInfo.Tags}-{BuildInfo.Distance}";
}