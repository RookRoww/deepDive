namespace DeepDive;

internal sealed class SpaceService(IStore store)
{
    public Space NewDraft() => new();

    public Space EditDraft(Space space) => space.Copy();

    public string? Validate(Space space)
    {
        string name = space.Name.Trim();
        if (name.Length is 0 or > 100)
            return "Enter a workspace name between 1 and 100 characters.";

        foreach (Item item in space.Items)
        {
            if (ItemRules.Validate(item) is { } error)
                return string.IsNullOrWhiteSpace(item.Name) ? error : $"{item.Name}: {error}";
        }

        return store.GetSpaces().Any(other =>
            other.Id != space.Id && string.Equals(other.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))
            ? "A workspace with this name already exists."
            : null;
    }

    public void Save(Space space)
    {
        if (Validate(space) is { } error)
            throw new InvalidOperationException(error);

        space.Name = space.Name.Trim();
        for (int index = 0; index < space.Items.Count; index++)
        {
            Item item = space.Items[index];
            item.Name = item.Name.Trim();
            item.Target = item.Target.Trim();
            item.Order = index + 1;
            item.TypeId = item.Type.Id;
        }

        store.SaveSpace(space);
    }
}
