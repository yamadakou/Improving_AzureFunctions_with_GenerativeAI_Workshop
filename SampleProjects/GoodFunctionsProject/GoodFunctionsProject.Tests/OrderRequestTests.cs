using Xunit;

namespace GoodFunctionsProject.Tests
{
    public class OrderRequestTests
    {
        // テスト観点: プロパティの初期値と設定値
        [Fact]
        public void OrderRequest_PropertySetAndGet()
        {
            var req = new OrderRequest { ProductName = "テスト商品", Quantity = 5 };
            Assert.Equal("テスト商品", req.ProductName);
            Assert.Equal(5, req.Quantity);
        }
    }
}
