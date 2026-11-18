namespace DeepDive;

public sealed class Item
{
    public int Id { get; set; }
    public int SpaceId { get; set; }
    public int TypeId { get; set; }
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
    public ItemType Type { get; set; } = new(0, "", "", "");

    internal Item Copy() => (Item)MemberwiseClone();
}
