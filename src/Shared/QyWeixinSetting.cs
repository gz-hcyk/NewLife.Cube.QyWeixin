using System.ComponentModel;
using NewLife.Configuration;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信基础配置。沿用旧 WxWork.Api 的 WxWork 配置节，部署时无需改配置文件名。
/// 凭据为空时，<see cref="QyWeixinCredential"/> 仍可回退到魔方 OAuth 配置中的企业微信条目。
/// </summary>
[Config("WxWork")]
public class QyWeixinSetting : Config<QyWeixinSetting>
{
    /// <summary>企业 Id</summary>
    [Description("企业ID")]
    public String CorpID { get; set; }

    /// <summary>通讯录应用凭证。通讯录同步写接口使用</summary>
    [Description("通讯录密钥")]
    public String ContactSecret { get; set; }

    /// <summary>应用 Id</summary>
    [Description("应用AgentId")]
    public String AgentID { get; set; }

    /// <summary>应用凭证。发送消息、授权换 userid、JS-SDK 使用</summary>
    [Description("应用Secret")]
    public String AgentSecret { get; set; }

    /// <summary>事件接收服务器 Token</summary>
    [Description("事件接收服务器Token")]
    public String CallbackToken { get; set; }

    /// <summary>事件接收服务器 EncodingAESKey（43 字符）</summary>
    [Description("事件接收服务器EncodingAESKey")]
    public String CallbackEncodingAESKey { get; set; }
}
