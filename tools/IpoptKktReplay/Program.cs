// Command-line front-end for the KKT replay/validation pipeline.
//
//   ipopt-kkt-replay generate <file> [count] [seed]
//       Write a synthetic dump of quasidefinite systems with known inertia.
//
//   ipopt-kkt-replay replay <file> [--kind auto|sparse|dense] [--dense-threshold N]
//       Replay captured (or synthetic) systems through the managed solver and
//       print an inertia-agreement + size-distribution report.

using System;
using System.IO;
using DWSIM.Numerics.Ipopt.Sparse;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Usage();
            return 1;
        }

        string mode = args[0];
        string file = args[1];

        try
        {
            switch (mode)
            {
                case "generate":
                {
                    int count = args.Length > 2 ? int.Parse(args[2]) : 200;
                    int seed = args.Length > 3 ? int.Parse(args[3]) : 1;
                    var records = SyntheticKkt.Generate(count, seed);
                    using var fs = File.Create(file);
                    KktDump.WriteAll(fs, records);
                    Console.WriteLine($"Wrote {records.Count} synthetic records to {file}");
                    return 0;
                }
                case "replay":
                {
                    var opts = new ReplayOptions();
                    for (int i = 2; i < args.Length; i++)
                    {
                        switch (args[i])
                        {
                            case "--kind":
                                opts.Kind = Enum.Parse<LinearSolverKind>(args[++i], ignoreCase: true);
                                break;
                            case "--dense-threshold":
                                opts.DenseThreshold = int.Parse(args[++i]);
                                break;
                            case "--growth-limit":
                                opts.GrowthLimit = double.Parse(args[++i]);
                                break;
                            default:
                                Console.Error.WriteLine($"Unknown option: {args[i]}");
                                return 1;
                        }
                    }

                    using var fs = File.OpenRead(file);
                    var records = KktDump.ReadAll(fs);
                    var report = ReplayEngine.Run(records, opts);
                    Console.WriteLine($"== KKT replay report ({opts.Kind}, dense-threshold={opts.DenseThreshold}) ==");
                    Console.Write(report.ToString());
                    return 0;
                }
                default:
                    Usage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  ipopt-kkt-replay generate <file> [count] [seed]");
        Console.Error.WriteLine("  ipopt-kkt-replay replay <file> [--kind auto|sparse|dense] [--dense-threshold N] [--growth-limit X]");
    }
}
