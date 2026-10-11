namespace PresetService.Entities;

public class PresetItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PresetId { get; set; }
    public Preset Preset { get; set; } = null!;
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string? Tag { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<PresetItem> Items { get; set; } = new();
}

