using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.Jobs;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Options;
using Bumpic.Web.Tests.Extensions;

namespace Bumpic.Web.Tests.Application.Jobs;

/// <summary>
/// 到期注销扫描任务单元测试。
/// </summary>
public class PurgeDeletedAccountsJobTests
{
    /// <summary>
    /// 扫描到的每个账户各派发一次注销命令，并带上宽限期阈值。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_PendingAccounts_DispatchesPurgeCommandPerAccount()
    {
        var first = new UserAccountId(Guid.NewGuid());
        var second = new UserAccountId(Guid.NewGuid());
        GetAccountsPendingDeletionQuery? capturedQuery = null;
        var mediator = new RecordingMediator(request =>
        {
            if (request is GetAccountsPendingDeletionQuery query)
            {
                capturedQuery = query;
                return new List<UserAccountId> { first, second };
            }

            return null;
        });
        var job = CreateJob(mediator);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.NotNull(capturedQuery);
        var commands = mediator.Commands.OfType<PurgeDeletedAccountCommand>().ToList();
        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, command => command.UserAccountId == first);
        Assert.Contains(commands, command => command.UserAccountId == second);
    }

    /// <summary>
    /// 没有到期账户时不派发任何命令。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NoPendingAccounts_DispatchesNothing()
    {
        var mediator = new RecordingMediator(request =>
            request is GetAccountsPendingDeletionQuery ? new List<UserAccountId>() : null);
        var job = CreateJob(mediator);

        await job.ExecuteAsync(CancellationToken.None);

        Assert.Empty(mediator.Commands);
    }

    /// <summary>
    /// 构造被测任务：宽限期取自配置项 AccountDeletion:GracePeriod。
    /// </summary>
    private static PurgeDeletedAccountsJob CreateJob(RecordingMediator mediator)
    {
        return new PurgeDeletedAccountsJob(
            mediator,
            Microsoft.Extensions.Options.Options.Create(
                new AccountDeletionOptions { GracePeriod = TimeSpan.FromMinutes(5) }));
    }
}
