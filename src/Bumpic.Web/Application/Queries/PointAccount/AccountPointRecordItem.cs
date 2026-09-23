using Bumpic.Domain.AggregateModel.PointAccountAggregate;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Application.Queries.PointAccount;

/// <summary>
/// 用户点数流水展示项。
/// </summary>
/// <param name="AccountPointRecordId">点数流水标识。</param>
/// <param name="Type">点数流水类型。</param>
/// <param name="Amount">点数变化绝对值。</param>
/// <param name="IsPositive">是否为正向点数流水。</param>
/// <param name="CreatedAt">流水创建时间。</param>
public record AccountPointRecordItem(
    AccountPointRecordId AccountPointRecordId,
    AccountPointRecordType Type,
    int Amount,
    bool IsPositive,
    DateTimeOffset CreatedAt);
