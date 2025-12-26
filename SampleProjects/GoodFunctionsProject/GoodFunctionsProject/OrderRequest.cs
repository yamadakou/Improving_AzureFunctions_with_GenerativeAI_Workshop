// 注文リクエストのDTO（データ転送オブジェクト）
public class OrderRequest
{
    // 商品名
    public string ProductName { get; set; }
    // 注文数量
    public int Quantity { get; set; }
}