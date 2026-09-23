namespace Bumpic.Web.Application.Queries.PointAccount;

/// <summary>
/// 当前用户点数余额查询结果。
/// </summary>
public record GetPointBalanceResult(int AvailablePoints, int FrozenPoints);
