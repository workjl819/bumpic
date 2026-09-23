using FastEndpoints;
using Microsoft.AspNetCore.Authorization;
using NetCorePal.Extensions.Dto;
using Bumpic.Domain.AggregateModel.UserAccountAggregate;
using Bumpic.Domain.Enums;
using Bumpic.Web.Application.Commands.StorePurchase;
using Bumpic.Web.Shared;
using Bumpic.Web.Utils;

namespace Bumpic.Web.Endpoints.StorePurchase;

/// <summary>
/// 商店验单请求。
/// </summary>
public record VerifyStorePurchaseRequest(
    AppStore Store,
    string ProductId,
    string? TransactionId,
    string? TransactionJws,
    string? PurchaseToken,
    string? AppAccountToken,
    string? ObfuscatedAccountId);

/// <summary>
/// 商店验单响应。
/// </summary>
public record VerifyStorePurchaseResponse(
    string? StoreTransactionId,
    string? Status,
    string? ProductId,
    int GrantedPoints,
    int ReversedPoints,
    string? Code,
    int? RetryAfterSeconds,
    int AvailablePoints,
    int FrozenPoints);

/// <summary>
/// 验证商店购买、消费并完成点数入账。
/// </summary>
[HttpPost("/api/v1/store-transaction/verify")]
[Authorize(Policy = PolicyNames.Client)]
[Tags("StorePurchase")]
public class VerifyStorePurchaseEndpoint(IMediator mediator, ILoginUser loginUser)
    : Endpoint<VerifyStorePurchaseRequest, ResponseData<VerifyStorePurchaseResponse>>
{
    private const int OrderConflictStatusCode = 409;
    private const int RetryableStatusCode = 503;
    private const int ProcessingStatusCode = 202;

    /// <summary>
    /// 幂等验单并按结果返回交易状态或可重试结果。
    /// </summary>
    public override async Task HandleAsync(
        VerifyStorePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = HttpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("N");
        }

        var userAccountId = new UserAccountId(loginUser.Id);
        var result = await mediator.Send(new VerifyStorePurchaseCommand(
            UserAccountId: userAccountId,
            Store: request.Store,
            ProductId: request.ProductId,
            TransactionId: request.TransactionId,
            TransactionJws: request.TransactionJws,
            PurchaseToken: request.PurchaseToken,
            ExternalAccountToken: request.AppAccountToken ?? request.ObfuscatedAccountId,
            IdempotencyKey: idempotencyKey), cancellationToken);
        var response = new VerifyStorePurchaseResponse(
            StoreTransactionId: result.StoreTransactionId?.Id.ToString(),
            Status: result.Status?.ToString(),
            ProductId: result.ProductId,
            GrantedPoints: result.GrantedPoints,
            ReversedPoints: result.ReversedPoints,
            Code: result.FailureCode,
            RetryAfterSeconds: result.RetryAfterSeconds > 0 ? result.RetryAfterSeconds : null,
            AvailablePoints: result.AvailablePoints,
            FrozenPoints: result.FrozenPoints);

        switch (result.Outcome)
        {
            case StorePurchaseVerificationOutcome.Processing:
                SetRetryAfterHeader(seconds: result.RetryAfterSeconds);
                await Send.ResponseAsync(
                    response.AsSuccessResponseData(),
                    statusCode: ProcessingStatusCode,
                    cancellation: cancellationToken);
                return;
            case StorePurchaseVerificationOutcome.RetryableFailed:
                SetRetryAfterHeader(seconds: result.RetryAfterSeconds);
                await Send.ResponseAsync(
                    response.AsSuccessResponseData(),
                    statusCode: RetryableStatusCode,
                    cancellation: cancellationToken);
                return;
            case StorePurchaseVerificationOutcome.DeterministicFailed:
                await Send.ResponseAsync(
                    response.AsSuccessResponseData(),
                    statusCode: ResolveFailureStatusCode(failureCode: result.FailureCode),
                    cancellation: cancellationToken);
                return;
        }

        await Send.OkAsync(response.AsSuccessResponseData(), cancellation: cancellationToken);
    }

    private void SetRetryAfterHeader(int seconds)
    {
        HttpContext.Response.Headers.RetryAfter = Math.Max(seconds, 1).ToString();
    }

    private static int ResolveFailureStatusCode(string? failureCode)
    {
        return failureCode is "PURCHASE_ACCOUNT_MISMATCH"
            or "PURCHASE_ALREADY_CLAIMED"
            or "IDEMPOTENCY_KEY_REUSED"
            ? OrderConflictStatusCode
            : 400;
    }
}
