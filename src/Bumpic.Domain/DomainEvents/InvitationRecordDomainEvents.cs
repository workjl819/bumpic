using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;

namespace Bumpic.Domain.DomainEvents;

/// <summary>
/// 邀请关系已建立领域事件。
/// </summary>
public record InvitationEstablishedDomainEvent(InvitationRecord InvitationRecord) : IDomainEvent;
