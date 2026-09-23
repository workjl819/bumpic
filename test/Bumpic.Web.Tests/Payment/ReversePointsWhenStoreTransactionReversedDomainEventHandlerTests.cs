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
/// 交易退款领域事件处理器测试。
/// </summary>
public class ReversePointsWhenStoreTransactionReversedDomainEventHandlerTests
{
    /// <summary>
    /// 处理器只通过 Mediator 派发点数冲正命令。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Send_ReversePurchasedPointsCommand()
    {
        var mediator = new Mock<IMediator>();
        var domainEvent = new StoreTransactionReversedDomainEvent(
            StoreTransactionId: new StoreTransactionId(Guid.NewGuid()),
            UserAccountId: new UserAccountId(Guid.NewGuid()),
            FactKey: "fact-key",
            ReversedPoints: 85);
        var handler = new ReversePointsWhenStoreTransactionReversedDomainEventHandler(mediator.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<ReversePurchasedPointsCommand>(command =>
                command.UserAccountId == domainEvent.UserAccountId
                && command.FactKey == domainEvent.FactKey
                && command.Points == domainEvent.ReversedPoints),
            CancellationToken.None), Times.Once);
    }
}
