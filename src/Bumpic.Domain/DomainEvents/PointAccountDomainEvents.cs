using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 点数已发放领域事件：由点数账户在入账时发布，用于驱动流水追加等后续处理。
/// </summary>
/// <param name="PointAccount">发生入账的点数账户。</param>
/// <param name="RecordType">对应流水类型。</param>
/// <param name="Amount">本次发放点数。</param>
/// <param name="BusinessReference">唯一业务引用。</param>
public record PointsGrantedDomainEvent(
    PointAccount PointAccount,
    AccountPointRecordType RecordType,
    int Amount,
    string BusinessReference) : IDomainEvent;


/// <summary>
/// 修复点数已变化，驱动不可变流水追加。
/// </summary>
/// <param name="PointAccount">发生变化的账户。</param>
/// <param name="RecordType">修复流水类型。</param>
/// <param name="BusinessReference">任务业务引用。</param>
public record RestorationPointsChangedDomainEvent(PointAccount PointAccount,
    AccountPointRecordType RecordType, string BusinessReference) : IDomainEvent;
