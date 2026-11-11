using System.Xml.Linq;
using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>解密后的回调报文。节点名与企微 XML 一致，CDATA 由 XElement 解开</summary>
public class QyWeixinInboundMessage
{
    /// <summary>原始 XML</summary>
    public XElement Xml { get; }

    /// <summary>实例化</summary>
    /// <param name="xml">解密后的 xml 根节点</param>
    public QyWeixinInboundMessage(XElement xml) => Xml = xml ?? throw new ArgumentNullException(nameof(xml));

    /// <summary>企业微信 CorpID</summary>
    public String ToUserName => Get("ToUserName");

    /// <summary>发送者 userid。事件里是成员 userid</summary>
    public String FromUserName => Get("FromUserName");

    /// <summary>消息创建时间，Unix 秒文本</summary>
    public String CreateTime => Get("CreateTime");

    /// <summary>消息类型。见 <see cref="QyWeixinCallbackCatalog.MsgType"/></summary>
    public String MsgType => Get("MsgType");

    /// <summary>事件类型。仅 MsgType=event 时有值</summary>
    public String Event => Get("Event");

    /// <summary>事件 key。菜单 key、按钮 key 等</summary>
    public String EventKey => Get("EventKey");

    /// <summary>消息 id</summary>
    public String MsgId => Get("MsgId");

    /// <summary>应用 id</summary>
    public String AgentId => Get("AgentID");

    /// <summary>文本内容</summary>
    public String Content => Get("Content");

    /// <summary>图片等的媒体 id</summary>
    public String MediaId => Get("MediaId");

    /// <summary>任务卡片或模板卡片的 task_id</summary>
    public String TaskId => Get("TaskId");

    /// <summary>模板卡片更新用的 response_code</summary>
    public String ResponseCode => Get("ResponseCode");

    /// <summary>通讯录变更细类</summary>
    public String ChangeType => Get("ChangeType");

    /// <summary>读取直接子节点文本。没有该节点时返回 null</summary>
    /// <param name="name">节点名，大小写不敏感</param>
    /// <returns>节点文本</returns>
    public String Get(String name)
    {
        if (name.IsNullOrEmpty() || Xml == null) return null;
        var node = Xml.Elements().FirstOrDefault(e => e.Name.LocalName.EqualIgnoreCase(name));
        return node?.Value;
    }

    /// <summary>是否为指定消息类型</summary>
    /// <param name="msgType">如 text、event</param>
    /// <returns>类型匹配时为 true</returns>
    public Boolean IsMessage(String msgType) => MsgType.EqualIgnoreCase(msgType);

    /// <summary>是否为指定事件</summary>
    /// <param name="eventName">如 click、template_card_event</param>
    /// <returns>MsgType=event 且 Event 匹配时为 true</returns>
    public Boolean IsEvent(String eventName) => IsMessage(QyWeixinCallbackCatalog.MsgType.Event) && Event.EqualIgnoreCase(eventName);

    /// <summary>解析 XML 文本</summary>
    /// <param name="xml">解密后的 XML</param>
    /// <returns>报文</returns>
    public static QyWeixinInboundMessage Parse(String xml)
    {
        if (xml.IsNullOrEmpty()) throw new ArgumentNullException(nameof(xml));
        return Parse(XElement.Parse(xml));
    }

    /// <summary>包装已解析的 XML</summary>
    /// <param name="xml">根节点</param>
    /// <returns>报文</returns>
    public static QyWeixinInboundMessage Parse(XElement xml) => new(xml);
}

/// <summary>处理器对一条回调的处理结论</summary>
public class QyWeixinHandleResult
{
    /// <summary>true 表示已消费，不再交给后续处理器，也不再调用 HandleAsync</summary>
    public Boolean Handled { get; set; }

    /// <summary>
    /// 被动回复的明文 XML。空表示不回复。
    /// 仅用户发来的消息可回复，事件回复会被企微忽略。加密由端点完成。
    /// </summary>
    public String ReplyXml { get; set; }

    /// <summary>消费且不回复</summary>
    /// <returns>已处理结果</returns>
    public static QyWeixinHandleResult Consume() => new() { Handled = true };

    /// <summary>消费并被动回复明文 XML</summary>
    /// <param name="replyXml">QyWeixinReply 生成的明文</param>
    /// <returns>已处理结果</returns>
    public static QyWeixinHandleResult Reply(String replyXml) => new() { Handled = true, ReplyXml = replyXml };
}

/// <summary>
/// 结构化回调处理器。仍须实现 <see cref="IQyWeixinEventHandler"/> 才能注册进
/// <see cref="QyWeixinEvents.Handlers"/>。Handled=false 时端点会继续调用 HandleAsync。
/// </summary>
public interface IQyWeixinInboundHandler : IQyWeixinEventHandler
{
    /// <summary>处理一条已解析的回调</summary>
    /// <param name="message">结构化报文</param>
    /// <returns>处理结论。null 视为未消费</returns>
    Task<QyWeixinHandleResult> HandleInboundAsync(QyWeixinInboundMessage message);
}

/// <summary>只实现结构化处理的基类。HandleAsync 固定返回 false</summary>
public abstract class QyWeixinEventHandlerBase : IQyWeixinInboundHandler
{
    /// <summary>结构化入口未消费时才会走到这里。默认不消费</summary>
    /// <param name="message">原始 XML</param>
    /// <returns>false</returns>
    public virtual Task<Boolean> HandleAsync(XElement message) => Task.FromResult(false);

    /// <summary>处理结构化报文</summary>
    /// <param name="message">报文</param>
    /// <returns>处理结论</returns>
    public abstract Task<QyWeixinHandleResult> HandleInboundAsync(QyWeixinInboundMessage message);
}

/// <summary>一次分发的结果</summary>
public class QyWeixinDispatchResult
{
    /// <summary>是否有处理器消费</summary>
    public Boolean Handled { get; set; }

    /// <summary>要加密回包的明文。空表示回空串</summary>
    public String ReplyXml { get; set; }
}

/// <summary>回调分发。端点与单元测试共用，避免把分支写死在 Controller 里</summary>
public static class QyWeixinCallbackDispatch
{
    /// <summary>
    /// 按注册顺序分发。实现了 <see cref="IQyWeixinInboundHandler"/> 的先走结构化入口；
    /// 未消费再走 <see cref="IQyWeixinEventHandler.HandleAsync"/>。
    /// </summary>
    /// <param name="xml">解密后的 XML</param>
    /// <param name="handlers">处理器列表</param>
    /// <returns>分发结果</returns>
    public static async Task<QyWeixinDispatchResult> DispatchAsync(XElement xml, IEnumerable<IQyWeixinEventHandler> handlers)
    {
        if (xml == null) throw new ArgumentNullException(nameof(xml));
        var inbound = QyWeixinInboundMessage.Parse(xml);
        foreach (var handler in handlers ?? Array.Empty<IQyWeixinEventHandler>())
        {
            if (handler == null) continue;
            if (handler is IQyWeixinInboundHandler rich)
            {
                var result = await rich.HandleInboundAsync(inbound).ConfigureAwait(false);
                if (result != null && result.Handled)
                    return new QyWeixinDispatchResult { Handled = true, ReplyXml = result.ReplyXml };
            }

            if (await handler.HandleAsync(xml).ConfigureAwait(false))
                return new QyWeixinDispatchResult { Handled = true };
        }

        return new QyWeixinDispatchResult();
    }
}
