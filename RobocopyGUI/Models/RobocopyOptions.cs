namespace RobocopyGUI.Models;

public class RobocopyOption
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;

    public string FileFilter {  get; set; } = "*.*";

    public bool Mirror { get; set; }

    public bool CopySubDirectories { get; set; } = true;

    public bool Purge {  get; set; }

    public bool RestartableMode { get; set; }

    public bool MultiThreaded { get; set; } = true;
    public int ThreadCount { get; set; } = 8;
    public int RetryCount { get; set; } = 3;
    public int WaitSecond {  get; set; } = 5;

    public List<string> ExcludeDirs { get; set; } = new();
    public List<string> ExcludeFiles { get; set; } = new();
}
