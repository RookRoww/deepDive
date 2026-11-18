namespace DeepDive;

public sealed class Result
{
    public int Id { get; set; }
    public int SpaceId { get; set; }
    public int ItemId { get; set; }
    public DateTime Time { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public bool NeedsFix { get; set; }
}
