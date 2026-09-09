using System.Text;
using Wolpertinger.Edge.Replay;
using Wolpertinger.Edge.Runtime;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        if (args.Length == 0) return Usage();
        return args[0] switch
        {
            "ingest" => await IngestAsync(args[1..]),
            "replay" => await ReplayAsync(args[1..]),
            _ => Usage(),
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        return 1;
    }
}

static async Task<int> IngestAsync(string[] args)
{
    var journal = Require(args, "--journal"); var data = Require(args, "--data"); var kernel = Require(args, "--kernel");
    await using var runner = await VerticalSliceRunner.OpenAsync(data, kernel);
    foreach (var line in File.ReadLines(journal))
        await runner.ProcessJournalLineAsync(Encoding.UTF8.GetBytes(line));
    foreach (var output in runner.Outputs) Console.Out.WriteLine(output.Text);
    foreach (var diagnostic in runner.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    if (runner.FinalStateDigest is { } digest) Console.Error.WriteLine($"StateDigest: {digest.Hex}");
    return 0;
}

static async Task<int> ReplayAsync(string[] args)
{
    var data = Require(args, "--data"); var kernel = Require(args, "--kernel");
    var result = await new ExactReplayRunner().RunAsync(data, kernel);
    foreach (var output in result.Outputs) Console.Out.WriteLine(output.Text);
    if (result.FinalStateDigest is { } digest) Console.Error.WriteLine($"StateDigest: {digest.Hex}");
    return 0;
}

static string Require(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        throw new ArgumentException($"Missing required option {name}.");
    return args[index + 1];
}

static int Usage()
{
    Console.Error.WriteLine("Usage: wolpertinger-host ingest --journal <jsonl> --data <dir> --kernel <exe>");
    Console.Error.WriteLine("   or: wolpertinger-host replay --data <dir> --kernel <exe>");
    return 2;
}
