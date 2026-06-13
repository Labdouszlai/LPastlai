namespace lpastlai;

public class ClipItem
{
    public string Text { get; set; } = "";
    public byte[]? ImageData { get; set; }
    public DateTime Time { get; set; }

    public bool IsImage => ImageData is { Length: > 0 };
}
