using System.Diagnostics;
using System.IO.Compression;

static string Arg(string[] args, string name)
{
    var i = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing " + name);
    return args[i + 1];
}

try
{
    var pid = int.Parse(Arg(args, "--pid"));
    var zip = Arg(args, "--zip");
    var target = Arg(args, "--target");

    for (var i = 0; i < 120; i++)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.HasExited) break;
        }
        catch { break; }
        Thread.Sleep(250);
    }

    var staging = Path.Combine(Path.GetTempPath(), "RAMChrome-update-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(staging);
    ZipFile.ExtractToDirectory(zip, staging);

    foreach (var source in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(staging, source);
        var destination = Path.Combine(target, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            var backup = destination + ".old";
            try { File.Replace(source, destination, backup, true); }
            catch { File.Copy(source, destination, true); }
            if (File.Exists(backup)) File.Delete(backup);
        }
        else File.Copy(source, destination, true);
    }

    Directory.Delete(staging, true);
    File.Delete(zip);
    Process.Start(new ProcessStartInfo { FileName = Path.Combine(target, "RAMChrome.exe"), UseShellExecute = true });
}
catch { Environment.ExitCode = 1; }