# 会議室予約システム (Meeting Room Reservation)

Windowsネイティブの会議室予約デスクトップアプリです。会議室の一覧表示、予約登録、予約キャンセルができ、**同一会議室・同一時間帯の重複予約を禁止**します。データは SQLite に保存されます。

## 主な機能

- **会議室一覧表示**: 登録済みの会議室（名前・定員・場所）を一覧表示
- **予約登録**: 会議室・日付・開始/終了時刻・件名・予約者を指定して予約
- **予約キャンセル**: 一覧から予約を選択して削除
- **重複予約の禁止**: 同一会議室で時間帯が重なる予約はエラーになります
- **絞り込み表示**: 選択中の会議室の予約のみ表示
- **永続化**: SQLite データベースに保存（`%LocalAppData%\MeetingRoomReservation\reservations.db`）

## 技術スタック

- C# / .NET 8 (`net8.0-windows`)
- WPF (Windows Presentation Foundation)
- Microsoft.Data.Sqlite
- xUnit（テスト）

## プロジェクト構成

```
MeetingRoomReservation.sln
├── src/
│   ├── MeetingRoom.Core/   # ドメインモデル・SQLite リポジトリ・重複判定ロジック（UI非依存）
│   └── MeetingRoom.App/    # WPF デスクトップアプリ
└── tests/
    └── MeetingRoom.Tests/  # Core ロジックの単体テスト（xUnit）
```

重複判定などの業務ロジックは UI 非依存の `MeetingRoom.Core` に集約しており、`MeetingRoom.Tests` から単体テストできます。

## ビルドと実行

事前に [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です（Windows 環境）。

```powershell
# ビルド
dotnet build

# テスト
dotnet test

# アプリの起動
dotnet run --project src/MeetingRoom.App
```

## 重複予約のルール

2つの予約が同一会議室で、かつ次の条件を満たすとき「重複」とみなします。

```
既存.開始 < 新規.終了  かつ  新規.開始 < 既存.終了
```

終了時刻と次の開始時刻が一致するだけ（例: 9:00-10:00 と 10:00-11:00）の場合は重複しません。

## データの初期値

初回起動時、会議室が未登録であれば以下のサンプル会議室を自動登録します。

| 会議室     | 定員 | 場所 |
|------------|------|------|
| 第1会議室  | 6    | 3F   |
| 第2会議室  | 10   | 3F   |
| 大会議室   | 20   | 5F   |
| 応接室     | 4    | 1F   |
