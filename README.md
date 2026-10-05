# NewLife.Cube.QyWeixin

企业微信自建应用扩展 for [NewLife.Cube 魔方](https://github.com/NewLifeX/NewLife.Cube)。装包即用，零业务依赖，不改框架源码（派生魔方 `QyWeiXin` 的 `protected` 接口）。

**客户联系（externalcontact）、获客助手、微信客服不在本库范围内。** 家校、政民、会议全量、微盘全量、企业支付、会话存档也不做。

## 文档

- [需求文档](Doc/需求文档.md)
- [功能清单](Doc/功能清单.md)
- [架构设计](Doc/架构设计.md)

## 覆盖面（P0–P3）

| 批次 | 能力 | 入口 |
|---|---|---|
| P0 | 临时素材上传/下载、图片 URL；应用消息 text/image/voice/video/file/textcard/news/mpnews/markdown/miniprogram_notice/template_card；撤回；任务卡片与模板卡片更新；被动回复加密；回调类型目录与分发 | `QyWeiXinSender` / `QyWeixinClient`、`QyWeixinReply`、`QyWeixinCallbackController` |
| P1 | 获取/设置应用，自定义菜单；userid↔openid；网页授权 URL（对齐魔方 OAuth，不另建登录）；code→userid；JS-SDK 企业票与应用票及签名 | `QyWeixinClient`、`QyWeixinOAuth`、`QyWeixinJsSignature` |
| P2 | 部门/成员/标签增删改、邀请、异步导入与任务查询。定时作业只拉不推 | `QyWeixinClient`、`QyWeixinService` |
| P3 | 应用群聊；工作台 keydata 与原始模板/数据；日程、打卡、审批的薄封装 | `QyWeixinClient` |

魔方基类原有的 `GetDepartments` / `GetUser` / `GetUsers` / `GetCheckIn`（固定全部打卡）/ `GetApprovals`（固定已通过）/ `GetApproval` 仍可直接用。

## 配置

凭据优先级：**显式参数 ＞ 魔方 OAuth**。OAuth 的 AppId 写成 `corp#agent`，Secret 填应用 Secret。通讯录写接口和同步作业若权限不同，请显式传入通讯录 Secret，不要复用只能发消息的应用 Secret。

```csharp
var (sender, source) = QyWeixinCredential.Resolve(new QyWeixinOptions
{
    CorpID = "wwxxxx#1000002",
    Secret = "应用Secret",
});
await sender.SendTextAsync("zhangsan", null, "hello");

var (api, _) = QyWeixinCredential.ResolveApi(options);
var sent = await api.SendMessageAsync(QyWeixinAppMessage.Markdown("**告警**").To("zhangsan", null));
await api.RecallMessageAsync(sent.MsgId);
```

`Resolve` 返回的 `QyWeiXinSender` 继承 `QyWeixinClient`，消息方法和其余 API 都在上面。`ResolveClient` 的声明类型仍是魔方 `QyWeiXin`（实际实例已是 `QyWeixinClient`），新代码请用 `ResolveApi`。

回调：企微后台「接收消息」URL 填 `https://域名/QyWeixin/Callback`，Token / EncodingAESKey 写入 `QyWeixinCallback` 并 `Enable=true`。企业 ID 取 OAuth AppId 的 `#` 前半段，用于校验 receiveid；被动回复加密也依赖它。

```csharp
QyWeixinEvents.Handlers.Add(new MyHandler());
```

只处理原始 XML 时继续实现 `IQyWeixinEventHandler`。需要类型字段或被动回复时实现 `IQyWeixinInboundHandler`（或继承 `QyWeixinEventHandlerBase`），`ReplyXml` 填明文，端点负责加密。事件请回空，不要回复。

网页授权：登录按钮继续走魔方 OAuth。本库的 `QyWeixinOAuth.BuildLikeCube` 与魔方 `Init` + `Authorize` 同一路径（企业微信内 `snsapi_base` 且不带 agentid，外部浏览器走扫码）。`snsapi_privateinfo` 用 `BuildInAppAuthorizeUrl`，必须带 agentid。code 换 userid 用 `GetUserByCodeAsync`（`auth/getuserinfo`），不替换魔方回调落库。

JS-SDK：`CreateConfigSignatureAsync` / `CreateAgentConfigSignatureAsync`。ticket 只留在服务端。

## 通讯录：同步作业与写接口

| | 定时作业 `QyWeixinService` | `QyWeixinClient` 写接口 |
|---|---|---|
| 方向 | 企微 → 魔方 Department/User | 调用方 → 企微 |
| 是否改另一侧 | 不调用任何写接口，不会覆盖企微 | 不写魔方部门/用户 |
| 默认 | `PullMode=Upsert`：企微覆盖本地映射字段（名称、上级、排序、启用、显示名、主部门） | 调用即推送 |
| 不想盖住本地手工修改 | `PullMode=InsertOnly`，或停用作业 | — |

两边一起用时，把企微当作组织数据来源。Upsert 会在下次作业把远端写入反映到魔方，这是拉取，不是把本地改动推回去。

## 接口对照

官方目录：[开发前必读 / 服务端 API](https://developer.work.weixin.qq.com/document/path/90556)。下表链到自建应用文档路径。

| 能力 | 方法 | 文档 |
|---|---|---|
| 上传临时素材 | `UploadMediaAsync` | [90253](https://developer.work.weixin.qq.com/document/path/90253) |
| 获取临时素材 | `GetMediaAsync` | [90254](https://developer.work.weixin.qq.com/document/path/90254) |
| 上传图片 | `UploadImageAsync` | [90256](https://developer.work.weixin.qq.com/document/path/90256) |
| 发送应用消息 | `SendMessageAsync` 及各类型方法 | [90236](https://developer.work.weixin.qq.com/document/path/90236) |
| 撤回应用消息 | `RecallMessageAsync` | [94947](https://developer.work.weixin.qq.com/document/path/94947) |
| 更新模板卡片 | `UpdateTemplateCardAsync` | [94888](https://developer.work.weixin.qq.com/document/path/94888) |
| 更新任务卡片 | `UpdateTaskCardAsync` | 历史 `message/update_taskcard` |
| 接收消息格式 | `QyWeixinInboundMessage` | [90239](https://developer.work.weixin.qq.com/document/path/90239) |
| 接收事件格式 | `QyWeixinCallbackCatalog` | [90240](https://developer.work.weixin.qq.com/document/path/90240) |
| 被动回复 | `QyWeixinReply` + `QyWeixinCrypt.BuildEncryptedReply` | [90241](https://developer.work.weixin.qq.com/document/path/90241) |
| 获取/设置应用 | `GetAgentAsync` / `SetAgentAsync` | [90227](https://developer.work.weixin.qq.com/document/path/90227) / [90228](https://developer.work.weixin.qq.com/document/path/90228) |
| 菜单 | `CreateMenuAsync` / `GetMenuAsync` / `DeleteMenuAsync` | [90231](https://developer.work.weixin.qq.com/document/path/90231) / [90232](https://developer.work.weixin.qq.com/document/path/90232) / [90233](https://developer.work.weixin.qq.com/document/path/90233) |
| userid↔openid | `ConvertToOpenIdAsync` / `ConvertToUserIdAsync` | [90202](https://developer.work.weixin.qq.com/document/path/90202) |
| 网页授权 | `QyWeixinOAuth`、`GetUserByCodeAsync`、`GetUserDetailByTicketAsync` | [91022](https://developer.work.weixin.qq.com/document/path/91022) / [91023](https://developer.work.weixin.qq.com/document/path/91023) / [95833](https://developer.work.weixin.qq.com/document/path/95833) |
| JS-SDK 签名 | `QyWeixinJsSignature`、`GetJsApiTicketAsync`、`GetAgentJsApiTicketAsync` | [90506](https://developer.work.weixin.qq.com/document/path/90506) |
| 部门 | `Create/Update/Delete/GetDepartmentAsync`，列表用基类 `GetDepartments` | [90205](https://developer.work.weixin.qq.com/document/path/90205) / [90206](https://developer.work.weixin.qq.com/document/path/90206) / [90207](https://developer.work.weixin.qq.com/document/path/90207) / [90208](https://developer.work.weixin.qq.com/document/path/90208) |
| 成员 | `Create/Update/DeleteMemberAsync`，读取用基类 `GetUser`/`GetUsers` | [90195](https://developer.work.weixin.qq.com/document/path/90195) / [90196](https://developer.work.weixin.qq.com/document/path/90196) / [90197](https://developer.work.weixin.qq.com/document/path/90197) / [90198](https://developer.work.weixin.qq.com/document/path/90198) |
| 批量删除成员 | `DeleteMembersAsync` | [90199](https://developer.work.weixin.qq.com/document/path/90199) |
| 邀请 | `InviteAsync` | [90975](https://developer.work.weixin.qq.com/document/path/90975) |
| 标签 | `Create/Update/Delete/GetTagAsync`、`Add/DeleteTagMembersAsync`、`ListTagsAsync` | [90210](https://developer.work.weixin.qq.com/document/path/90210)–[90216](https://developer.work.weixin.qq.com/document/path/90216) |
| 异步导入 | `SyncUsersBatchAsync`、`ReplaceUsersBatchAsync`、`ReplaceDepartmentsBatchAsync`、`GetBatchResultAsync` | [90980](https://developer.work.weixin.qq.com/document/path/90980) / [90981](https://developer.work.weixin.qq.com/document/path/90981) / [90982](https://developer.work.weixin.qq.com/document/path/90982) / [90983](https://developer.work.weixin.qq.com/document/path/90983) |
| 应用群聊 | `Create/Update/Get/SendAppChatAsync` | [90245](https://developer.work.weixin.qq.com/document/path/90245) / [98913](https://developer.work.weixin.qq.com/document/path/98913) / [90248](https://developer.work.weixin.qq.com/document/path/90248) |
| 工作台 | `SetWorkbenchKeyDataTemplateAsync`、`SetWorkbenchTemplateRawAsync`、`SetWorkbenchKeyDataAsync` | [92535](https://developer.work.weixin.qq.com/document/path/92535) |
| 日程 | `Add/Update/Get/CancelScheduleAsync` | [93648](https://developer.work.weixin.qq.com/document/path/93648) / [97720](https://developer.work.weixin.qq.com/document/path/97720) / [97724](https://developer.work.weixin.qq.com/document/path/97724) |
| 打卡 | `GetCheckInDataAsync`、日报/月报 | [96497](https://developer.work.weixin.qq.com/document/path/96497) / [93374](https://developer.work.weixin.qq.com/document/path/93374) / [93387](https://developer.work.weixin.qq.com/document/path/93387) |
| 审批 | `QueryApprovalInfoAsync`、`GetApprovalTemplateAsync`，详情用基类 `GetApproval` | [91982](https://developer.work.weixin.qq.com/document/path/91982) |

## 兼容

- `SendTextAsync` / `SendTextCardAsync` / `SendTemplateCardAsync` / `UpdateTaskCardAsync` 的签名不变。前三个仍返回 `Task`（不带 msgid）；要 msgid 用 `SendMessageAsync`。
- 文本卡片历史默认按钮文案仍是「查看」。
- `message/send` 的 agentid 仍按字符串写入。
- `DecryptEcho` 改为按官方封装拆包后回显明文。此前会把随机头和 receiveid 一起当明文返回，URL 验证对不上。
- 回调端点路径、`IQyWeixinEventHandler` 注册方式不变。

## 打包与测试

```bash
dotnet test XUnitTest/XUnitTest.csproj
dotnet pack -c Release
# 产物：nupkg/NewLife.Cube.QyWeixin.<版本>.nupkg
```

依赖 `NewLife.Cube.Core` 6.15+，目标框架 `net8.0`。单测不访问企微网络，覆盖加解密、签名、消息体、回调解析和授权 URL。

## 参考实现

智能锁管理平台（明佳 IoT）的告警推送基于本扩展：`AlertPushJob` 用 `QyWeiXinSender` 发模板卡片，`AlertCardEventHandler` 实现 `IQyWeixinEventHandler` 完成按钮回调。
