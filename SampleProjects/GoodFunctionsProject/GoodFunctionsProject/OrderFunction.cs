// 必要な名前空間のインポート
using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

// 注文処理用のHTTPトリガー関数クラス（依存サービスはDIで注入）
public class OrderFunction
{
    // 注文サービスのインスタンス
    private readonly IOrderService _service;
    private readonly IOrderValidator _validator;
    // ロガーのインスタンス
    private readonly ILogger<OrderFunction> _logger;

    // コンストラクタで依存性を受け取り保持する
    public OrderFunction(IOrderService service, IOrderValidator validator, ILogger<OrderFunction> logger)
    {
        _service = service;
        _validator = validator;
        _logger = logger;
    }

    // HTTPトリガーで注文処理を実行（JSON読込→検証→処理→応答）
    [Function("OrderFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("注文処理開始"); // 開始ログ
        var res = req.CreateResponse();
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var ids) ? ids.FirstOrDefault() : Guid.NewGuid().ToString();
        res.Headers.Add("x-correlation-id", correlationId);

        // 拡張メソッドに依存せず自前でJSONデシリアライズ（テスト/本番共通で安定）
        var order = await JsonSerializer.DeserializeAsync<OrderRequest>(req.Body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // 共通バリデーション
        var validationErrors = _validator.Validate(order);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("不正な注文リクエスト: {Errors}", string.Join(";", validationErrors));
            res.StatusCode = HttpStatusCode.BadRequest;
            await res.WriteStringAsync("不正なリクエストです");
            return res;
        }

        // 業務処理（例外はグローバルミドルウェアで500化）
        try
        {
            await _service.ProcessAsync(order);
            res.StatusCode = HttpStatusCode.OK;
            await res.WriteStringAsync("注文を受け付けました");
            _logger.LogInformation("注文処理完了"); // 完了ログ
            return res;
        }
        catch (Exception ex)
        {
            // 単体テストなどでミドルウェアが無い場合も500を返す
            _logger.LogError(ex, "注文処理中にエラー発生");
            res.StatusCode = HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("サーバーエラーが発生しました");
            return res;
        }
    }
}
