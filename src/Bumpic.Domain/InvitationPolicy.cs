namespace Bumpic.Domain;

/// <summary>
/// 邀请策略。
/// </summary>
public static class InvitationPolicy
{
    /// <summary>
    /// 单个账号最多可邀请的人数。
    /// </summary>
    /// <remarks>
    /// 按邀请人的邀请奖励流水（<c>InvitationGranted</c>）计数：每个受邀用户只建立一次邀请关系且只发放一次奖励，
    /// 因此流水条数等于有效邀请人数。
    /// </remarks>
    public const int MaxInvitationsPerAccount = 3;

    /// <summary>
    /// 邀请关系建立时发放给邀请人的点数（产品规则，受邀者不发放邀请积分）。
    /// </summary>
    public const int RewardPoints = 5;
}
