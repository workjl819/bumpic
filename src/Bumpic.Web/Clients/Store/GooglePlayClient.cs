using Google;
using Google.Apis.AndroidPublisher.v3;
using Microsoft.Extensions.Options;
using Bumpic.Web.Options;

namespace Bumpic.Web.Clients.Store;

/// <summary>
/// 基于官方 Google API 客户端的 Play Developer API 验单和消费实现。
/// </summary>
public sealed class GooglePlayClient : IGooglePlayClient, IDisposable
{
    private readonly GooglePlayServiceFactory _serviceFactory;
    private readonly IOptions<GooglePlayOptions> _options;
    private readonly Lock _serviceLock = new();
    private AndroidPublisherService? _service;
    private bool _disposed;

    /// <summary>
    /// 初始化 Play Developer API 客户端。
    /// </summary>
    public GooglePlayClient(
        GooglePlayServiceFactory serviceFactory,
        IOptions<GooglePlayOptions> options)
    {
        _serviceFactory = serviceFactory;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<GooglePlayPurchase> GetPurchaseAsync(
        GooglePlayPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PurchaseToken);
        var service = ResolveService();
        try
        {
            var purchase = await service.Purchases.Productsv2
                .Getproductpurchasev2(
                    packageName: ResolvePackageName(),
                    token: request.PurchaseToken)
                .ExecuteAsync(cancellationToken);
            return GooglePlayPurchaseMapper.Map(purchase: purchase);
        }
        catch (GoogleApiException exception)
        {
            throw GooglePlayApiErrorTranslator.TranslatePurchaseQueryFailure(exception: exception);
        }
        catch (Exception exception) when (IsTransportFailure(
                     exception: exception,
                     cancellationToken: cancellationToken))
        {
            throw GooglePlayApiErrorTranslator.TranslateTransportFailure(exception: exception);
        }
    }

    /// <inheritdoc />
    public async Task ConsumeAsync(
        GooglePlayConsumptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PurchaseToken);
        var service = ResolveService();
        try
        {
            await service.Purchases.Products
                .Consume(
                    packageName: ResolvePackageName(),
                    productId: request.ProductId,
                    token: request.PurchaseToken)
                .ExecuteAsync(cancellationToken);
        }
        catch (GoogleApiException exception)
        {
            throw GooglePlayApiErrorTranslator.TranslateConsumptionFailure(exception: exception);
        }
        catch (Exception exception) when (IsTransportFailure(
                     exception: exception,
                     cancellationToken: cancellationToken))
        {
            throw GooglePlayApiErrorTranslator.TranslateTransportFailure(exception: exception);
        }
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception is HttpRequestException
               || (exception is OperationCanceledException
                   && !cancellationToken.IsCancellationRequested);
    }

    private AndroidPublisherService ResolveService()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_service is not null)
        {
            return _service;
        }

        lock (_serviceLock)
        {
            // 凭据缺失时保持可重试失败，不缓存异常，配置补齐后无需重启即可恢复。
            return _service ??= _serviceFactory.Create();
        }
    }

    private string ResolvePackageName()
    {
        return _options.Value.PackageName.Trim();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_serviceLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _service?.Dispose();
            _service = null;
        }
    }
}
