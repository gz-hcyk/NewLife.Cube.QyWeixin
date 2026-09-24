# NewLife.Cube.QyWeixin

企业微信扩展 for [NewLife.Cube 魔方](https://github.com/NewLifeX/NewLife.Cube) 框架。把企微通用能力（应用消息发送、通讯录同步、接收消息回调）封装为独立类库，任何魔方项目装包即用，零业务依赖、不改框架源码（基于魔方 `QyWeiXin` 的 `protected virtual` 派生扩展）。

## 功能

| 组件 | 职责 |
|---|---|
| `QyWeiXinSender` | 应用消息发送：`message/send`（text / textcard / template_card）+ `update_taskcard`（按钮终态回执） |
| `QyWeixinCredential` / `QyWeixinOptions` | 凭据解析：显式参数 ＞ 魔方 OAuth 配置（企业微信，AppId 写 `corp#agent` 复合写法同时提供 CorpId+AgentId） |
| `QyWeixinService` | 通讯录同步定时作业（`[CronJob("QyWeixinService")]`，企微 userid→User.Name、部门 Id→Department.Code，魔方 JobService 自动扫描，无需手工注册） |
| `QyWeixinCallbackController` | 通用「接收消息」回调端点 `/QyWeixin/Callback`：GET 解密 echostr 回显（URL 验证），POST 验签解密后分发事件 |
| `QyWeixinCrypt` | WXBizMsgCrypt 验签加解密（SHA1 + AES-256-CBC + receiveid 校验），零第三方依赖 |
| `IQyWeixinEventHandler` / `QyWeixinEvents` | 回调事件扩展点：实现接口、启动时 `QyWeixinEvents.Handlers.Add(...)`，首个返回 true 的处理器消费事件 |
| `QyWeixinCallbackSetting` | `[Config("QyWeixinCallback")]`：Enable 总开关 + Token + EncodingAESKey（与企微后台「接收消息」配置一致） |

## 快速开始

1. 安装包（或项目引用）：

   ```xml
   <PackageReference Include="NewLife.Cube.QyWeixin" Version="1.0.*" />
   ```

2. 魔方 OAuth 配置加企业微信条目：AppId 写 `corp#agent` 复合写法、Secret 填应用 Secret —— 之后发消息与通讯录同步零额外配置。
3. （可选）回调：企微后台「应用 → 接收消息」URL 填 `https://域名/QyWeixin/Callback`，随机 Token/EncodingAESKey 抄入 `QyWeixinCallback` 配置并置 `Enable=true`；业务事件实现 `IQyWeixinEventHandler` 并在启动时注册：

   ```csharp
   QyWeixinEvents.Handlers.Add(new MyEventHandler());
   ```

## 打包

```bash
dotnet pack -c Release
# 产物：nupkg/NewLife.Cube.QyWeixin.<版本>.nupkg
```

把 `nupkg/` 目录加为 NuGet 本地源（`nuget sources add` 或在 NuGet.config 配 `<packageSources>`），其他项目即可 `Version="1.0.*"` 浮动引用。

## 依赖

- `NewLife.Cube.Core` 6.15+（传递引入 NewLife.Core / XCode / Membership）
- `Microsoft.AspNetCore.App` 框架引用（回调端点）

## 参考实现

智能锁管理平台（明佳 IoT）的告警推送即基于本扩展：`AlertPushJob` 用 `QyWeiXinSender` 发模板卡片告警，`AlertCardEventHandler` 实现 `IQyWeixinEventHandler` 完成「标记已处理/忽略」按钮回调闭环（docs/26 §8–§10）。
