namespace DeepDive;

public sealed record ItemType(int Id, string Code, string Name, string Format)
{
    public override string ToString() => Name;
}
