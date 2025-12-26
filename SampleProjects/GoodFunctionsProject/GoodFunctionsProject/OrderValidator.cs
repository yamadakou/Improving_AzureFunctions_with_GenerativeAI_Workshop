using System.Collections.Generic;
using System.Linq;

// 注文の入力検証を行うインターフェース
public interface IOrderValidator
{
    IReadOnlyCollection<string> Validate(OrderRequest order);
}

// 必須項目と数量の簡易チェックを実装
public class OrderValidator : IOrderValidator
{
    public IReadOnlyCollection<string> Validate(OrderRequest order)
    {
        var errors = new List<string>();
        if (order == null)
        {
            errors.Add("注文が空です。");
            return errors;
        }
        if (string.IsNullOrWhiteSpace(order.ProductName))
        {
            errors.Add("商品名は必須です。");
        }
        if (order.Quantity <= 0)
        {
            errors.Add("数量は1以上で指定してください。");
        }
        return errors;
    }
}