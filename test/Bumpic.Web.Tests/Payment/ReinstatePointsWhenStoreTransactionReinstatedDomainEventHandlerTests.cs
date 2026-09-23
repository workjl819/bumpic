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
/// 交易退款撤回领域事件处理器测试。
/// </summary>
public class ReinstatePointsWhenStoreTransactionReinstatedDomainEventHandlerTests
{
    /// <summary>
    /// 处理器只通过 Mediator 派发点数恢复命令。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Send_ReinstatePurchasedPointsCommand()
    {
        var mediator = new Mock<IMediator>();
        var domainEvent = new StoreTransactionReinstatedDomainEvent(
            StoreTransactionId: new StoreTransactionId(Guid.NewGuid()),
            UserAccountId: new UserAccountId(Guid.NewGuid()),
            FactKey: "fact-key",
            ReinstatedPoints: 85);
        var handler = new ReinstatePointsWhenStoreTransactionReinstatedDomainEventHandler(mediator.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<ReinstatePurchasedPointsCommand>(command =>
                command.UserAccountId == domainEvent.UserAccountId
                && command.FactKey == domainEvent.FactKey
                && command.Points == domainEvent.ReinstatedPoints),
            CancellationToken.None), Times.Once);
    }
}
