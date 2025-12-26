using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

// HTTPトリガーの未処理例外を捕捉し共通レスポンスを返すミドルウェア
public class GlobalExceptionMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var loggerFactory = context.InstanceServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            var logger = loggerFactory?.CreateLogger<GlobalExceptionMiddleware>();

            // HTTPリクエストを取得して相関IDを付与し、レスポンスを生成
            var req = await context.GetHttpRequestDataAsync();
            var correlationId = req?.Headers?.FirstOrDefault(h => h.Key.Equals("x-correlation-id", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault()
                               ?? Guid.NewGuid().ToString();

            logger?.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            var res = req?.CreateResponse() ?? context.GetInvocationResult()?.Value as HttpResponseData;
            if (res == null && req != null)
            {
                res = req.CreateResponse();
            }

            if (res != null)
            {
                res.StatusCode = HttpStatusCode.InternalServerError;
                res.Headers.Add("x-correlation-id", correlationId);
                await res.WriteStringAsync("サーバーエラーが発生しました");
                var result = context.GetInvocationResult();
                if (result != null)
                {
                    result.Value = res;
                }
            }
        }
    }
}