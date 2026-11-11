using System.ComponentModel;
using Xunit;
using System.Xml.Linq;
using NewLife.Cube.QyWeixin;

namespace XUnitTest;

public class QyWeixinCallbackTests
{
    [Fact]
    [DisplayName("入站解析_模板卡片事件字段")]
    public void Parse_TemplateCardEvent()
    {
        var xml = """
            <xml>
              <ToUserName><![CDATA[ww1]]></ToUserName>
              <FromUserName><![CDATA[zhangsan]]></FromUserName>
              <CreateTime>1700000000</CreateTime>
              <MsgType><![CDATA[event]]></MsgType>
              <Event><![CDATA[template_card_event]]></Event>
              <EventKey><![CDATA[btn_ok]]></EventKey>
              <TaskId><![CDATA[task-1]]></TaskId>
              <ResponseCode><![CDATA[rc]]></ResponseCode>
              <AgentID>1000002</AgentID>
            </xml>
            """;
        var msg = QyWeixinInboundMessage.Parse(xml);
        Assert.True(msg.IsEvent(QyWeixinCallbackCatalog.Event.TemplateCard));
        Assert.Equal("zhangsan", msg.FromUserName);
        Assert.Equal("btn_ok", msg.EventKey);
        Assert.Equal("task-1", msg.TaskId);
        Assert.Equal("rc", msg.ResponseCode);
        Assert.Equal("1000002", msg.AgentId);
        Assert.False(msg.IsMessage(QyWeixinCallbackCatalog.MsgType.Text));
    }

    [Fact]
    [DisplayName("入站解析_通讯录变更ChangeType")]
    public void Parse_ChangeContact()
    {
        var xml = "<xml><MsgType><![CDATA[event]]></MsgType><Event><![CDATA[change_contact]]></Event><ChangeType><![CDATA[update_user]]></ChangeType><UserID><![CDATA[u1]]></UserID></xml>";
        var msg = QyWeixinInboundMessage.Parse(xml);
        Assert.True(msg.IsEvent(QyWeixinCallbackCatalog.Event.ChangeContact));
        Assert.Equal(QyWeixinCallbackCatalog.ChangeType.UpdateUser, msg.ChangeType);
        Assert.Equal("u1", msg.Get("UserID"));
    }

    [Fact]
    [DisplayName("被动回复文本_含CDATA与MsgType")]
    public void ReplyText_Xml()
    {
        var xml = QyWeixinReply.ToXml(QyWeixinReply.Text("user", "ww", "收到", 1700000000));
        var doc = XElement.Parse(xml);
        Assert.Equal("user", doc.Element("ToUserName").Value);
        Assert.Equal("ww", doc.Element("FromUserName").Value);
        Assert.Equal("text", doc.Element("MsgType").Value);
        Assert.Equal("收到", doc.Element("Content").Value);
        Assert.Equal("1700000000", doc.Element("CreateTime").Value);
        Assert.Contains("CDATA", xml);
    }

    [Fact]
    [DisplayName("分发_结构化处理器优先且可回复")]
    public async Task Dispatch_InboundHandler_ReturnsReply()
    {
        var xml = XElement.Parse("<xml><MsgType><![CDATA[text]]></MsgType><Content><![CDATA[hi]]></Content><FromUserName><![CDATA[u]]></FromUserName><ToUserName><![CDATA[ww]]></ToUserName></xml>");
        var result = await QyWeixinCallbackDispatch.DispatchAsync(xml, new IQyWeixinEventHandler[]
        {
            new ReplyHandler(),
            new LegacyHandler(),
        });
        Assert.True(result.Handled);
        Assert.Contains("pong", result.ReplyXml);
    }

    [Fact]
    [DisplayName("分发_旧处理器仍可单独消费")]
    public async Task Dispatch_LegacyHandler_NoReply()
    {
        var xml = XElement.Parse("<xml><MsgType>event</MsgType><Event>click</Event></xml>");
        var legacy = new LegacyHandler();
        var result = await QyWeixinCallbackDispatch.DispatchAsync(xml, new IQyWeixinEventHandler[] { legacy });
        Assert.True(result.Handled);
        Assert.Null(result.ReplyXml);
        Assert.True(legacy.Called);
    }

    [Fact]
    [DisplayName("分发_结构化未消费时回落到HandleAsync")]
    public async Task Dispatch_InboundSkip_FallsBack()
    {
        var xml = XElement.Parse("<xml><MsgType>text</MsgType></xml>");
        var both = new SkipThenLegacy();
        var result = await QyWeixinCallbackDispatch.DispatchAsync(xml, new IQyWeixinEventHandler[] { both });
        Assert.True(result.Handled);
        Assert.True(both.LegacyCalled);
    }

    private sealed class ReplyHandler : QyWeixinEventHandlerBase
    {
        public override Task<QyWeixinHandleResult> HandleInboundAsync(QyWeixinInboundMessage message)
        {
            var reply = QyWeixinReply.ToXml(QyWeixinReply.Text(message.FromUserName, message.ToUserName, "pong", 1));
            return Task.FromResult(QyWeixinHandleResult.Reply(reply));
        }
    }

    private sealed class LegacyHandler : IQyWeixinEventHandler
    {
        public Boolean Called { get; private set; }

        public Task<Boolean> HandleAsync(XElement message)
        {
            Called = true;
            return Task.FromResult(true);
        }
    }

    private sealed class SkipThenLegacy : IQyWeixinInboundHandler
    {
        public Boolean LegacyCalled { get; private set; }

        public Task<QyWeixinHandleResult> HandleInboundAsync(QyWeixinInboundMessage message)
            => Task.FromResult(new QyWeixinHandleResult { Handled = false });

        public Task<Boolean> HandleAsync(XElement message)
        {
            LegacyCalled = true;
            return Task.FromResult(true);
        }
    }
}
