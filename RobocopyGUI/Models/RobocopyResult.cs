using RobocopyGUI.Models

public class RobocopyResult
{
    public int ExitCode { get; set; }
    public string ExitCodeDescription { get; set; } = string.Empty;

    public long DirsTotal { get; set; }
    public long DirsCopied { get; set; }
    public long filesTotal { get; set; }
    public long filesCopied { get; set; }
    public long BytesTotal { get; set; }
    public long BytesCopied { get; set; }

    public TimeSpan Elapsed { get; set; }

    public bool Succeeded => ExitCode < 8;
}