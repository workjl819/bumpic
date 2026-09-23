using MediatR;
using Moq;
using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Points;
using Bumpic.Web.Application.DomainEventHandlers;

namespace Bumpic.Web.Tests.Payment;

/// <summary>
/// 点数余额变化领域事件处理器测试。
/// </summary>
public class PointBalanceChangedDomainEventHandlerTests
{
    /// <summary>
    /// 处理器只通过命令追加独立点数流水。
    /// </summary>
    [Fact]
    public async Task Handle_Should_Send_AppendAccountPointRecordCommand()
    {
        var mediator = new Mock<IMediator>();
        var account = PointAccount.Create(new UserAccountId(Guid.NewGuid()));
        var domainEvent = new PointBalanceChangedDomainEvent(
            PointAccount: account,
            RecordType: AccountPointRecordType.PurchaseReversed,
            Amount: -100,
            BusinessReference: "REFUND:Apple:key:fact");
        var handler = new PointBalanceChangedDomainEventHandlerForAppendPointRecord(mediator.Object);

        await handler.Handle(domainEvent, CancellationToken.None);

        mediator.Verify(x => x.Send(
            It.Is<AppendAccountPointRecordCommand>(command =>
                command.UserAccountId == account.UserAccountId
                && command.RecordType == AccountPointRecordType.PurchaseReversed
                && command.Amount == -100
                && command.BusinessReference == "REFUND:Apple:key:fact"),
            CancellationToken.None), Times.Once);
    }
}
