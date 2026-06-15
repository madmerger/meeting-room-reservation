namespace MeetingRoom.Core;

/// <summary>同一会議室・同一時間帯の重複予約が発生したときにスローされる。</summary>
public class ReservationConflictException : Exception
{
    public ReservationConflictException(string message) : base(message)
    {
    }
}
