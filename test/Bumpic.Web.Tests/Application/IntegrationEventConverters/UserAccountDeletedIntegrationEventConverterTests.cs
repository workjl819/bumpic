using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.IntegrationEventConverters;

namespace Bumpic.Web.Tests.Application.IntegrationEventConverters;

/// <summary>
/// 账户注销领域事件到集成事件的转换器单元测试。
/// </summary>
public class UserAccountDeletedIntegrationEventConverterTests
{
    /// <summary>
    /// 转换结果携带被注销账户标识。
    /// </summary>
    [Fact]
    public void Convert_UserAccountDeletedDomainEvent_MapsUserAccountId()
    {
        var user = UserAccount.Register("converter@example.com");
        var converter = new UserAccountDeletedIntegrationEventConverter();

        var integrationEvent = converter.Convert(new UserAccountDeletedDomainEvent(user));

        Assert.Equal(user.Id, integrationEvent.UserAccountId);
    }
}
