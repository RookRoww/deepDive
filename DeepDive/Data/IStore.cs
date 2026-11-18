namespace DeepDive;

public interface IStore
{
    IReadOnlyList<Space> GetSpaces();
    IReadOnlyList<ItemType> GetTypes();
    void SaveSpace(Space space);
    void DeleteSpace(int id);
    void SaveOrder(IReadOnlyList<int> ids);
    void SaveResults(IReadOnlyList<Result> results);
}
