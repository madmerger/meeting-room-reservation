using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MeetingRoom.Core;
using MeetingRoom.Core.Models;

namespace MeetingRoom.App;

public partial class MainWindow : Window
{
    private readonly ReservationRepository _repository;
    private readonly ObservableCollection<Room> _rooms = new();
    private readonly ObservableCollection<Reservation> _reservations = new();

    public MainWindow(ReservationRepository repository)
    {
        _repository = repository;
        InitializeComponent();

        RoomList.ItemsSource = _rooms;
        RoomCombo.ItemsSource = _rooms;
        ReservationGrid.ItemsSource = _reservations;

        BuildTimeOptions();
        DatePick.SelectedDate = DateTime.Today;

        LoadRooms();
        LoadReservations();
    }

    private void BuildTimeOptions()
    {
        var times = new List<string>();
        for (var minutes = 0; minutes < 24 * 60; minutes += 30)
            times.Add($"{minutes / 60:D2}:{minutes % 60:D2}");

        StartCombo.ItemsSource = times;
        EndCombo.ItemsSource = times;
        StartCombo.SelectedItem = "09:00";
        EndCombo.SelectedItem = "10:00";
    }

    private void LoadRooms()
    {
        _rooms.Clear();
        foreach (var room in _repository.GetRooms())
            _rooms.Add(room);

        if (RoomCombo.SelectedItem == null && _rooms.Count > 0)
            RoomCombo.SelectedIndex = 0;
    }

    private void LoadReservations()
    {
        int? roomId = null;
        if (FilterCheck.IsChecked == true && RoomList.SelectedItem is Room selected)
            roomId = selected.Id;

        _reservations.Clear();
        foreach (var reservation in _repository.GetReservations(roomId))
            _reservations.Add(reservation);
    }

    private void RoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RoomList.SelectedItem is Room room)
            RoomCombo.SelectedItem = room;

        if (FilterCheck.IsChecked == true)
            LoadReservations();
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => LoadReservations();

    private bool TryBuildDateTime(out DateTime start, out DateTime end)
    {
        start = default;
        end = default;

        if (DatePick.SelectedDate is not DateTime date)
        {
            MessageBox.Show("日付を選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (StartCombo.SelectedItem is not string startText ||
            EndCombo.SelectedItem is not string endText)
        {
            MessageBox.Show("開始時刻と終了時刻を選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var startSpan = TimeSpan.Parse(startText);
        var endSpan = TimeSpan.Parse(endText);
        start = date.Date + startSpan;
        end = date.Date + endSpan;

        if (end <= start)
        {
            MessageBox.Show("終了時刻は開始時刻より後にしてください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void Reserve_Click(object sender, RoutedEventArgs e)
    {
        if (RoomCombo.SelectedItem is not Room room)
        {
            MessageBox.Show("会議室を選択してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("件名を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ReservedByBox.Text))
        {
            MessageBox.Show("予約者を入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryBuildDateTime(out var start, out var end))
            return;

        try
        {
            _repository.AddReservation(room.Id, TitleBox.Text, ReservedByBox.Text, start, end);
        }
        catch (ReservationConflictException ex)
        {
            MessageBox.Show(ex.Message, "重複予約", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TitleBox.Clear();
        LoadReservations();
        MessageBox.Show("予約を登録しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationGrid.SelectedItem is not Reservation reservation)
        {
            MessageBox.Show("キャンセルする予約を一覧から選択してください。", "未選択", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"次の予約をキャンセルしますか？\n\n{reservation.RoomName}\n{reservation.Title}\n{reservation.TimeRange}",
            "予約キャンセルの確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        _repository.CancelReservation(reservation.Id);
        LoadReservations();
    }
}
