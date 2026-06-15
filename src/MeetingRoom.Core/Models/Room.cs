namespace MeetingRoom.Core.Models;

/// <summary>会議室。</summary>
public class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Location { get; set; }

    public string Display => Capacity > 0 ? $"{Name}（{Capacity}名）" : Name;

    public override string ToString() => Display;
}
