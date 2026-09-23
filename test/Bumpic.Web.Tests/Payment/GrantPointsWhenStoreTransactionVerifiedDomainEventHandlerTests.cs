using MediatR;
using Moq;
using Bumpic.Domain.AggregateModel.StoreTransactionAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.PointAccount;
using Bumpic.Web.Application.DomainEventHandlers;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 交易入账领域事件处理器测试。
/// </summary>
public class GrantPointsWhenStoreTransactionVerifiedDomainEventHandlerTests
{
    /// <summary>
    /// 处理器只通过 Mediator 派发点数发放命令。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Send_GrantPurchasedPointsCommand()
    {
        var mediator = new Mock<IMediator>();
        var domainEvent = new StoreTransactionVerifiedDomainEvent(
            StoreTransactionId: new StoreTransactionId(Guid.NewGuid()),
            UserAccountId: new UserAccountId(Guid.NewGuid()),
            GrantedPoints: 100);
        var handler = new GrantPointsWhenStoreTransactionVerifiedDomainEventHandler(mediator.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<GrantPurchasedPointsCommand>(command =>
                command.UserAccountId == domainEvent.UserAccountId
                && command.Points == domainEvent.GrantedPoints),
            CancellationToken.None), Times.Once);
    }
}
