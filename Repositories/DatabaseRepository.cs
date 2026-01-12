using MySqlConnector;
using AutoPoster.Models;

namespace AutoPoster.Repositories;

/// <summary>
/// MySQL を利用して投稿モードや投稿キューを管理するリポジトリ。
/// AutoPoster のデータアクセス層として機能し、
/// ・現在の投稿モードの取得
/// ・投稿キューの取得
/// ・投稿済みキューの削除
/// といった DB 操作を担当する。
///
/// Repository パターンにより、
/// アプリ本体（ロジック）と DB アクセスを分離している。
/// </summary>
public class DatabaseRepository
{
    // MySQL への接続文字列（appsettings から注入される）
    private readonly string _connectionString;

    /// <summary>
    /// 接続文字列を受け取り、リポジトリを初期化する。
    /// </summary>
    public DatabaseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// post_mode テーブルから現在の投稿モードを取得する。
    /// このテーブルは 1 レコードのみ存在する前提。
    ///
    /// 例:
    /// +------+ 
    /// | mode |
    /// +------+
    /// | auto |
    /// +------+
    ///
    /// </summary>
    /// <returns>PostMode オブジェクト。存在しない場合は null。</returns>
    public async Task<PostMode?> GetPostModeAsync()
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "SELECT mode FROM post_mode LIMIT 1";

        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        // レコードが存在すれば PostMode を返す
        if (await reader.ReadAsync())
        {
            return new PostMode
            {
                Mode = reader.GetString("mode")
            };
        }

        // レコードが無い場合は null
        return null;
    }

    /// <summary>
    /// 現在時刻の「時」(hour) と一致する post_time を持つキューを 1 件取得する。
    /// 
    /// ・post_time は MySQL TIME 型（例: 09:00:00）
    /// ・現在の hour と一致するレコードを sort_index 昇順で 1 件取得
    ///
    /// 例:
    /// +------------+-----------+------------------------------+
    /// | sort_index | post_time | message                      |
    /// +------------+-----------+------------------------------+
    /// |     1      | 09:00:00  | "おはようございます"          |
    /// +------------+-----------+------------------------------+
    ///
    /// </summary>
    /// <returns>DatQueue オブジェクト。該当がなければ null。</returns>
    public async Task<DatQueue?> GetNextQueueAsync()
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        // 現在の hour を TimeSpan に変換（例: 9 → 09:00:00）
        int hour = DateTime.Now.Hour;
        var targetTime = new TimeSpan(hour, 0, 0);

        Console.WriteLine($"[DEBUG] Now={DateTime.Now:yyyy-MM-dd HH:mm:ss}, hour={hour}");
        Console.WriteLine($"[DEBUG] targetTime={targetTime}");

        const string sql = @"
            SELECT sort_index, post_time, message
            FROM dat_queue
            WHERE post_time = @time
            ORDER BY sort_index ASC
            LIMIT 1;
        ";

        Console.WriteLine("[DEBUG] Executing SQL:");
        Console.WriteLine(sql);

        await using var cmd = new MySqlCommand(sql, conn);

        // MySQL TIME 型に TimeSpan を渡す
        var p = cmd.Parameters.Add("@time", MySqlDbType.Time);
        p.Value = targetTime;

        Console.WriteLine($"[DEBUG] SQL Param @time={p.Value}");

        await using var reader = await cmd.ExecuteReaderAsync();

        bool hasRow = await reader.ReadAsync();
        Console.WriteLine($"[DEBUG] SQL Result hasRow={hasRow}");

        if (!hasRow)
            return null;

        // カラム値を取得
        int sortIndex = reader.GetInt32(reader.GetOrdinal("sort_index"));
        TimeSpan postTime = reader.GetTimeSpan(reader.GetOrdinal("post_time"));
        string message = reader.GetString(reader.GetOrdinal("message"));

        Console.WriteLine($"[DEBUG] Retrieved: sort_index={sortIndex}, post_time={postTime}, message={message}");

        // post_time は TimeSpan なので、今日の日付に加算して DateTime に変換
        return new DatQueue
        {
            SortIndex = sortIndex,
            PostTime = DateTime.Today + postTime,
            Message = message
        };
    }

    /// <summary>
    /// 投稿処理後、指定された sort_index のキューを削除する。
    /// 
    /// ・投稿が完了したキューを削除することで
    ///   次回の実行時に重複投稿を防ぐ。
    /// </summary>
    public async Task DeleteQueueAsync(int sortIndex)
    {
        using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = "DELETE FROM dat_queue WHERE sort_index = @idx";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@idx", sortIndex);

        await cmd.ExecuteNonQueryAsync();
    }
}