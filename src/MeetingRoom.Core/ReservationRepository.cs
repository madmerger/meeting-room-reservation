using Microsoft.Data.Sqlite;
using MeetingRoom.Core.Models;

namespace MeetingRoom.Core;

/// <summary>
/// SQLite に会議室・予約データを永続化するリポジトリ。
/// 同一会議室・同一時間帯の重複予約を禁止する。
/// </summary>
public class ReservationRepository
{
    private readonly string _connectionString;

    public ReservationRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("データベースのパスを指定してください。", nameof(databasePath));

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        return connection;
    }

    /// <summary>スキーマを作成し、会議室が未登録なら初期データを投入する。</summary>
    public void Initialize()
    {
        using var connection = OpenConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Rooms (
                    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name     TEXT    NOT NULL,
                    Capacity INTEGER NOT NULL DEFAULT 0,
                    Location TEXT
                );
                CREATE TABLE IF NOT EXISTS Reservations (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    RoomId     INTEGER NOT NULL,
                    Title      TEXT    NOT NULL,
                    ReservedBy TEXT    NOT NULL,
                    Start      TEXT    NOT NULL,
                    End        TEXT    NOT NULL,
                    FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
                );";
            command.ExecuteNonQuery();
        }

        SeedRoomsIfEmpty(connection);
    }

    private static void SeedRoomsIfEmpty(SqliteConnection connection)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM Rooms;";
            var count = Convert.ToInt64(check.ExecuteScalar());
            if (count > 0) return;
        }

        var seed = new (string Name, int Capacity, string Location)[]
        {
            ("第1会議室", 6, "3F"),
            ("第2会議室", 10, "3F"),
            ("大会議室", 20, "5F"),
            ("応接室", 4, "1F"),
        };

        foreach (var room in seed)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO Rooms (Name, Capacity, Location) VALUES ($name, $cap, $loc);";
            insert.Parameters.AddWithValue("$name", room.Name);
            insert.Parameters.AddWithValue("$cap", room.Capacity);
            insert.Parameters.AddWithValue("$loc", room.Location);
            insert.ExecuteNonQuery();
        }
    }

    public List<Room> GetRooms()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Capacity, Location FROM Rooms ORDER BY Id;";

        var rooms = new List<Room>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rooms.Add(new Room
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Capacity = reader.GetInt32(2),
                Location = reader.IsDBNull(3) ? null : reader.GetString(3),
            });
        }
        return rooms;
    }

    public Room AddRoom(string name, int capacity, string? location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("会議室名を入力してください。", nameof(name));

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Rooms (Name, Capacity, Location) VALUES ($name, $cap, $loc);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$cap", capacity);
        command.Parameters.AddWithValue("$loc", (object?)location?.Trim() ?? DBNull.Value);
        var id = Convert.ToInt32(command.ExecuteScalar());
        return new Room { Id = id, Name = name.Trim(), Capacity = capacity, Location = location?.Trim() };
    }

    /// <summary>予約一覧を取得する。<paramref name="roomId"/> 指定時はその会議室のみ。</summary>
    public List<Reservation> GetReservations(int? roomId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT r.Id, r.RoomId, m.Name, r.Title, r.ReservedBy, r.Start, r.End
            FROM Reservations r
            JOIN Rooms m ON m.Id = r.RoomId
            WHERE ($roomId IS NULL OR r.RoomId = $roomId)
            ORDER BY r.Start;";
        command.Parameters.AddWithValue("$roomId", (object?)roomId ?? DBNull.Value);

        var list = new List<Reservation>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Reservation
            {
                Id = reader.GetInt32(0),
                RoomId = reader.GetInt32(1),
                RoomName = reader.GetString(2),
                Title = reader.GetString(3),
                ReservedBy = reader.GetString(4),
                Start = DateTime.Parse(reader.GetString(5)),
                End = DateTime.Parse(reader.GetString(6)),
            });
        }
        return list;
    }

    /// <summary>指定会議室・時間帯に重複する予約が存在するか。</summary>
    public bool HasConflict(int roomId, DateTime start, DateTime end, int excludeReservationId = 0)
    {
        using var connection = OpenConnection();
        return HasConflict(connection, roomId, start, end, excludeReservationId);
    }

    private static bool HasConflict(SqliteConnection connection, int roomId, DateTime start, DateTime end, int excludeReservationId)
    {
        using var command = connection.CreateCommand();
        // 重複条件: 既存.Start < 新規.End AND 新規.Start < 既存.End
        command.CommandText = @"
            SELECT COUNT(*) FROM Reservations
            WHERE RoomId = $roomId
              AND Id <> $excludeId
              AND Start < $end
              AND $start < End;";
        command.Parameters.AddWithValue("$roomId", roomId);
        command.Parameters.AddWithValue("$excludeId", excludeReservationId);
        command.Parameters.AddWithValue("$start", start.ToString("o"));
        command.Parameters.AddWithValue("$end", end.ToString("o"));
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    /// <summary>予約を追加する。重複時は <see cref="ReservationConflictException"/> をスロー。</summary>
    public Reservation AddReservation(int roomId, string title, string reservedBy, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("件名を入力してください。", nameof(title));
        if (string.IsNullOrWhiteSpace(reservedBy))
            throw new ArgumentException("予約者を入力してください。", nameof(reservedBy));
        if (end <= start)
            throw new ArgumentException("終了時刻は開始時刻より後にしてください。", nameof(end));

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (HasConflict(connection, roomId, start, end, 0))
            throw new ReservationConflictException("選択した会議室・時間帯はすでに予約されています。");

        int id;
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = @"
                INSERT INTO Reservations (RoomId, Title, ReservedBy, Start, End)
                VALUES ($roomId, $title, $by, $start, $end);
                SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$roomId", roomId);
            command.Parameters.AddWithValue("$title", title.Trim());
            command.Parameters.AddWithValue("$by", reservedBy.Trim());
            command.Parameters.AddWithValue("$start", start.ToString("o"));
            command.Parameters.AddWithValue("$end", end.ToString("o"));
            id = Convert.ToInt32(command.ExecuteScalar());
        }

        transaction.Commit();

        return new Reservation
        {
            Id = id,
            RoomId = roomId,
            Title = title.Trim(),
            ReservedBy = reservedBy.Trim(),
            Start = start,
            End = end,
        };
    }

    /// <summary>予約をキャンセル（削除）する。削除できた場合は true。</summary>
    public bool CancelReservation(int reservationId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reservations WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", reservationId);
        return command.ExecuteNonQuery() > 0;
    }
}
