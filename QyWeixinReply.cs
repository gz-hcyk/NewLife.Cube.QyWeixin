using System.Xml.Linq;
using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>被动回复图文的一条</summary>
public class QyWeixinReplyArticle
{
    /// <summary>标题</summary>
    public String Title { get; set; }

    /// <summary>描述</summary>
    public String Description { get; set; }

    /// <summary>图片链接</summary>
    public String PicUrl { get; set; }

    /// <summary>点击跳转链接</summary>
    public String Url { get; set; }
}

/// <summary>
/// 被动回复明文 XML。文档 https://developer.work.weixin.qq.com/document/path/90241
/// 明文交给 <see cref="QyWeixinCrypt.BuildEncryptedReply"/> 加密后作为 HTTP 正文。
/// 5 秒内只能回复一次，且只对用户消息有效。
/// </summary>
public static class QyWeixinReply
{
    /// <summary>文本回复</summary>
    /// <param name="toUser">接收方，填回调的 FromUserName</param>
    /// <param name="fromUser">发送方，填回调的 ToUserName（企业 ID）</param>
    /// <param name="content">文本</param>
    /// <param name="createTime">Unix 秒，空则取当前 UTC</param>
    /// <returns>明文 XML</returns>
    public static XElement Text(String toUser, String fromUser, String content, Int64? createTime = null)
        => Root(toUser, fromUser, "text", createTime, CData("Content", content));

    /// <summary>图片回复</summary>
    /// <param name="toUser">接收方</param>
    /// <param name="fromUser">发送方</param>
    /// <param name="mediaId">图片 media_id</param>
    /// <param name="createTime">Unix 秒，可空</param>
    /// <returns>明文 XML</returns>
    public static XElement Image(String toUser, String fromUser, String mediaId, Int64? createTime = null)
        => Root(toUser, fromUser, "image", createTime, new XElement("Image", CData("MediaId", mediaId)));

    /// <summary>语音回复</summary>
    /// <param name="toUser">接收方</param>
    /// <param name="fromUser">发送方</param>
    /// <param name="mediaId">语音 media_id</param>
    /// <param name="createTime">Unix 秒，可空</param>
    /// <returns>明文 XML</returns>
    public static XElement Voice(String toUser, String fromUser, String mediaId, Int64? createTime = null)
        => Root(toUser, fromUser, "voice", createTime, new XElement("Voice", CData("MediaId", mediaId)));

    /// <summary>视频回复</summary>
    /// <param name="toUser">接收方</param>
    /// <param name="fromUser">发送方</param>
    /// <param name="mediaId">视频 media_id</param>
    /// <param name="title">标题</param>
    /// <param name="description">描述</param>
    /// <param name="createTime">Unix 秒，可空</param>
    /// <returns>明文 XML</returns>
    public static XElement Video(String toUser, String fromUser, String mediaId, String title, String description, Int64? createTime = null)
        => Root(toUser, fromUser, "video", createTime, new XElement("Video",
            CData("MediaId", mediaId),
            CData("Title", title),
            CData("Description", description)));

    /// <summary>图文回复，1 到 8 条</summary>
    /// <param name="toUser">接收方</param>
    /// <param name="fromUser">发送方</param>
    /// <param name="articles">图文</param>
    /// <param name="createTime">Unix 秒，可空</param>
    /// <returns>明文 XML</returns>
    public static XElement News(String toUser, String fromUser, IEnumerable<QyWeixinReplyArticle> articles, Int64? createTime = null)
    {
        var list = (articles ?? Array.Empty<QyWeixinReplyArticle>()).ToList();
        if (list.Count == 0) throw new ArgumentException("图文至少 1 条", nameof(articles));
        var news = new XElement("Articles");
        foreach (var article in list)
        {
            news.Add(new XElement("item",
                CData("Title", article?.Title),
                CData("Description", article?.Description),
                CData("PicUrl", article?.PicUrl),
                CData("Url", article?.Url)));
        }

        return Root(toUser, fromUser, "news", createTime,
            new XElement("ArticleCount", list.Count),
            news);
    }

    /// <summary>序列化成无缩进 XML 文本</summary>
    /// <param name="element">明文根节点</param>
    /// <returns>XML 文本</returns>
    public static String ToXml(XElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        return element.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Root(String toUser, String fromUser, String msgType, Int64? createTime, params Object[] body)
    {
        if (toUser.IsNullOrEmpty()) throw new ArgumentNullException(nameof(toUser));
        if (fromUser.IsNullOrEmpty()) throw new ArgumentNullException(nameof(fromUser));
        var root = new XElement("xml",
            CData("ToUserName", toUser),
            CData("FromUserName", fromUser),
            new XElement("CreateTime", createTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            CData("MsgType", msgType));
        root.Add(body);
        return root;
    }

    private static XElement CData(String name, String value) => new(name, new XCData(value ?? ""));
}
