using System.Text.Json;
using System.Text.Json.Serialization;
using Bumpic.Domain.Enums;

namespace Bumpic.Web.Utils;

/// <summary>
/// 验证码用途的 JSON 转换器：接受 Register/Login 字符串（大小写不敏感）与兼容数字 1/2，
/// 取值非法时返回可读文案，避免默认转换器把 .NET 命名空间与内部类型名写进错误响应。
/// </summary>
public class OperationTypeJsonConverter : JsonConverter<OperationType>
{
    /// <summary>
    /// 取值非法时的错误文案。
    /// </summary>
    public const string InvalidValueMessage = "purpose 只能为 Register 或 Login";

    /// <inheritdoc />
    public override OperationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (Enum.TryParse<OperationType>(value, ignoreCase: true, out var parsed)
                && parsed is OperationType.Register or OperationType.Login)
            {
                return parsed;
            }

            throw new JsonException(InvalidValueMessage);
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
        {
            var parsed = (OperationType)numeric;
            if (parsed is OperationType.Register or OperationType.Login)
            {
                return parsed;
            }
        }

        throw new JsonException(InvalidValueMessage);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OperationType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
