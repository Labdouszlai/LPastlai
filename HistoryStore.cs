using System.Text.Json;

namespace lpastlai;

public static class HistoryStore
{
    public const int MaxItems = 30;

    private static readonly string FolderPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "lpastlai");

    private static readonly string FilePath = Path.Combine(FolderPath, "history.json");

    public static List<ClipItem> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new List<ClipItem>();

            string json = File.ReadAllText(FilePath);
            var items = JsonSerializer.Deserialize<List<ClipItem>>(json);
            return items ?? new List<ClipItem>();
        }
        catch
        {
            return new List<ClipItem>();
        }
    }

    public static void Save(List<ClipItem> items)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);

            if (items.Count > MaxItems)
                items.RemoveRange(MaxItems, items.Count - MaxItems);

            string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    public static void Add(List<ClipItem> items, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        items.RemoveAll(i => i.Text == text);
        items.Insert(0, new ClipItem { Text = text, Time = DateTime.Now });

        if (items.Count > MaxItems)
            items.RemoveRange(MaxItems, items.Count - MaxItems);

        Save(items);
    }

    public static void AddImage(List<ClipItem> items, Image image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        byte[] data = ms.ToArray();

        items.Insert(0, new ClipItem { ImageData = data, Time = DateTime.Now });

        if (items.Count > MaxItems)
            items.RemoveRange(MaxItems, items.Count - MaxItems);

        Save(items);
    }

    public static void Clear(List<ClipItem> items)
    {
        items.Clear();
        Save(items);
    }
}
