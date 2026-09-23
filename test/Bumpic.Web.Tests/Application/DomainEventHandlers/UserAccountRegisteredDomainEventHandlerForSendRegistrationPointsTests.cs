using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.DomainEvents;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.Points;
using Bumpic.Web.Application.DomainEventHandlers;
using Bumpic.Web.Options;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.DomainEventHandlers;

/// <summary>
/// 注册赠点发放的领域事件处理器单元测试。
/// </summary>
public class UserAccountRegisteredDomainEventHandlerForSendRegistrationPointsTests
{
    /// <summary>
    /// 测试用配置注册赠点（配置项 RewardPoints:Registration，默认 10）。
    /// </summary>
    private const int ConfiguredRegistrationPoints = 12;

    /// <summary>
    /// 按配置点数发放，业务引用取账户标识（同一账户只发放一次）。
    /// </summary>
    [Fact]
    public async Task Handle_Registered_GrantsConfiguredPointsWithAccountScopedReference()
    {
        // 领域模型不手动赋主键（由 EF 生成），单元测试用 WithId 补齐以便断言业务引用。
        var user = UserAccount.Register("grant@example.com").WithId(new UserAccountId(Guid.NewGuid()));
        var (handler, mediator) = CreateHandler();

        await handler.Handle(new UserAccountRegisteredDomainEvent(user), CancellationToken.None);

        var command = Assert.IsType<GrantPointsCommand>(Assert.Single(mediator.Commands));
        Assert.Equal(ConfiguredRegistrationPoints, command.Points);
        Assert.Equal(AccountPointRecordType.RegistrationGranted, command.RecordType);
        Assert.Equal("registration:" + user.Id.Id, command.BusinessReference);
    }

    /// <summary>
    /// 同一邮箱注销后重新注册得到新账户标识，业务引用不同，因此会重新发放赠点。
    /// </summary>
    [Fact]
    public async Task Handle_ReRegisteredSameEmail_UsesDifferentReference()
    {
        var first = UserAccount.Register("again@example.com").WithId(new UserAccountId(Guid.NewGuid()));
        var second = UserAccount.Register("again@example.com").WithId(new UserAccountId(Guid.NewGuid()));

        var (firstHandler, firstMediator) = CreateHandler();
        var (secondHandler, secondMediator) = CreateHandler();
        await firstHandler.Handle(new UserAccountRegisteredDomainEvent(first), CancellationToken.None);
        await secondHandler.Handle(new UserAccountRegisteredDomainEvent(second), CancellationToken.None);

        var firstCommand = Assert.IsType<GrantPointsCommand>(Assert.Single(firstMediator.Commands));
        var secondCommand = Assert.IsType<GrantPointsCommand>(Assert.Single(secondMediator.Commands));
        Assert.NotEqual(firstCommand.BusinessReference, secondCommand.BusinessReference);
        Assert.Equal("registration:" + first.Id.Id, firstCommand.BusinessReference);
        Assert.Equal("registration:" + second.Id.Id, secondCommand.BusinessReference);
    }

    private static (
        UserAccountRegisteredDomainEventHandlerForSendRegistrationPoints Handler,
        RecordingMediator Mediator) CreateHandler()
    {
        var mediator = new RecordingMediator();
        var options = Microsoft.Extensions.Options.Options.Create(
            new RewardPointsOptions { Registration = ConfiguredRegistrationPoints });
        return (new UserAccountRegisteredDomainEventHandlerForSendRegistrationPoints(mediator, options), mediator);
    }
}
