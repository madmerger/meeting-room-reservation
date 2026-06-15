using System.IO;
using System.Windows;
using MeetingRoom.Core;

namespace MeetingRoom.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeetingRoomReservation");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "reservations.db");
        var repository = new ReservationRepository(dbPath);
        repository.Initialize();

        var window = new MainWindow(repository);
        window.Show();
    }
}
