namespace DeepDive;

public sealed class Space
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2563EB";
    public string Icon { get; set; } = "Briefcase";
    public int Order { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
    public List<Item> Items { get; set; } = [];

    internal Space Copy()
    {
        var copy = (Space)MemberwiseClone();
        copy.Items = Items.Select(item => item.Copy()).ToList();
        return copy;
    }
}
