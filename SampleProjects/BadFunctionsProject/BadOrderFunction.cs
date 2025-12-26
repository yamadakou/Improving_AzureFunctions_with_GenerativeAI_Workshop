using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public static class BadOrderFunction
{
    [Function("BadOrderFunction")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        await Task.Delay(100); // 注文の処理をシミュレート
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteStringAsync("processed");
        return res;
    }
}
