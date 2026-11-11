using NewLife.Configuration;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企微「接收消息」回调设置。Web 进程加载 Config/QyWeixinCallback.config（或配置中心）。
/// <para>Enable=false 时回调端点直接 404（不验签不解密）；
/// Token / EncodingAESKey 必须与企微后台「接收消息」API 配置一致（企微随机生成后抄录到本配置）。</para>
/// </summary>
[Config("QyWeixinCallback")]
public class QyWeixinCallbackSetting : Config<QyWeixinCallbackSetting>
{
    /// <summary>总开关。默认 false——企微后台配置好回调前保持关闭</summary>
    public Boolean Enable { get; set; }

    /// <summary>回调 Token。与企微后台「接收消息」配置一致</summary>
    public String Token { get; set; } = "";

    /// <summary>回调 EncodingAESKey（43 字符）。与企微后台「接收消息」配置一致</summary>
    public String EncodingAESKey { get; set; } = "";
}
