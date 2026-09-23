using MediatR;
using NetCorePal.Extensions.Primitives;
using Bumpic.Domain;
using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Points;
using Bumpic.Web.Application.DomainEventHandlers;
using Bumpic.Web.Application.Queries.Invitation;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.DomainEventHandlers;

/// <summary>
/// 邀请奖励发放与邀请人数上限的领域事件处理器单元测试。
/// </summary>
public class InvitationEstablishedDomainEventHandlerForSendInvitationRewardTests
{
    private static readonly UserAccountId InviterId = new(Guid.NewGuid());
    private static readonly UserAccountId InviteeId = new(Guid.NewGuid());

    /// <summary>
    /// 未达上限时先查询邀请人数，再以邀请记录标识为业务引用发放奖励。
    /// </summary>
    [Fact]
    public async Task Handle_BelowLimit_GrantsRewardToInviter()
    {
        var invitationRecord = CreateInvitationRecord();
        var (handler, mediator) = CreateHandler(invitationCount: InvitationPolicy.MaxInvitationsPerAccount - 1);

        await handler.Handle(new InvitationEstablishedDomainEvent(invitationRecord), CancellationToken.None);

        var query = Assert.IsType<GetInviterInvitationCountQuery>(mediator.Requests[0]);
        Assert.Equal(InviterId, query.InviterUserAccountId);
        var command = Assert.IsType<GrantPointsCommand>(Assert.Single(mediator.Commands));
        Assert.Equal(InviterId, command.UserAccountId);
        Assert.Equal(InvitationPolicy.RewardPoints, command.Points);
        Assert.Equal(invitationRecord.RewardPoints, command.Points);
        Assert.Equal(AccountPointRecordType.InvitationGranted, command.RecordType);
        Assert.Equal("invitation:" + invitationRecord.Id.Id, command.BusinessReference);
    }

    /// <summary>
    /// 邀请人已达奖励上限时：邀请关系保留（事件仍然处理完成），仅不再发放奖励且不抛异常。
    /// </summary>
    [Fact]
    public async Task Handle_ReachedLimit_SkipsGrantWithoutThrowing()
    {
        var invitationRecord = CreateInvitationRecord();
        var (handler, mediator) = CreateHandler(invitationCount: InvitationPolicy.MaxInvitationsPerAccount);

        await handler.Handle(new InvitationEstablishedDomainEvent(invitationRecord), CancellationToken.None);

        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 超过上限时同样跳过发放且不抛异常。
    /// </summary>
    [Fact]
    public async Task Handle_AboveLimit_SkipsGrantWithoutThrowing()
    {
        var invitationRecord = CreateInvitationRecord();
        var (handler, mediator) = CreateHandler(invitationCount: InvitationPolicy.MaxInvitationsPerAccount + 1);

        await handler.Handle(new InvitationEstablishedDomainEvent(invitationRecord), CancellationToken.None);

        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 构造邀请记录，并补齐单元测试中由 EF Core 生成的主键。
    /// </summary>
    private static InvitationRecord CreateInvitationRecord()
    {
        return InvitationRecord
            .Establish(InviterId, InviteeId, "ABCDEF123456")
            .WithId(new InvitationRecordId(Guid.NewGuid()));
    }

    /// <summary>
    /// 构造被测处理器与 IMediator 替身。
    /// </summary>
    private static (
        InvitationEstablishedDomainEventHandlerForSendInvitationReward Handler,
        RecordingMediator Mediator) CreateHandler(int invitationCount)
    {
        var mediator = new RecordingMediator(invitationCount);
        var handler = new InvitationEstablishedDomainEventHandlerForSendInvitationReward(
            mediator,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InvitationEstablishedDomainEventHandlerForSendInvitationReward>.Instance);
        return (handler, mediator);
    }

    /// <summary>
    /// 记录发送请求的 IMediator 替身：邀请人数查询返回指定人数，其余请求只记录不处理。
    /// </summary>
    private sealed class RecordingMediator(int invitationCount) : IMediator
    {
        /// <summary>
        /// 已发送的请求集合。
        /// </summary>
        public List<object> Requests { get; } = [];

        /// <summary>
        /// 已发送的命令集合。
        /// </summary>
        public List<object> Commands => Requests.Where(request => request is GrantPointsCommand).ToList();

        /// <inheritdoc />
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request is GetInviterInvitationCountQuery)
            {
                return Task.FromResult((TResponse)(object)new GetInviterInvitationCountResponse(invitationCount));
            }

            return Task.FromResult(default(TResponse)!);
        }

        /// <inheritdoc />
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Requests.Add(request!);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<object?>(null);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }
}
