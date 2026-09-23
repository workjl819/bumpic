namespace Bumpic.Domain;

/// <summary>
/// 邀请码生成器。
/// </summary>
public static class InvitationCodeGenerator
{
    /// <summary>
    /// 邀请码默认长度。
    /// </summary>
    public const int DefaultLength = 12;

    /// <summary>
    /// 邀请码字符集，去除易混淆字符 0、O、1、l、I。
    /// </summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// 生成随机邀请码。
    /// </summary>
    public static string Generate(int length = DefaultLength)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "邀请码长度必须大于 0");
        }

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return new string(chars);
    }
}
