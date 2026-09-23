using Microsoft.Extensions.Options;
using Bumpic.Web.Application.Commands.Authentication;
using Bumpic.Web.Application.Queries.Authentication;
using Bumpic.Web.Options;

namespace Bumpic.Web.Application.Jobs;

/// <summary>
/// 扫描注销宽限期已过的账户并执行注销：软删除账户自身，由领域事件处理器负责解绑外部身份。
/// </summary>
public sealed class PurgeDeletedAccountsJob(
    IMediator mediator,
    IOptions<AccountDeletionOptions> accountDeletionOptions)
{
    /// <summary>
    /// 单批处理的账户数量。
    /// </summary>
    private const int BatchSize = 50;

    /// <summary>
    /// 执行注销扫描。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var requestedBefore = DateTimeOffset.UtcNow.Subtract(accountDeletionOptions.Value.GracePeriod);
        var userAccountIds = await mediator.Send(
            new GetAccountsPendingDeletionQuery(requestedBefore, BatchSize),
            cancellationToken);

        foreach (var userAccountId in userAccountIds)
        {
            await mediator.Send(new PurgeDeletedAccountCommand(userAccountId), cancellationToken);
        }
    }
}
