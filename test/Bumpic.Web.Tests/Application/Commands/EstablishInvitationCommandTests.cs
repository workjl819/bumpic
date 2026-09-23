using Moq;
using Bumpic.Domain.AggregateModel.InvitationRecordAggregate;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Infrastructure.Repositories;
using Bumpic.Web.Application.Commands.Invitation;
using Bumpic.Web.Options;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.Commands;

/// <summary>
/// 建立邀请关系命令处理器单元测试（邀请码补交窗口与注册流程共用同一命令）。
/// </summary>
public class EstablishInvitationCommandTests
{
    private static readonly UserAccountId InviteeId = new(Guid.NewGuid());

    /// <summary>
    /// 测试用配置奖励点数（配置项 RewardPoints:Invitation，默认 5）。
    /// </summary>
    private const int ConfiguredRewardPoints = 7;

    /// <summary>
    /// 邀请码有效且受邀者尚未绑定邀请人时建立关系并返回成功。
    /// </summary>
    [Fact]
    public async Task Handle_ValidCode_EstablishesInvitation()
    {
        var inviter = UserAccount.Register("inviter@example.com");
        var (handler, invitationRepository) = CreateHandler(inviter: inviter, existingInvitation: null);

        var result = await handler.Handle(new EstablishInvitationCommand(InviteeId, "ABCDEF123456"), CancellationToken.None);

        Assert.True(result.Established);
        Assert.Null(result.Reason);
        invitationRepository.Verify(
            repository => repository.AddAsync(It.IsAny<InvitationRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // 奖励点数来自配置项 Invitation:RewardPoints，并写入邀请记录供后续发放使用。
        var added = Assert.IsType<InvitationRecord>(Assert.Single(invitationRepository.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IInvitationRecordRepository.AddAsync))
            .Select(invocation => invocation.Arguments[0])));
        Assert.Equal(ConfiguredRewardPoints, added.RewardPoints);
    }

    /// <summary>
    /// 邀请码为空时返回原因码且不建立关系。
    /// </summary>
    [Fact]
    public async Task Handle_EmptyCode_ReturnsRequiredReason()
    {
        var (handler, _) = CreateHandler(inviter: null, existingInvitation: null);

        var result = await handler.Handle(new EstablishInvitationCommand(InviteeId, "  "), CancellationToken.None);

        Assert.False(result.Established);
        Assert.Equal("INVITATION_CODE_REQUIRED", result.Reason);
    }

    /// <summary>
    /// 邀请码不存在时返回原因码，便于补交窗口把失败原因反馈给前端。
    /// </summary>
    [Fact]
    public async Task Handle_UnknownCode_ReturnsInvalidReason()
    {
        var (handler, _) = CreateHandler(inviter: null, existingInvitation: null);

        var result = await handler.Handle(new EstablishInvitationCommand(InviteeId, "NOTEXIST12345"), CancellationToken.None);

        Assert.False(result.Established);
        Assert.Equal("INVITATION_CODE_INVALID", result.Reason);
    }

    /// <summary>
    /// 不能邀请自己。
    /// </summary>
    [Fact]
    public async Task Handle_SelfInvitation_ReturnsSelfReason()
    {
        var inviter = UserAccount.Register("self@example.com");
        var (handler, _) = CreateHandler(inviter: inviter, existingInvitation: null);

        var result = await handler.Handle(new EstablishInvitationCommand(inviter.Id, inviter.InvitationCode), CancellationToken.None);

        Assert.False(result.Established);
        Assert.Equal("INVITATION_SELF", result.Reason);
    }

    /// <summary>
    /// 受邀者已有邀请关系时不再重复建立。
    /// </summary>
    [Fact]
    public async Task Handle_AlreadyBound_ReturnsAlreadyBoundReason()
    {
        var inviter = UserAccount.Register("inviter2@example.com");
        var existing = InvitationRecord.Establish(
            inviterUserAccountId: inviter.Id,
            inviteeUserAccountId: InviteeId,
            invitationCode: "ABCDEF123456");
        var (handler, invitationRepository) = CreateHandler(inviter: inviter, existingInvitation: existing);

        var result = await handler.Handle(new EstablishInvitationCommand(InviteeId, "ABCDEF123456"), CancellationToken.None);

        Assert.False(result.Established);
        Assert.Equal("INVITATION_ALREADY_BOUND", result.Reason);
        invitationRepository.Verify(
            repository => repository.AddAsync(It.IsAny<InvitationRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// 邀请人已达奖励上限时依然建立邀请关系（上限只限制奖励发放）。
    /// </summary>
    [Fact]
    public async Task Handle_InviterReachedLimit_StillEstablishesInvitation()
    {
        var inviter = UserAccount.Register("capped@example.com");
        var (handler, invitationRepository) = CreateHandler(inviter: inviter, existingInvitation: null);

        var result = await handler.Handle(new EstablishInvitationCommand(InviteeId, "ABCDEF123456"), CancellationToken.None);

        Assert.True(result.Established);
        Assert.Null(result.Reason);
        invitationRepository.Verify(
            repository => repository.AddAsync(It.IsAny<InvitationRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (EstablishInvitationCommandHandler Handler, Mock<IInvitationRecordRepository> InvitationRepository) CreateHandler(
        UserAccount? inviter,
        InvitationRecord? existingInvitation)
    {
        var userRepository = new Mock<IUserAccountRepository>();
        userRepository
            .Setup(repository => repository.FindByInvitationCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inviter);

        var invitationRepository = new Mock<IInvitationRecordRepository>();
        invitationRepository
            .Setup(repository => repository.FindByInviteeAsync(It.IsAny<UserAccountId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvitation);
        invitationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<InvitationRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationRecord entity, CancellationToken _) => entity);

        var options = Microsoft.Extensions.Options.Options.Create(
            new RewardPointsOptions { Invitation = ConfiguredRewardPoints });

        return (
            new EstablishInvitationCommandHandler(
                userRepository.Object,
                invitationRepository.Object,
                options),
            invitationRepository);
    }
}
