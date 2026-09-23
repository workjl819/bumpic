using Microsoft.IdentityModel.Tokens;

namespace Bumpic.Web.Options;

/// <summary>
/// Jwt 密钥配置
/// </summary>
public class JsonWebKeysOptions
{
    public List<JsonWebKey> Keys { get; set; } = [];
}
