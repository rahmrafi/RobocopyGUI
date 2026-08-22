using RobocopyGUI.Models;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;

namespace RobocopyGUI.Services;

public interface IRobocopyService
{
    Task<RobocopyResult> RunAsync(RobocopyOptions options, Action<string> onOutputLine, CancellationToken cancellationToken);
}

public class RobocopyService : IRobocopyService
{
    private Process? _process;

    private static string BuildArguments(RobocopyOptions o)
    {
        var sb = new StringBuilder();

        sb.Append('"').Append(o.SourcePath.TrimEnd('\\')).Append('"').Append(' ');
        sb.Append('"').Append(o.DestinationPath.TrimEnd('\\')).Append('"').Append(' ');

        if (!string.IsNullOrWhiteSpace(o.FileFilter))
            sb.Append(o.FileFilter.Trim()).Append(' ');

        if (o.Mirror)
        {
            sb.Append("/MIR ");
        }
        else
        {
            if (o.CopySubDirectories) sb.Append("/E ");
            if (o.Purge) sb.Append("/PURGE ");
        }

        if (o.RestartableMode) sb.Append("/Z ");
        if (o.MultiThreaded) sb.Append($"/MT:{Math.Clamp(o.ThreadCount, 1, 128)} ");

        sb.Append($"/R:{Math.Max(o.RetryCount, 0)} ");
        sb.Append($"/W:{Math.Max(o.WaitSecond, 0)} ");
        sb.Append("/NP ");

        if (o.ExcludeDirs.Count > 0)
        {
            sb.Append("/XD ");
            foreach (var dir in o.ExcludeDirs) sb.Append('"').Append(dir).Append('"').Append(' ');
        }
        if (o.ExcludeFiles.Count > 0)
        {
            sb.Append("/XD ");
            foreach (var files in o.ExcludeFiles) sb.Append('"').Append(files).Append('"').Append(' ');
        }

        return sb.ToString().TrimEnd();
    }

    private static long ParseApproxLong(string value) =>
        double.TryParse(value, out var d) ? (long)d : 0L;

    private static (long Total, long Copied)? FindRow(List<string> lines, string label)
    {
        var regex = new Regex($@"^\s*{label}\s*:\s*([\d.]+)\s*\w?\s+([\d.]+)\s*\w?", RegexOptions.IgnoreCase);

        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (!match.Success) continue;

            var total = ParseApproxLong(match.Groups[1].Value);
            var copied = ParseApproxLong(match.Groups[2].Value);
            return (total, copied);
        }

        return null;
    }

    private static RobocopyResult ParseSummary(List<string> lines)
    {
        var result = new RobocopyResult();

        var dirsMatch = FindRow(lines, "Dirs");
        var filesMatch = FindRow(lines, "Files");
        var bytesMatch = FindRow(lines, "Bytes");

        if (dirsMatch is { } d) (result.DirsTotal, result.DirsCopied) = d;
        if (filesMatch is { } f) (result.FilesTotal, result.FilesCopied) = f;
        if (bytesMatch is { } b) (result.BytesTotal, result.BytesCopied) = b;

        return result;
    }

    public static string DescribeExitCode(int code)
    {
        if (code == 0) return "No files copied - source and destination already in sync.";
        if (code == 16) return "Serious error - robocopy did not copy any files.";

        var parts = new List<string>();
        if ((code & 1) != 0) parts.Add("files copied");
        if ((code & 2) != 0) parts.Add("extra files/dirs detected");
        if ((code & 4) != 0) parts.Add("mismatched files/dirs detected");
        if ((code & 8) != 0) parts.Add("some files/dirs could not be copied (errors occurred)");

        return parts.Count > 0 ? string.Join(", ", parts) : $"Unknown exit code {code}";
    }

    public void Cancel()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    public async Task<RobocopyResult> RunAsync(RobocopyOptions options, Action<string> onOutputLine, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.SourcePath))
            throw new ArgumentException("Source path is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.DestinationPath))
            throw new ArgumentException("Destination path is required.", nameof(options));

        var arguments = BuildArguments(options);
        onOutputLine($"> robocopy {arguments}");

        var psi = new ProcessStartInfo
        {
            FileName = "robocopy.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var outputLines = new List<string>();
        var syncRoot = new object();

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (syncRoot) outputLines.Add(e.Data);
            onOutputLine(e.Data);
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine("ERROR : " + e.Data);
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("Could not start robocopy.exe. It ships with Windows, so this usually means PATH is broken.", ex);
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using (cancellationToken.Register(() => Cancel()))
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        stopwatch.Stop();

        var result = ParseSummary(outputLines);
        result.ExitCode = _process.ExitCode;
        result.ExitCodeDescription = DescribeExitCode(_process.ExitCode);
        result.Elapsed = stopwatch.Elapsed;
        return result;
    }
}
