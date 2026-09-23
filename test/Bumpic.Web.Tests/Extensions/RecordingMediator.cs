using MediatR;

namespace Bumpic.Web.Tests.Extensions;

/// <summary>
/// 记录发送请求的 IMediator 替身；可通过 responder 为查询等请求返回值。
/// </summary>
internal sealed class RecordingMediator(Func<object, object?>? responder = null) : IMediator
{
    /// <summary>
    /// 已发送的请求集合。
    /// </summary>
    public List<object> Requests { get; } = [];

    /// <summary>
    /// 已发送的命令集合（按类型名以 Command 结尾识别，避免依赖接口的程序集解析）。
    /// </summary>
    public List<object> Commands => Requests
        .Where(request => request.GetType().Name.EndsWith("Command", StringComparison.Ordinal))
        .ToList();

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        var response = responder?.Invoke(request);
        return Task.FromResult(response is TResponse typed ? typed : default(TResponse)!);
    }

    /// <inheritdoc />
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        Requests.Add(request!);
        responder?.Invoke(request!);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(responder?.Invoke(request));
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
