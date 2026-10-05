using System.ComponentModel;
using Xunit;
using NewLife.Cube.QyWeixin;

namespace XUnitTest;

public class QyWeixinMessageTests
{
    [Fact]
    [DisplayName("文本消息_保留历史字段且不带空totag")]
    public void Text_MatchesLegacyShape()
    {
        var body = QyWeixinAppMessage.Text("hello").To("zhangsan", "").ToBody("1000002");
        Assert.Equal("zhangsan", body["touser"]);
        Assert.Equal("", body["toparty"]);
        Assert.False(body.ContainsKey("totag"));
        Assert.Equal("text", body["msgtype"]);
        Assert.Equal("1000002", body["agentid"]);
        var text = Assert.IsType<Dictionary<String, Object>>(body["text"]);
        Assert.Equal("hello", text["content"]);
        Assert.False(body.ContainsKey("safe"));
    }

    [Fact]
    [DisplayName("接收人全空_抛ArgumentNullException")]
    public void Text_NoTarget_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QyWeixinAppMessage.Text("a").ToBody("1"));
    }

    [Fact]
    [DisplayName("图片语音文件视频_media_id与可选标题")]
    public void MediaMessages_Shape()
    {
        var image = Assert.IsType<Dictionary<String, Object>>(QyWeixinAppMessage.Image("mid").To("u", null).ToBody("1")["image"]);
        Assert.Equal("mid", image["media_id"]);

        var video = Assert.IsType<Dictionary<String, Object>>(
            QyWeixinAppMessage.Video("vid", "标题", null).To("u", null, "3").ToBody("1")["video"]);
        Assert.Equal("vid", video["media_id"]);
        Assert.Equal("标题", video["title"]);
        Assert.False(video.ContainsKey("description"));
        Assert.Equal("3", QyWeixinAppMessage.Video("vid").To("u", null, "3").ToBody("1")["totag"]);
    }

    [Fact]
    [DisplayName("图文markdown小程序_正文字段名与msgtype一致")]
    public void RichMessages_Shape()
    {
        var news = QyWeixinAppMessage.News(new[]
        {
            new QyWeixinNewsArticle { Title = "t", Url = "https://e.cn", PicUrl = "https://e.cn/a.png" },
        }).To("u", null).ToBody("9");
        var articles = Assert.IsType<List<Dictionary<String, Object>>>(((Dictionary<String, Object>)news["news"])["articles"]);
        Assert.Equal("t", articles[0]["title"]);
        Assert.False(articles[0].ContainsKey("description"));

        var md = Assert.IsType<Dictionary<String, Object>>(QyWeixinAppMessage.Markdown("**a**").To("@all", null).ToBody("9")["markdown"]);
        Assert.Equal("**a**", md["content"]);

        var mini = QyWeixinAppMessage.MiniProgramNotice("wx1", "通知", new[]
        {
            new QyWeixinMiniProgramItem { Key = "地点", Value = "A1" },
        }, page: "pages/a", emphasisFirstItem: true).To("u", null).ToBody("9");
        var payload = Assert.IsType<Dictionary<String, Object>>(mini["miniprogram_notice"]);
        Assert.Equal("wx1", payload["appid"]);
        Assert.Equal(true, payload["emphasis_first_item"]);
        Assert.Equal("pages/a", payload["page"]);
    }

    [Fact]
    [DisplayName("文本卡片_按钮字段是btntxt")]
    public void TextCard_BtnTxt()
    {
        var card = Assert.IsType<Dictionary<String, Object>>(
            QyWeixinAppMessage.TextCard("t", "d", "https://a", "查看").To("u", "1").ToBody("2")["textcard"]);
        Assert.Equal("查看", card["btntxt"]);
    }

    [Fact]
    [DisplayName("群聊消息_无agentid")]
    public void ChatMessage_UsesChatId()
    {
        var body = QyWeixinChatMessage.FromApp("chat1", QyWeixinAppMessage.Text("hi")).ToBody();
        Assert.Equal("chat1", body["chatid"]);
        Assert.False(body.ContainsKey("agentid"));
        Assert.False(body.ContainsKey("touser"));
    }

    [Fact]
    [DisplayName("素材大小_小于5字节与未知类型拒绝")]
    public void MediaLimits_Reject()
    {
        Assert.Throws<ArgumentException>(() => QyWeixinMediaLimits.Ensure("image", 4));
        Assert.Throws<ArgumentException>(() => QyWeixinMediaLimits.Ensure("nope", 10));
        QyWeixinMediaLimits.Ensure("file", 20);
    }
}
