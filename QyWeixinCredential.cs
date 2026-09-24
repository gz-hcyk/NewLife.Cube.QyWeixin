using NewLife;
using NewLife.Cube.Entity;
using NewLife.Log;
using NewLife.Web.OAuth;

namespace NewLife.Cube.QyWeixin;

/// <summary>企微凭据（显式参数优先；为空时回退魔方 OAuth 配置的企业微信条目）</summary>
public class QyWeixinOptions
{
    /// <summary>企业 Id。可写成 corp#agent 复合值，Init 后同时得到 AgentId</summary>
    public String CorpID { get; set; }

    /// <summary>应用 Id</summary>
    public String AgentID { get; set; }

    /// <summary>应用凭证（发送消息用应用 Secret，通讯录同步用通讯录 Secret）</summary>
    public String Secret { get; set; }
}

/// <summary>
/// 企微凭据解析：显式参数（作业参数）＞魔方 OAuth 配置（企业微信）＞无可用凭据返回 null。
/// 魔方 OAuth 配置约定：AppId 写成 corp#agent 复合写法以同时提供 CorpId 与 AgentId。
/// </summary>
public static class QyWeixinCredential
{
    /// <summary>解析应用消息发送器。</summary>
    /// <param name="options">显式凭据（CorpID/AgentID/Secret），null 或不全时走 OAuth 配置</param>
    /// <returns>发送器（无可用凭据时为 null）与凭据来源说明（用于日志排查）</returns>
    public static (QyWeiXinSender Sender, String Source) Resolve(QyWeixinOptions options)
    {
        // 1. 显式参数优先
        if (options != null && !options.CorpID.IsNullOrEmpty() && !options.Secret.IsNullOrEmpty())
        {
            var s = new QyWeiXinSender
            {
                CorpId = options.CorpID,
                Secret = options.Secret,
                AgentId = options.AgentID,
            };
            // Init 会解析 CorpID 中 corp#agent 的复合写法
            s.Init("");
            return (s, "显式参数");
        }

        // 2. 魔方 OAuth 配置（OAuthConfig 表）中的企业微信条目
        try
        {
            var cfg = FindOAuthConfig();
            if (cfg != null && !cfg.AppId.IsNullOrEmpty() && !cfg.Secret.IsNullOrEmpty())
            {
                var s = new QyWeiXinSender
                {
                    CorpId = cfg.AppId,
                    Secret = cfg.Secret,
                };
                s.Init("");
                // AgentId 优先取 AppId 复合写法 corp#agent；否则回退显式参数的 AgentID
                if (s.AgentId.IsNullOrEmpty() && options != null && !options.AgentID.IsNullOrEmpty())
                    s.AgentId = options.AgentID;
                if (!s.AgentId.IsNullOrEmpty()) return (s, "魔方OAuth配置");
                XTrace.WriteLine("企微凭据：魔方 OAuth 配置(企业微信) 未提供应用 AgentId（AppId 需为 corp#agent 复合写法，或显式提供 AgentID），无法推送应用消息");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return (null, null);
    }

    /// <summary>解析 API 客户端（QyWeiXin，用于通讯录等只读接口）。</summary>
    /// <param name="options">显式凭据，null 或不全时走 OAuth 配置</param>
    public static (QyWeiXin Client, String Source) ResolveClient(QyWeixinOptions options)
    {
        if (options != null && !options.CorpID.IsNullOrEmpty() && !options.Secret.IsNullOrEmpty())
        {
            var client = CreateClient(options.CorpID, options.Secret, options.AgentID);
            return (client, "显式参数");
        }

        try
        {
            var cfg = FindOAuthConfig();
            if (cfg != null && !cfg.AppId.IsNullOrEmpty() && !cfg.Secret.IsNullOrEmpty())
            {
                var client = CreateClient(cfg.AppId, cfg.Secret, options?.AgentID);
                return (client, "魔方OAuth配置");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return (null, null);
    }

    /// <summary>在魔方 OAuth 配置（OAuthConfig 表）中查找已启用的企业微信条目：优先按 Provider=QyWeiXin 匹配，其次按名称包含“企业微信”或 QyWeiXin。</summary>
    public static OAuthConfig FindOAuthConfig()
    {
        var list = OAuthConfig.FindAllByTenantId(0);
        foreach (var c in list)
        {
            if (!c.Enable) continue;
            if (c.Provider.EqualIgnoreCase("QyWeiXin")) return c;
        }
        foreach (var c in list)
        {
            if (!c.Enable) continue;
            if (!c.Name.IsNullOrEmpty() && (c.Name.Contains("企业微信") || c.Name.EqualIgnoreCase("QyWeiXin"))) return c;
        }
        return null;
    }

    private static QyWeiXin CreateClient(String corpId, String secret, String agentId)
    {
        var client = new QyWeiXin
        {
            CorpId = corpId,
            Secret = secret,
            AgentId = agentId,
        };
        client.Init("");
        return client;
    }
}
