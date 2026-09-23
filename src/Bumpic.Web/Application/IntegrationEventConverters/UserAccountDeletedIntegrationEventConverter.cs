using Bumpic.Domain.DomainEvents;
using Bumpic.Web.Application.IntegrationEvents;

namespace Bumpic.Web.Application.IntegrationEventConverters;

/// <summary>
/// 账户注销领域事件到集成事件的转换器：只做映射，不带业务逻辑。
/// </summary>
public class UserAccountDeletedIntegrationEventConverter
    : IIntegrationEventConverter<UserAccountDeletedDomainEvent, UserAccountDeletedIntegrationEvent>
{
    /// <inheritdoc />
    public UserAccountDeletedIntegrationEvent Convert(UserAccountDeletedDomainEvent domainEvent)
    {
        return new UserAccountDeletedIntegrationEvent(domainEvent.UserAccount.Id);
    }
}
