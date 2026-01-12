using Microsoft.Extensions.Configuration;
using AutoPoster.Repositories;
using AutoPoster; // BlueskyClient を使うため

class Program
{
    static async Task Main(string[] args)
    {
        // 設定読み込み
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.vps.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        // DB接続文字列
        string connStr = config.GetConnectionString("Default");

        // Bluesky 設定の読み込み
        var handle = config["Bluesky:Handle"];
        var appPassword = config["Bluesky:AppPassword"];

        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(appPassword))
        {
            Console.WriteLine("Bluesky の設定が不足しています");
            return;
        }

        // DBアクセス用リポジトリと Bluesky 投稿クライアントの生成
        var repo = new DatabaseRepository(connStr);
        var bsClient = new BlueskyClient(handle, appPassword);

        // 投稿モード取得
        var mode = await repo.GetPostModeAsync();
        if (mode == null)
        {
            Console.WriteLine("投稿モードが取得できませんでした");
            return;
        }

        // 文字列で判定（normal / random）
        if (mode.Mode == "normal")
        {
            Console.WriteLine("モード: normal → 通常投稿");
        }
        else if (mode.Mode == "random")
        {
            Console.WriteLine("モード: random → ランダム投稿");
        }
        else
        {
            Console.WriteLine($"未知のモード: {mode.Mode}");
            return;
        }

        // 投稿キュー取得
        var queue = await repo.GetNextQueueAsync();
        if (queue == null)
        {
            Console.WriteLine("投稿キューが空です");
            return;
        }

        Console.WriteLine($"投稿内容: {queue.Message}");

        // 投稿処理（Bluesky）
        bool success = await bsClient.PostAsync(queue.Message);

        if (success)
        {
            await repo.DeleteQueueAsync(queue.SortIndex);
            Console.WriteLine("投稿成功 → キュー削除完了");
        }
        else
        {
            Console.WriteLine("投稿失敗 → キューは残します");
        }
    }
}