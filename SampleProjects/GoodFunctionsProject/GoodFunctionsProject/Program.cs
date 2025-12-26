using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

// Azure Functions ホストの構成を定義するエントリポイント
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<GlobalExceptionMiddleware>();
    })
    // DI登録
    .ConfigureServices(s =>
    {
        s.AddScoped<IOrderService, OrderService>();
        s.AddScoped<IOrderValidator, OrderValidator>();
    })
    .Build();

host.Run();
