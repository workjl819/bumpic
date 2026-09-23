using Bumpic.Domain.AggregateModel.UserAccountAggregate;

namespace Bumpic.Web.Application.IntegrationEvents;

/// <summary>
/// 账户已注销集成事件：宽限期届满、账户与相关资源软删除后发布，用于完成需要跨事务或跨服务处理的收尾动作。
/// </summary>
/// <param name="UserAccountId">被注销的用户账户标识。</param>
public record UserAccountDeletedIntegrationEvent(UserAccountId UserAccountId) : IIntegrationEvent;
