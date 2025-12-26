#nullable enable
using Xunit;

namespace GoodFunctionsProject.Tests
{
    public class OrderServiceTests
    {
        // テスト観点: 正常な注文リクエストで例外が発生しないこと
        [Fact]
        public async void ProcessAsync_ValidOrder_NoException()
        {
            var service = new OrderService();
            var order = new OrderRequest { ProductName = "商品A", Quantity = 1 };
            await service.ProcessAsync(order);
        }

        // テスト観点: nullを渡した場合に例外が発生すること
        [Fact]
        public async void ProcessAsync_NullOrder_ThrowsException()
        {
            var service = new OrderService();
            await Assert.ThrowsAsync<System.ArgumentNullException>(async () => await service.ProcessAsync(null));
        }
    }
}
