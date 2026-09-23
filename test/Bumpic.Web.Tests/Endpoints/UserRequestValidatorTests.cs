using System.Text.Json;
using FluentValidation.TestHelper;
using Bumpic.Domain.Enums;
using Bumpic.Web.Endpoints.Authentication;
using Bumpic.Web.Endpoints.Registration;

namespace Bumpic.Web.Tests.Endpoints;

/// <summary>
/// 用户认证与注册端点请求验证器测试：字段缺失或取值非法时必须在进入业务逻辑前被拦截。
/// </summary>
public class UserRequestValidatorTests
{
    private readonly SendEmailCodeRequestValidator _sendEmailCodeValidator = new();
    private readonly RegisterWithEmailRequestValidator _registerValidator = new();
    private readonly LoginWithEmailRequestValidator _loginValidator = new();
    private readonly CheckEmailRegisteredRequestValidator _checkEmailValidator = new();
    private readonly LoginWithAppleExternalIdentityRequestValidator _appleValidator = new();
    private readonly LoginWithGoogleExternalIdentityRequestValidator _googleValidator = new();

    /// <summary>
    /// 发送验证码：邮箱与 purpose 均合法时通过。
    /// </summary>
    [Theory]
    [InlineData(OperationType.Register)]
    [InlineData(OperationType.Login)]
    public void SendEmailCode_ValidRequest_HasNoErrors(OperationType purpose)
    {
        var result = _sendEmailCodeValidator.TestValidate(CreateSendEmailCodeRequest(purpose));

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// 发送验证码：purpose 缺省时反序列化为 Unknown，仅靠 [Required] 拦不住，必须由验证器拦截。
    /// </summary>
    [Fact]
    public void SendEmailCode_UnknownPurpose_HasPurposeError()
    {
        var result = _sendEmailCodeValidator.TestValidate(CreateSendEmailCodeRequest(OperationType.Unknown));

        result.ShouldHaveValidationErrorFor(x => x.Purpose)
            .WithErrorMessage("purpose 只能为 Register 或 Login");
    }

    /// <summary>
    /// 发送验证码：邮箱缺失、格式错误或超长时报错。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("user@example.com@example.com")]
    public void SendEmailCode_InvalidEmail_HasEmailError(string emailAddress)
    {
        var request = CreateSendEmailCodeRequest(OperationType.Register) with { EmailAddress = emailAddress };

        var result = _sendEmailCodeValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EmailAddress);
    }

