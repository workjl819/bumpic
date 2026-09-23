# UserAccount 与 UserExternalIdentity 迁移说明

来源：`/Users/jianglei/cita/photo-rescue`。本次仅迁移账户与认证流程；邀请、积分、照片、修复任务和商店的事件处理器未迁移。

## 业务链路

| 入口 | 领域行为 | 存储与后续动作 |
| --- | --- | --- |
| 邮箱注册与登录 | 规范化邮箱、验证码核销、失败锁定、记录登录 | `UserAccount`；签发 JWT 与 Redis 刷新会话 |
| Apple/Google 登录 | 校验平台签名与声明、查询绑定、按已验证邮箱绑定或注册 | `UserExternalIdentity`；签发会话 |
| 刷新与退出 | 校验并轮换或撤销刷新令牌 | Redis 只保存令牌摘要 |
| 当前用户查询 | 返回账户与已绑定平台 | 只读查询两个 DbSet |
| 申请注销 | 进入宽限期；再次登录自动取消 | Apple 撤销令牌加密存入外部身份 |
| 到期注销 | 定时扫描并软删除账户与身份 | 集成事件撤销 Apple 授权 |

## 范围与差异

- 迁移了两个聚合、状态/提供方枚举、EF 配置与迁移、仓储、账号命令/查询、认证与注册端点、邮箱验证码、平台令牌校验、会话令牌和注销作业。
- 注册接口不再接收邀请码；快捷登录不再返回邀请补交窗口。领域模型保留来源中的邀请码和购买账户标识字段以保持原始账户结构，但本次不执行邀请或商店业务。
- 修复了到期注销后无法清除 Apple 撤销令牌密文的问题，并使登录取消注销与到期删除使用相同的账户命令锁。平台撤销凭据获取或 Google 撤销失败时，注销申请会返回错误，避免留下无法自动重试的授权。
- 不启用来源项目的固定审核验证码，也不复制其邮件或平台密钥。验证码不会写入日志。

## 部署配置

- `Email:SenderAddress`、`Email:EmailServerHost`、`Email:EmailServerPort` 及需要时的 SMTP 凭据。
- `ExternalIdentity:Apple:AllowedAudiences`、`ExternalIdentity:Google:AllowedAudiences`；Apple 撤销功能还需要 TeamId、KeyId、ClientId、AuthKeyPrivateKeyPem。
- `Jwt` 签名密钥及签发参数、Redis、MySQL、RabbitMQ 与 Hangfire 沿用 bumpic 配置。
- 应用数据库需应用 `AddUserAccountsAndExternalIdentities` 迁移。空平台受众列表会使对应平台令牌校验失败。
