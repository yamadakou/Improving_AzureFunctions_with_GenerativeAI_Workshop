#nullable enable
using System;
using System.IO;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

// テスト用のHttpCookies実装（簡易メモリ格納）
public class TestHttpCookies : HttpCookies
{
    private readonly List<IHttpCookie> _cookies = new List<IHttpCookie>();
    public override void Append(IHttpCookie cookie) { _cookies.Add(cookie); }
    public override void Append(string name, string value) { _cookies.Add(new TestHttpCookie(name, value)); }
    public override IHttpCookie CreateNew() => new TestHttpCookie("", "");
    public System.Collections.IEnumerator GetEnumerator() => _cookies.GetEnumerator();
}

// テスト用のIHttpCookie実装（Name/Value保持のみ）
public class TestHttpCookie : IHttpCookie
{
    public TestHttpCookie(string name, string value)
    {
        Name = name;
        Value = value;
    }
    public string Name { get; }
    public string Value { get; }
    public string? Domain { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public bool? HttpOnly { get; set; }
    public double? MaxAge { get; set; }
    public string? Path { get; set; }
    public Microsoft.Azure.Functions.Worker.Http.SameSite SameSite { get; set; }
    public bool? Secure { get; set; }
}

// テスト用のHttpRequestData継承クラス（ボディをJSON化して返す）
public class TestHttpRequestData : HttpRequestData
{
    private readonly object _body;
    private readonly MemoryStream _bodyStream;
    public TestHttpRequestData(FunctionContext context, object body) : base(context)
    {
        _body = body;
        var json = JsonSerializer.Serialize(body);
        _bodyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        _bodyStream.Position = 0;
    }
    public override Stream Body => _bodyStream;
    public override HttpHeadersCollection Headers => new HttpHeadersCollection();
    public override IReadOnlyCollection<IHttpCookie> Cookies => new List<IHttpCookie>();
    public override Uri Url => new Uri("http://localhost");
    public override IEnumerable<ClaimsIdentity> Identities => new List<ClaimsIdentity>();
    public override string Method => "POST";
    public override HttpResponseData CreateResponse() => new TestHttpResponseData(FunctionContext);
    public T GetBody<T>() => (T)_body;
    public Task<T> ReadFromJsonAsync<T>(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((T)_body);
    }
}
// テスト用のHttpResponseData実装（メモリボディとヘッダーを保持）
public class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext context) : base(context) { StatusCode = HttpStatusCode.OK; }
    public override HttpStatusCode StatusCode { get; set; }
    private HttpHeadersCollection _headers = new HttpHeadersCollection();
    public override HttpHeadersCollection Headers { get => _headers; set => _headers = value; }
    private Stream _body = new MemoryStream();
    public override Stream Body { get => _body; set => _body = value; }
    public override HttpCookies Cookies => new TestHttpCookies();
}

namespace GoodFunctionsProject.Tests
{
    public class OrderFunctionTests
    {
        // テスト観点: 正常な注文リクエストで200 OKが返ること
        [Fact]
        public async Task Run_ValidOrder_ReturnsOk()
        {
            // Arrange
            var order = new OrderRequest { ProductName = "商品A", Quantity = 2 };
            var serviceMock = new Mock<IOrderService>();
            var validatorMock = new Mock<IOrderValidator>();
            validatorMock.Setup(v => v.Validate(It.IsAny<OrderRequest>())).Returns(Array.Empty<string>());
            var loggerMock = new Mock<ILogger<OrderFunction>>();
            var context = new Mock<FunctionContext>().Object;
            var req = new TestHttpRequestData(context, order);
            var res = new TestHttpResponseData(context);
            var function = new OrderFunction(serviceMock.Object, validatorMock.Object, loggerMock.Object);
            // Act
            var result = await function.Run(req);
            // Assert
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            serviceMock.Verify(s => s.ProcessAsync(It.Is<OrderRequest>(o => o.ProductName == order.ProductName && o.Quantity == order.Quantity)), Times.Once);
        }

        // テスト観点: 不正な注文リクエスト（商品名なし）で400 BadRequestが返ること
        [Fact]
        public async Task Run_InvalidOrder_NoProductName_ReturnsBadRequest()
        {
            var order = new OrderRequest { ProductName = "", Quantity = 2 };
            var serviceMock = new Mock<IOrderService>();
            var validatorMock = new Mock<IOrderValidator>();
            validatorMock.Setup(v => v.Validate(It.IsAny<OrderRequest>())).Returns(new[] { "商品名は必須です。" });
            var loggerMock = new Mock<ILogger<OrderFunction>>();
            var context = new Mock<FunctionContext>().Object;
            var req = new TestHttpRequestData(context, order);
            var res = new TestHttpResponseData(context);
            var function = new OrderFunction(serviceMock.Object, validatorMock.Object, loggerMock.Object);
            var result = await function.Run(req);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            serviceMock.Verify(s => s.ProcessAsync(It.IsAny<OrderRequest>()), Times.Never);
        }

        // テスト観点: 不正な注文リクエスト（数量0以下）で400 BadRequestが返ること
        [Fact]
        public async Task Run_InvalidOrder_QuantityZero_ReturnsBadRequest()
        {
            var order = new OrderRequest { ProductName = "商品A", Quantity = 0 };
            var serviceMock = new Mock<IOrderService>();
            var validatorMock = new Mock<IOrderValidator>();
            validatorMock.Setup(v => v.Validate(It.IsAny<OrderRequest>())).Returns(new[] { "数量は1以上で指定してください。" });
            var loggerMock = new Mock<ILogger<OrderFunction>>();
            var context = new Mock<FunctionContext>().Object;
            var req = new TestHttpRequestData(context, order);
            var res = new TestHttpResponseData(context);
            var function = new OrderFunction(serviceMock.Object, validatorMock.Object, loggerMock.Object);
            var result = await function.Run(req);
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
            serviceMock.Verify(s => s.ProcessAsync(It.IsAny<OrderRequest>()), Times.Never);
        }

        // テスト観点: サービスで例外が発生した場合500 InternalServerErrorが返ること
        [Fact]
        public async Task Run_ServiceThrows_ReturnsInternalServerError()
        {
            var order = new OrderRequest { ProductName = "商品A", Quantity = 1 };
            var serviceMock = new Mock<IOrderService>();
            var validatorMock = new Mock<IOrderValidator>();
            validatorMock.Setup(v => v.Validate(It.IsAny<OrderRequest>())).Returns(Array.Empty<string>());
            var loggerMock = new Mock<ILogger<OrderFunction>>();
            var context = new Mock<FunctionContext>().Object;
            var req = new TestHttpRequestData(context, order);
            var res = new TestHttpResponseData(context);
            serviceMock.Setup(s => s.ProcessAsync(It.Is<OrderRequest>(o => o.ProductName == order.ProductName && o.Quantity == order.Quantity))).ThrowsAsync(new System.Exception("error"));
            var function = new OrderFunction(serviceMock.Object, validatorMock.Object, loggerMock.Object);
            var result = await function.Run(req);
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }
    }
}
