using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutoPoster;

/// <summary>
/// Bluesky（AT Protocol）へ投稿するためのクライアント。
/// ・セッション作成（ログイン）
/// ・投稿レコードの作成
/// ・投稿 API の呼び出し
/// を担当する。
///
/// Bluesky の API は AT Protocol の XRPC 形式で提供されており、
/// 1. createSession で JWT を取得
/// 2. createRecord で投稿
/// という2段階の処理が必要になる。
/// </summary>
public class BlueskyClient
{
    // Bluesky のログインに必要なハンドル（例: user.bsky.social）
    private readonly string _handle;

    // Bluesky の App Password（通常のパスワードとは別物）
    private readonly string _appPassword;

    // HTTP 通信を行うためのクライアント
    private readonly HttpClient _http;

    /// <summary>
    /// Bluesky のハンドルと App Password を指定してクライアントを初期化する。
    /// </summary>
    public BlueskyClient(string handle, string appPassword)
    {
        _handle = handle;
        _appPassword = appPassword;

        // HttpClient は内部で接続を再利用するため、基本的に使い回すのが推奨。
        _http = new HttpClient();
    }

    /// <summary>
    /// 指定したメッセージを Bluesky に投稿する。
    /// 内部で以下の順に処理する：
    /// 1. createSession（ログイン）で JWT を取得
    /// 2. createRecord（投稿 API）で投稿
    /// </summary>
    /// <param name="message">投稿するテキスト</param>
    /// <returns>成功した場合 true、失敗した場合 false</returns>
    public async Task<bool> PostAsync(string message)
    {
        try
        {
            // ============================================================
            // 1. セッション作成（ログイン）
            // ============================================================
            // Bluesky の API はログイン時に JWT（JSON Web Token）を返す。
            // この JWT を Bearer トークンとして投稿 API に渡す必要がある。
            var loginPayload = new
            {
                identifier = _handle,      // ログインID（@付きのハンドル）
                password = _appPassword    // App Password（16桁の専用パスワード）
            };

            var loginRes = await _http.PostAsJsonAsync(
                "https://bsky.social/xrpc/com.atproto.server.createSession",
                loginPayload
            );

            if (!loginRes.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Bluesky] ログイン失敗: {loginRes.StatusCode}");
                return false;
            }

            // レスポンス JSON から accessJwt を取得
            // 例: { "accessJwt": "xxxxx", "refreshJwt": "xxxxx", ... }
            var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
            var jwt = loginJson.GetProperty("accessJwt").GetString();

            // ============================================================
            // 2. 投稿データの作成
            // ============================================================
            // AT Protocol のレコードは $type を含む必要がある。
            // createdAt は ISO 8601（UTC）で指定する。
            var record = new Dictionary<string, object>
            {
                ["$type"] = "app.bsky.feed.post",          // 投稿レコードの型
                ["text"] = message,                        // 投稿本文
                ["createdAt"] = DateTime.UtcNow.ToString("o") // ISO 8601 (UTC)
            };

            // createRecord API に渡すペイロード
            var postPayload = new
            {
                repo = _handle,                    // 投稿先の DID またはハンドル
                collection = "app.bsky.feed.post", // 投稿コレクション
                record = record                    // 実際の投稿データ
            };

            // ============================================================
            // 3. 投稿リクエスト送信
            // ============================================================
            var req = new HttpRequestMessage(
                HttpMethod.Post,
                "https://bsky.social/xrpc/com.atproto.repo.createRecord"
            );

            // Bearer 認証ヘッダに JWT を設定
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            // JSON ボディを設定
            req.Content = JsonContent.Create(postPayload);

            // 投稿 API 呼び出し
            var postRes = await _http.SendAsync(req);

            if (!postRes.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Bluesky] 投稿失敗: {postRes.StatusCode}");

                // エラー内容を出力（API からの詳細メッセージ）
                var body = await postRes.Content.ReadAsStringAsync();
                Console.WriteLine(body);

                return false;
            }

            // 投稿成功
            return true;
        }
        catch (Exception ex)
        {
            // ネットワーク障害などの例外
            Console.WriteLine($"[Bluesky] 投稿例外: {ex.Message}");
            return false;
        }
    }
}