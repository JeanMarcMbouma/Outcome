using BenchmarkDotNet.Running;

var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args).ToArray();
if (summaries.Length == 0 || summaries.Any(summary => summary.Reports.Any(report => report.ResultStatistics is null)))
    Environment.ExitCode = 1;
