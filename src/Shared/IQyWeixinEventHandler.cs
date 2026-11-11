using System.Xml.Linq;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企微回调事件处理器。回调端点完成验签解密后，按注册顺序分发事件报文，
/// 第一个返回 true 的处理器消费该事件。业务方实现自己的处理器并注册到
/// <see cref="QyWeixinEvents.Handlers"/>（如告警卡片按钮处理）。
/// </summary>
public interface IQyWeixinEventHandler
{
    /// <summary>处理一条解密后的事件报文</summary>
    /// <param name="message">事件 XML（MsgType/Event/TaskId/EventKey/FromUserName 等，CDATA 已解析）</param>
    /// <returns>true=已消费（不再分发后续处理器）</returns>
    Task<Boolean> HandleAsync(XElement message);
}

/// <summary>回调事件处理器注册表。端点按顺序分发；业务方在启动时注册自己的处理器。</summary>
public static class QyWeixinEvents
{
    /// <summary>已注册处理器（按注册顺序分发）</summary>
    public static IList<IQyWeixinEventHandler> Handlers { get; } = new List<IQyWeixinEventHandler>();
}