    /// <summary>
    /// 发送验证码：邮箱超长时报错。
    /// </summary>
    [Fact]
    public void SendEmailCode_TooLongEmail_HasEmailError()
    {
        var request = CreateSendEmailCodeRequest(OperationType.Register)
            with { EmailAddress = new string('a', 320) + "@example.com" };

        var result = _sendEmailCodeValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EmailAddress);
    }

    /// <summary>
    /// 发送验证码：设备标识超长时报错。
    /// </summary>
    [Fact]
    public void SendEmailCode_TooLongDeviceId_HasDeviceError()
    {
        var request = CreateSendEmailCodeRequest(OperationType.Register)
            with { DeviceId = new string('d', 129) };

        var result = _sendEmailCodeValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.DeviceId);
    }

    /// <summary>
    /// 邮箱注册：合法请求通过。
    /// </summary>
    [Fact]
    public void Register_ValidRequest_HasNoErrors()
    {
        var result = _registerValidator.TestValidate(new RegisterWithEmailRequest
        {
            EmailAddress = "user@example.com",
            EmailCode = "123456",
            InvitationCode = "ABCD2345EFGH"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// 邮箱注册：邮箱、验证码缺失或邀请码超长时报错。
    /// </summary>
    [Theory]
    [InlineData("", "123456", null)]
    [InlineData("not-an-email", "123456", null)]
    [InlineData("user@example.com", "", null)]
    [InlineData("user@example.com", "123456", "TOO-LONG-INVITATION-CODE")]
    public void Register_InvalidRequest_HasErrors(string emailAddress, string emailCode, string? invitationCode)
    {
        var request = new RegisterWithEmailRequest
        {
            EmailAddress = emailAddress,
            EmailCode = emailCode,
            InvitationCode = invitationCode is null ? null : new string('A', 65)
        };

        var result = _registerValidator.TestValidate(request);

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// 邮箱登录：合法请求通过；邮箱或验证码为空时报错。
    /// </summary>
    [Theory]
    [InlineData("user@example.com", "123456", true)]
    [InlineData("", "123456", false)]
    [InlineData("user@example.com", "", false)]
    [InlineData("not-an-email", "123456", false)]
    public void Login_Request_ValidatesEmailAndCode(string emailAddress, string emailCode, bool expectedValid)
    {
        var result = _loginValidator.TestValidate(new LoginWithEmailRequest
        {
            EmailAddress = emailAddress,
            EmailCode = emailCode
        });

        Assert.Equal(expectedValid, result.IsValid);
    }

    /// <summary>
    /// 邮箱是否已注册：邮箱为空或格式错误时报错。
    /// </summary>
    [Fact]
    public void CheckEmailRegistered_InvalidEmail_HasEmailError()
    {
        var result = _checkEmailValidator.TestValidate(new CheckEmailRegisteredRequest
        {
            EmailAddress = string.Empty
        });

        result.ShouldHaveValidationErrorFor(x => x.EmailAddress);
    }

    /// <summary>
    /// Apple 快捷登录：令牌缺失时报错，不进入外部身份校验。
    /// </summary>
    [Fact]
    public void AppleLogin_MissingToken_HasTokenError()
    {
        var result = _appleValidator.TestValidate(new LoginWithAppleExternalIdentityRequest());

        result.ShouldHaveValidationErrorFor(x => x.Token);
    }

    /// <summary>
    /// Apple 快捷登录：仅带令牌的合法请求通过（nonce 与授权码为可选项）。
    /// </summary>
    [Fact]
    public void AppleLogin_TokenOnly_HasNoErrors()
    {
        var result = _appleValidator.TestValidate(new LoginWithAppleExternalIdentityRequest
        {
            Token = "header.payload.signature"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Apple 快捷登录：令牌超长时报错。
    /// </summary>
    [Fact]
    public void AppleLogin_TooLongToken_HasTokenError()
    {
        var result = _appleValidator.TestValidate(new LoginWithAppleExternalIdentityRequest
        {
            Token = new string('t', 8193)
        });

        result.ShouldHaveValidationErrorFor(x => x.Token);
    }

    /// <summary>
    /// Google 快捷登录：令牌缺失时报错，不进入外部身份校验。
    /// </summary>
    [Fact]
    public void GoogleLogin_MissingToken_HasTokenError()
    {
        var result = _googleValidator.TestValidate(new LoginWithGoogleExternalIdentityRequest());

        result.ShouldHaveValidationErrorFor(x => x.Token);
    }

    /// <summary>
    /// Google 快捷登录：仅带令牌的合法请求通过。
    /// </summary>
    [Fact]
    public void GoogleLogin_TokenOnly_HasNoErrors()
    {
        var result = _googleValidator.TestValidate(new LoginWithGoogleExternalIdentityRequest
        {
            Token = "header.payload.signature"
        });

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static SendEmailCodeRequest CreateSendEmailCodeRequest(OperationType purpose)
    {
        return new SendEmailCodeRequest
        {
            EmailAddress = "user@example.com",
            Purpose = purpose,
            DeviceId = "device-1"
        };
    }
}

/// <summary>
/// 验证码用途 JSON 转换器测试：非法取值必须给出可读文案，且不得回退到数字缺省值。
/// </summary>
public class OperationTypeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new Bumpic.Web.Utils.OperationTypeJsonConverter() }
    };

    /// <summary>
    /// 字符串写法大小写不敏感，兼容旧客户端的数字写法 1/2。
    /// </summary>
    [Theory]
    [InlineData("\"Register\"", OperationType.Register)]
    [InlineData("\"register\"", OperationType.Register)]
    [InlineData("\"REGISTER\"", OperationType.Register)]
    [InlineData("\"Login\"", OperationType.Login)]
    [InlineData("\"login\"", OperationType.Login)]
    [InlineData("1", OperationType.Register)]
    [InlineData("2", OperationType.Login)]
    public void Deserialize_ValidValue_ReturnsOperationType(string json, OperationType expected)
    {
        var parsed = JsonSerializer.Deserialize<OperationType>(json, Options);

        Assert.Equal(expected, parsed);
    }

    /// <summary>
    /// 非法取值统一返回可读文案，不泄漏 .NET 类型名与命名空间。
    /// </summary>
    [Theory]
    [InlineData("\"Unknown\"")]
    [InlineData("\"0\"")]
    [InlineData("\"3\"")]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("true")]
    [InlineData("null")]
    public void Deserialize_InvalidValue_ThrowsReadableMessage(string json)
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<OperationType>(json, Options));

        Assert.Equal(Bumpic.Web.Utils.OperationTypeJsonConverter.InvalidValueMessage, exception.Message);
        Assert.DoesNotContain("Bumpic", exception.Message);
    }

    /// <summary>
    /// 序列化输出字符串名称，便于前端按名称判断用途。
    /// </summary>
    [Fact]
    public void Serialize_WritesEnumName()
    {
        Assert.Equal("\"Register\"", JsonSerializer.Serialize(OperationType.Register, Options));
        Assert.Equal("\"Login\"", JsonSerializer.Serialize(OperationType.Login, Options));
    }

    /// <summary>
    /// 通过请求模型反序列化时同样生效（端点实际使用路径）。
    /// </summary>
    [Fact]
    public void Deserialize_SendEmailCodeRequest_AppliesConverter()
    {
        var request = JsonSerializer.Deserialize<SendEmailCodeRequest>(
            "{\"emailAddress\":\"user@example.com\",\"purpose\":\"login\"}",
            new JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                Converters = { new Bumpic.Web.Utils.OperationTypeJsonConverter() }
            });

        Assert.NotNull(request);
        Assert.Equal(OperationType.Login, request!.Purpose);
    }
}
