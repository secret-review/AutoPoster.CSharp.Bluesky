# AutoPoster (.NET 8)

Bluesky への投稿を自動化するための .NET アプリケーションです。  
投稿内容は MySQL で管理し、Docker と systemd-timer を利用することで、安定した定期実行を行います。

## 概要
- Bluesky API（AT Protocol）を利用して投稿を行います。
- 投稿キューは MySQL / MariaDB に保存します。
- Docker コンテナとして動作します。
- VPS 環境では systemd-timer により定期実行します。
- 投稿モードの取得には対応しています（※変更機能は未実装）。

## 主な機能
- 投稿キューの取得と投稿処理
- Bluesky への投稿（AT Protocol）
- 投稿モードの取得
- Docker コンテナとしての実行
- systemd-timer によるスケジュール実行

## 技術スタック
- C# (.NET 8)
- MySQL / MariaDB
- Docker
- systemd
- MySqlConnector

## セットアップ（概要）
1. `appsettings.json` を配置し、必要な設定値を入力します。
2. MySQL に `post_mode` と `dat_queue` テーブルを作成します。
3. Docker イメージをビルドします。
4. VPS 環境では systemd-timer を設定して定期実行します。

## 注意点
- 投稿モードの変更機能は今後の改善項目です（現時点では取得のみ対応）。
