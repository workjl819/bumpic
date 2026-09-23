using System.Text;
using System.Text.Json;
using Bumpic.Web.Clients.Store;
using Bumpic.Web.Options;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// Google Play RTDN Pub/Sub 通知解析器测试。
/// </summary>
public class GooglePlayNotificationParserTests
{
    /// <summary>
    /// 解析一次性商品购买通知并核对部署包名。
    /// </summary>
    [Fact]
    public void Parse_Should_Parse_OneTimeProductPurchased()
    {
        var occurredAt = new DateTimeOffset(2026, 9, 11, 1, 2, 3, TimeSpan.Zero);
        var parser = new GooglePlayNotificationParser(
            Microsoft.Extensions.Options.Options.Create(new GooglePlayOptions
            {
                PackageName = "com.lumavill.photorescue"
            }));
        var payload = JsonSerializer.Serialize(new
        {
            version = "1.0",
            packageName = "com.lumavill.photorescue",
            eventTimeMillis = occurredAt.ToUnixTimeMilliseconds().ToString(),
            oneTimeProductNotification = new
            {
                version = "1.0",
                notificationType = 1,
                purchaseToken = "purchase-token",
                sku = "photorescue.points.small"
            }
        });

        var result = parser.Parse(
            messageId: "message-1",
            data: Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            publishTime: null);

        Assert.Equal("ONE_TIME_PRODUCT_PURCHASED", result.NotificationType);
        Assert.Equal(occurredAt, result.OccurredAt);
        Assert.True(result.BelongsToCurrentDeployment);
        Assert.Contains("purchase-token", result.NormalizedPayload, StringComparison.Ordinal);
    }

    /// <summary>
    /// 来源有效但包名不属于当前部署时返回部署不匹配结果。
    /// </summary>
    [Fact]
    public void Parse_Should_Reject_Other_Package()
    {
        var parser = new GooglePlayNotificationParser(
            Microsoft.Extensions.Options.Options.Create(new GooglePlayOptions
            {
                PackageName = "com.lumavill.photorescue"
            }));
        var payload = "{\"packageName\":\"com.other.app\",\"testNotification\":{\"version\":\"1.0\"}}";

        var result = parser.Parse(
            messageId: "message-2",
            data: Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            publishTime: null);

        Assert.False(result.BelongsToCurrentDeployment);
    }
}
