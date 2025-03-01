namespace PicView.Avalonia.Update;

public class UpdateInfo
{
    public required string Version { get; set; }
    public required string X64Portable { get; set; }
    public required string X64Install { get; set; }
    public required string Arm64Portable { get; set; }
    public required string Arm64Install { get; set; }
}
