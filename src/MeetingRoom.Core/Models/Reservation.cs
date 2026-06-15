namespace MeetingRoom.Core.Models;

/// <summary>会議室の予約。</summary>
public class Reservation
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ReservedBy { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public string TimeRange => $"{Start:yyyy/MM/dd HH:mm} - {End:HH:mm}";
}
