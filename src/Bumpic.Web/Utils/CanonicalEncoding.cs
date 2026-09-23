using System.Buffers.Binary;
using System.Text;

namespace Bumpic.Web.Utils;

/// <summary>
/// 提供无歧义的版本化规范化编码，用于平台事实摘要和幂等请求摘要。
/// </summary>
internal static class CanonicalEncoding
{
    /// <summary>
    /// 规范化编码的算法版本。
    /// </summary>
    internal const int Version = 1;

    /// <summary>
    /// 写入可空字符串字段，空值与空字符串使用不同标记。
    /// </summary>
    internal static void WriteNullableString(Stream stream, string? value)
    {
        if (value is null)
        {
            stream.WriteByte(0);
            return;
        }

        stream.WriteByte(1);
        WriteString(stream, value);
    }

    /// <summary>
    /// 写入长度前缀的 UTF-8 字符串字段。
    /// </summary>
    internal static void WriteString(Stream stream, string value)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// 写入长度前缀的二进制字段。
    /// </summary>
    internal static void WriteBytes(Stream stream, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(value);
        WriteInt32(stream, value.Length);
        stream.Write(value, 0, value.Length);
    }

    /// <summary>
    /// 写入可空时间字段，统一转换为 UTC 毫秒。
    /// </summary>
    internal static void WriteNullableTimestamp(Stream stream, DateTimeOffset? value)
    {
        if (value is null)
        {
            stream.WriteByte(0);
            return;
        }

        stream.WriteByte(1);
        WriteTimestamp(stream, value.Value);
    }

    /// <summary>
    /// 写入 UTC 毫秒时间戳字段。
    /// </summary>
    internal static void WriteTimestamp(Stream stream, DateTimeOffset value)
    {
        WriteInt64(stream, value.ToUniversalTime().ToUnixTimeMilliseconds());
    }

    /// <summary>
    /// 写入可空整数字段。
    /// </summary>
    internal static void WriteNullableInt32(Stream stream, int? value)
    {
        if (value is null)
        {
            stream.WriteByte(0);
            return;
        }

        stream.WriteByte(1);
        WriteInt32(stream, value.Value);
    }

    /// <summary>
    /// 写入整数字段。
    /// </summary>
    internal static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    /// <summary>
    /// 写入长整数字段。
    /// </summary>
    internal static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }

    /// <summary>
    /// 写入布尔字段。
    /// </summary>
    internal static void WriteBoolean(Stream stream, bool value)
    {
        stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// 将 Base64Url 无填充字符串转换为字节数组。
    /// </summary>
    internal static byte[] DecodeBase64Url(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalized);
    }

    /// <summary>
    /// 将字节数组编码为 Base64Url 无填充字符串。
    /// </summary>
    internal static string EncodeBase64Url(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
