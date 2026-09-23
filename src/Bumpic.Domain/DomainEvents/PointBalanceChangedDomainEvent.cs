using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 点数账户余额已变化领域事件。
/// </summary>
public record PointBalanceChangedDomainEvent(
    PointAccount PointAccount,
    AccountPointRecordType RecordType,
    int Amount,
    string BusinessReference) : IDomainEvent;
