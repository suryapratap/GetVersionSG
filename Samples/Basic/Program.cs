using GetVersionSG.Sample;

Console.WriteLine($"Commit: {BuildInfo.CommitHash}");
Console.WriteLine($"Branch: {BuildInfo.BranchName}");
Console.WriteLine($"Version: {BuildInfo.DefVersion}");