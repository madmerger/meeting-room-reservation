using MeetingRoom.Core;
using Xunit;

namespace MeetingRoom.Tests;

public class ReservationRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReservationRepository _repository;

    public ReservationRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mrr_test_{Guid.NewGuid():N}.db");
        _repository = new ReservationRepository(_dbPath);
        _repository.Initialize();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private int FirstRoomId() => _repository.GetRooms()[0].Id;

    [Fact]
    public void Initialize_SeedsDefaultRooms()
    {
        var rooms = _repository.GetRooms();
        Assert.NotEmpty(rooms);
    }

    [Fact]
    public void AddReservation_PersistsReservation()
    {
        var roomId = FirstRoomId();
        var start = new DateTime(2026, 6, 15, 9, 0, 0);
        var end = new DateTime(2026, 6, 15, 10, 0, 0);

        _repository.AddReservation(roomId, "定例会", "田中", start, end);

        var reservations = _repository.GetReservations();
        Assert.Single(reservations);
        Assert.Equal("定例会", reservations[0].Title);
    }

    [Fact]
    public void AddReservation_OverlappingSameRoom_Throws()
    {
        var roomId = FirstRoomId();
        _repository.AddReservation(roomId, "A", "田中",
            new DateTime(2026, 6, 15, 9, 0, 0), new DateTime(2026, 6, 15, 10, 0, 0));

        Assert.Throws<ReservationConflictException>(() =>
            _repository.AddReservation(roomId, "B", "鈴木",
                new DateTime(2026, 6, 15, 9, 30, 0), new DateTime(2026, 6, 15, 10, 30, 0)));
    }

    [Fact]
    public void AddReservation_AdjacentSameRoom_Allowed()
    {
        var roomId = FirstRoomId();
        _repository.AddReservation(roomId, "A", "田中",
            new DateTime(2026, 6, 15, 9, 0, 0), new DateTime(2026, 6, 15, 10, 0, 0));

        // 10:00-11:00 は前の予約の終了時刻と接するが重複しない
        _repository.AddReservation(roomId, "B", "鈴木",
            new DateTime(2026, 6, 15, 10, 0, 0), new DateTime(2026, 6, 15, 11, 0, 0));

        Assert.Equal(2, _repository.GetReservations().Count);
    }

    [Fact]
    public void AddReservation_OverlappingDifferentRoom_Allowed()
    {
        var rooms = _repository.GetRooms();
        var start = new DateTime(2026, 6, 15, 9, 0, 0);
        var end = new DateTime(2026, 6, 15, 10, 0, 0);

        _repository.AddReservation(rooms[0].Id, "A", "田中", start, end);
        _repository.AddReservation(rooms[1].Id, "B", "鈴木", start, end);

        Assert.Equal(2, _repository.GetReservations().Count);
    }

    [Fact]
    public void AddReservation_EndBeforeStart_Throws()
    {
        var roomId = FirstRoomId();
        Assert.Throws<ArgumentException>(() =>
            _repository.AddReservation(roomId, "A", "田中",
                new DateTime(2026, 6, 15, 10, 0, 0), new DateTime(2026, 6, 15, 9, 0, 0)));
    }

    [Fact]
    public void CancelReservation_RemovesReservation_AndFreesSlot()
    {
        var roomId = FirstRoomId();
        var start = new DateTime(2026, 6, 15, 9, 0, 0);
        var end = new DateTime(2026, 6, 15, 10, 0, 0);
        var created = _repository.AddReservation(roomId, "A", "田中", start, end);

        var removed = _repository.CancelReservation(created.Id);

        Assert.True(removed);
        Assert.Empty(_repository.GetReservations());

        // キャンセル後は同じ枠を再予約できる
        _repository.AddReservation(roomId, "B", "鈴木", start, end);
        Assert.Single(_repository.GetReservations());
    }

    [Fact]
    public void GetReservations_FilterByRoom()
    {
        var rooms = _repository.GetRooms();
        var start = new DateTime(2026, 6, 15, 9, 0, 0);
        var end = new DateTime(2026, 6, 15, 10, 0, 0);
        _repository.AddReservation(rooms[0].Id, "A", "田中", start, end);
        _repository.AddReservation(rooms[1].Id, "B", "鈴木", start, end);

        var filtered = _repository.GetReservations(rooms[0].Id);
        Assert.Single(filtered);
        Assert.Equal(rooms[0].Id, filtered[0].RoomId);
    }
}
