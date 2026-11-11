using NewLife;
using NewLife.Web.OAuth;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 网页授权 URL 辅助。不替换魔方 OAuth 回调，也不另建用户绑定。
/// 登录按钮仍走 <see cref="QyWeiXin.Init"/> 与魔方 OAuthClient.Authorize；
/// code 换 userid 用 <see cref="QyWeixinClient.GetUserByCodeAsync"/>（官方 auth/getuserinfo）。
/// </summary>
/// <remarks>
/// 文档：构造链接 https://developer.work.weixin.qq.com/document/path/91022 ，
/// 身份 https://developer.work.weixin.qq.com/document/path/91023 。
/// 魔方内置模板在企业微信内打开时 scope=snsapi_base 且不带 agentid；
/// 非企业微信 UA 改为扫码 qrConnect。snsapi_privateinfo 必须带 agentid，请用 <see cref="BuildInAppAuthorizeUrl"/>。
/// </remarks>
public static class QyWeixinOAuth
{
    /// <summary>静默授权，只能拿到 userid</summary>
    public const String ScopeBase = "snsapi_base";

    /// <summary>手动授权，可再拿 user_ticket 换敏感信息。必须带 agentid</summary>
    public const String ScopePrivate = "snsapi_privateinfo";

    /// <summary>构造企业微信内网页授权链接（官方参数，含可选 agentid）</summary>
    /// <param name="corpId">企业 ID</param>
    /// <param name="redirectUri">授权后回跳地址，需与企微后台可信域名一致</param>
    /// <param name="state">回传状态，可空</param>
    /// <param name="scope">snsapi_base 或 snsapi_privateinfo</param>
    /// <param name="agentId">应用 ID。scope 为 snsapi_privateinfo 时必填</param>
    /// <returns>带 #wechat_redirect 的授权 URL</returns>
    public static String BuildInAppAuthorizeUrl(String corpId, String redirectUri, String state = null, String scope = ScopeBase, String agentId = null)
    {
        if (corpId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(corpId));
        if (redirectUri.IsNullOrEmpty()) throw new ArgumentNullException(nameof(redirectUri));
        if (scope.IsNullOrEmpty()) scope = ScopeBase;
        if (scope.EqualIgnoreCase(ScopePrivate) && agentId.IsNullOrEmpty())
            throw new ArgumentException("snsapi_privateinfo 必须提供 agentid", nameof(agentId));

        var url = "https://open.weixin.qq.com/connect/oauth2/authorize?appid=" + Uri.EscapeDataString(corpId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
            + "&response_type=code&scope=" + Uri.EscapeDataString(scope)
            + "&state=" + Uri.EscapeDataString(state ?? "");
        if (!agentId.IsNullOrEmpty())
            url += "&agentid=" + Uri.EscapeDataString(agentId);
        return url + "#wechat_redirect";
    }

    /// <summary>构造扫码登录链接，参数与魔方非企业微信 UA 分支一致</summary>
    /// <param name="corpId">企业 ID</param>
    /// <param name="agentId">应用 ID</param>
    /// <param name="redirectUri">回跳地址</param>
    /// <param name="state">回传状态，可空</param>
    /// <returns>qrConnect URL</returns>
    public static String BuildQrConnectUrl(String corpId, String agentId, String redirectUri, String state = null)
    {
        if (corpId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(corpId));
        if (agentId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(agentId));
        if (redirectUri.IsNullOrEmpty()) throw new ArgumentNullException(nameof(redirectUri));

        return "https://open.work.weixin.qq.com/wwopen/sso/qrConnect?appid=" + Uri.EscapeDataString(corpId)
            + "&agentid=" + Uri.EscapeDataString(agentId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
            + "&state=" + Uri.EscapeDataString(state ?? "");
    }

    /// <summary>
    /// 按魔方同一路径生成授权地址：先 Init(userAgent)，再 Authorize。
    /// 企业微信 UA（含 " wxwork/"）得到 snsapi_base 且不附加 agentid；其他 UA 得到 qrConnect。
    /// Authorize 会要求已设置 Secret，与登录按钮一致。
    /// </summary>
    /// <param name="client">已填 CorpId/Secret 的魔方客户端。CorpId 可写成 corp#agent</param>
    /// <param name="redirectUri">回跳地址</param>
    /// <param name="state">状态</param>
    /// <param name="userAgent">浏览器 UA。空字符串表示非企业微信内打开</param>
    /// <returns>魔方生成的授权 URL</returns>
    public static String BuildLikeCube(QyWeiXin client, String redirectUri, String state, String userAgent)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        client.Init(userAgent ?? "");
        return client.Authorize(redirectUri, state);
    }
}

/// <summary>网页授权 code 换到的身份。字段随接口略有差异，以 Raw 为准</summary>
public class QyWeixinCodeIdentity
{
    /// <summary>企业成员 userid。外部用户可能为空</summary>
    public String UserId { get; set; }

    /// <summary>非企业成员时返回的 openid</summary>
    public String OpenId { get; set; }

    /// <summary>敏感信息票据。scope=snsapi_privateinfo 时才有，用于 auth/getuserdetail</summary>
    public String UserTicket { get; set; }

    /// <summary>外部联系人 id。本库不展开客户联系，仅原样带出</summary>
    public String ExternalUserId { get; set; }

    /// <summary>原始响应</summary>
    public IDictionary<String, Object> Raw { get; set; }

    /// <summary>从 auth/getuserinfo 或 user/getuserinfo 的响应字典组装</summary>
    /// <param name="raw">接口响应</param>
    /// <returns>身份对象</returns>
    public static QyWeixinCodeIdentity From(IDictionary<String, Object> raw)
    {
        return new QyWeixinCodeIdentity
        {
            Raw = raw,
            UserId = QyWeixinValues.Pick(raw, "userid", "UserId"),
            OpenId = QyWeixinValues.Pick(raw, "openid", "OpenId"),
            UserTicket = QyWeixinValues.Pick(raw, "user_ticket"),
            ExternalUserId = QyWeixinValues.Pick(raw, "external_userid"),
        };
    }
}
