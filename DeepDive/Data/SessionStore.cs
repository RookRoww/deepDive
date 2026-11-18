namespace DeepDive;

public sealed class SessionStore : IStore
{
    private static readonly IReadOnlyList<ItemType> Types = Array.AsReadOnly<ItemType>(
    [
        new(1, "APP", "Application", "Local executable (.exe)"),
        new(2, "FOLDER", "Folder", "Local folder path"),
        new(3, "WEBSITE", "Website", "HTTP or HTTPS address")
    ]);

    private readonly Dictionary<int, Space> spaces = new();
    private readonly List<Result> launchResults = new();
    private int nextSpaceId = 1;
    private int nextItemId = 1;
    private int nextResultId = 1;

    public IReadOnlyList<Space> GetSpaces() => spaces.Values
        .OrderBy(space => space.Order)
        .ThenBy(space => space.Id)
        .Select(space => space.Copy())
        .ToList();

    public IReadOnlyList<ItemType> GetTypes() => Types;

    public void SaveSpace(Space space)
    {
        var snapshot = space.Copy();
        if (spaces.Values.Any(existing => existing.Id != snapshot.Id &&
                string.Equals(existing.Name, snapshot.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("That workspace name is already in use.");

        Space? previous = null;
        if (snapshot.Id != 0 && !spaces.TryGetValue(snapshot.Id, out previous))
            throw new InvalidOperationException("That workspace no longer exists.");

        var removed = previous?.Items.Select(item => item.Id).ToHashSet() ?? new();
        foreach (var item in snapshot.Items)
        {
            if (item.Id != 0 && !removed.Remove(item.Id))
                throw new InvalidOperationException("An item no longer belongs to this workspace.");
            item.Type = Types.FirstOrDefault(type => type.Id == item.TypeId)
                ?? throw new InvalidOperationException("The selected item type is unavailable.");
        }

        var now = DateTime.Now;
        snapshot.Id = previous?.Id ?? nextSpaceId++;
        snapshot.Order = previous?.Order ?? (spaces.Count == 0 ? 1 : spaces.Values.Max(s => s.Order) + 1);
        snapshot.Created = previous?.Created ?? now;
        snapshot.Updated = now;
        for (int i = 0; i < snapshot.Items.Count; i++)
        {
            var item = snapshot.Items[i];
            if (item.Id == 0)
                item.Id = nextItemId++;
            item.SpaceId = snapshot.Id;
            item.Order = i + 1;
        }
        spaces[snapshot.Id] = snapshot;
        launchResults.RemoveAll(result => result.SpaceId == snapshot.Id && removed.Contains(result.ItemId));

        space.Id = snapshot.Id;
        space.Order = snapshot.Order;
        space.Created = snapshot.Created;
        space.Updated = snapshot.Updated;
        for (int i = 0; i < snapshot.Items.Count; i++)
        {
            space.Items[i].Id = snapshot.Items[i].Id;
            space.Items[i].SpaceId = snapshot.Id;
            space.Items[i].Order = snapshot.Items[i].Order;
            space.Items[i].Type = snapshot.Items[i].Type;
        }
    }

    public void DeleteSpace(int id)
    {
        spaces.Remove(id);
        launchResults.RemoveAll(result => result.SpaceId == id);
    }

    public void SaveOrder(IReadOnlyList<int> ids)
    {
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("A workspace can appear only once in the order.", nameof(ids));
        if (ids.Any(id => !spaces.ContainsKey(id)))
            throw new InvalidOperationException("That workspace no longer exists.");
        for (int i = 0; i < ids.Count; i++)
            spaces[ids[i]].Order = i + 1;
    }

    public void SaveResults(IReadOnlyList<Result> results)
    {
        foreach (var result in results)
        {
            if (!spaces.TryGetValue(result.SpaceId, out var space) ||
                !space.Items.Any(item => item.Id == result.ItemId))
                throw new InvalidOperationException("The item no longer belongs to the workspace.");
        }
        foreach (var result in results)
        {
            launchResults.Add(new Result
            {
                Id = nextResultId++,
                SpaceId = result.SpaceId,
                ItemId = result.ItemId,
                Time = result.Time,
                Code = result.Code,
                Message = result.Message,
                NeedsFix = result.NeedsFix
            });
        }
    }

}
