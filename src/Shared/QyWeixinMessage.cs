using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>图文消息（news）的一条。跳转外链，不用 media_id</summary>
public class QyWeixinNewsArticle
{
    /// <summary>标题，不超过 128 字节</summary>
    public String Title { get; set; }

    /// <summary>描述，不超过 512 字节</summary>
    public String Description { get; set; }

    /// <summary>点击跳转链接</summary>
    public String Url { get; set; }

    /// <summary>图片链接。支持 JPG、PNG，建议大图 1068*455，小图 150*150</summary>
    public String PicUrl { get; set; }

    /// <summary>转成接口 articles 元素</summary>
    /// <returns>仅包含已填写字段的字典</returns>
    public Dictionary<String, Object> ToBody()
    {
        var body = new Dictionary<String, Object>();
        QyWeixinValues.Set(body, "title", Title);
        QyWeixinValues.Set(body, "description", Description);
        QyWeixinValues.Set(body, "url", Url);
        QyWeixinValues.Set(body, "picurl", PicUrl);
        return body;
    }
}

/// <summary>图文消息（mpnews）的一条。正文在企微内打开，封面用临时素材 media_id</summary>
public class QyWeixinMpNewsArticle
{
    /// <summary>标题，不超过 128 字节</summary>
    public String Title { get; set; }

    /// <summary>封面 media_id，通过素材上传接口获得</summary>
    public String ThumbMediaId { get; set; }

    /// <summary>作者，不超过 64 字节</summary>
    public String Author { get; set; }

    /// <summary>原文链接</summary>
    public String ContentSourceUrl { get; set; }

    /// <summary>正文，支持 HTML，不超过 666 K</summary>
    public String Content { get; set; }

    /// <summary>摘要。不填则从正文截取</summary>
    public String Digest { get; set; }

    /// <summary>转成接口 articles 元素</summary>
    /// <returns>仅包含已填写字段的字典</returns>
    public Dictionary<String, Object> ToBody()
    {
        var body = new Dictionary<String, Object>();
        QyWeixinValues.Set(body, "title", Title);
        QyWeixinValues.Set(body, "thumb_media_id", ThumbMediaId);
        QyWeixinValues.Set(body, "author", Author);
        QyWeixinValues.Set(body, "content_source_url", ContentSourceUrl);
        QyWeixinValues.Set(body, "content", Content);
        QyWeixinValues.Set(body, "digest", Digest);
        return body;
    }
}

/// <summary>小程序通知 content_item 的一项</summary>
public class QyWeixinMiniProgramItem
{
    /// <summary>长度 10 个汉字以内</summary>
    public String Key { get; set; }

    /// <summary>长度 30 个汉字以内</summary>
    public String Value { get; set; }

    /// <summary>转成 content_item 元素</summary>
    /// <returns>key/value 字典</returns>
    public Dictionary<String, Object> ToBody() => new()
    {
        ["key"] = Key ?? "",
        ["value"] = Value ?? "",
    };
}

/// <summary>message/send 的发送结果。errcode 非 0 时由基类抛出，不会得到本对象</summary>
public class QyWeixinSendResult
{
    /// <summary>消息 id，撤回时原样回传</summary>
    public String MsgId { get; set; }

    /// <summary>无效的 userid，竖线分隔</summary>
    public String InvalidUser { get; set; }

    /// <summary>无效的部门 id</summary>
    public String InvalidParty { get; set; }

    /// <summary>无效的标签 id</summary>
    public String InvalidTag { get; set; }

    /// <summary>没有基础接口许可的 userid</summary>
    public String UnlicensedUser { get; set; }

    /// <summary>原始响应</summary>
    public IDictionary<String, Object> Raw { get; set; }

    /// <summary>从 message/send 响应组装</summary>
    /// <param name="raw">响应字典</param>
    /// <returns>发送结果</returns>
    public static QyWeixinSendResult From(IDictionary<String, Object> raw) => new()
    {
        Raw = raw,
        MsgId = QyWeixinValues.Pick(raw, "msgid"),
        InvalidUser = QyWeixinValues.Pick(raw, "invaliduser"),
        InvalidParty = QyWeixinValues.Pick(raw, "invalidparty"),
        InvalidTag = QyWeixinValues.Pick(raw, "invalidtag"),
        UnlicensedUser = QyWeixinValues.Pick(raw, "unlicenseduser"),
    };
}

/// <summary>
/// 应用消息体。ToBody 的键与 message/send 一致。
/// 文档 https://developer.work.weixin.qq.com/document/path/90236
/// touser/toparty 始终输出（可为空串），与历史发送器一致；totag 仅在有值时输出。
/// </summary>
public class QyWeixinAppMessage
{
    /// <summary>成员 ID 列表，竖线分隔，@all 表示全员</summary>
    public String ToUser { get; set; }

    /// <summary>部门 ID 列表，竖线分隔</summary>
    public String ToParty { get; set; }

    /// <summary>标签 ID 列表，竖线分隔</summary>
    public String ToTag { get; set; }

    /// <summary>消息类型，同时作为正文字段名</summary>
    public String MsgType { get; set; }

    /// <summary>消息正文。键名由 MsgType 决定，例如 text、image、template_card</summary>
    public Object Payload { get; set; }

    /// <summary>是否保密消息。0 可对外分享，1 不能分享且显示水印。不设则不传</summary>
    public Int32? Safe { get; set; }

    /// <summary>是否开启 id 转译。0 否 1 是</summary>
    public Int32? EnableIdTrans { get; set; }

    /// <summary>是否开启重复消息检查</summary>
    public Int32? EnableDuplicateCheck { get; set; }

    /// <summary>重复检查间隔，默认 1800 秒，最大 4 小时</summary>
    public Int32? DuplicateCheckInterval { get; set; }

    /// <summary>填接收人。返回自身便于链式调用</summary>
    /// <param name="toUser">成员，可空</param>
    /// <param name="toParty">部门，可空</param>
    /// <param name="toTag">标签，可空</param>
    /// <returns>当前消息</returns>
    public QyWeixinAppMessage To(String toUser, String toParty = null, String toTag = null)
    {
        ToUser = toUser;
        ToParty = toParty;
        ToTag = toTag;
        return this;
    }

    /// <summary>组装 message/send 请求体</summary>
    /// <param name="agentId">应用 ID，整数的十进制文本。与历史发送器一样按字符串写入</param>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToBody(String agentId)
    {
        if (MsgType.IsNullOrEmpty()) throw new ArgumentNullException(nameof(MsgType));
        if (agentId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(agentId));
        if (ToUser.IsNullOrEmpty() && ToParty.IsNullOrEmpty() && ToTag.IsNullOrEmpty())
            throw new ArgumentNullException(nameof(ToUser), "touser/toparty/totag 不能同时为空");

        var body = new Dictionary<String, Object>
        {
            ["touser"] = ToUser ?? "",
            ["toparty"] = ToParty ?? "",
            ["msgtype"] = MsgType,
            ["agentid"] = agentId,
        };
        if (!ToTag.IsNullOrEmpty()) body["totag"] = ToTag;
        if (Payload != null) body[MsgType] = Payload;
        if (Safe != null) body["safe"] = Safe.Value;
        if (EnableIdTrans != null) body["enable_id_trans"] = EnableIdTrans.Value;
        if (EnableDuplicateCheck != null) body["enable_duplicate_check"] = EnableDuplicateCheck.Value;
        if (DuplicateCheckInterval != null) body["duplicate_check_interval"] = DuplicateCheckInterval.Value;
        return body;
    }

    /// <summary>文本。最长 2048 字节</summary>
    /// <param name="content">文本内容</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage Text(String content) => new()
    {
        MsgType = "text",
        Payload = new Dictionary<String, Object> { ["content"] = content ?? "" },
    };

    /// <summary>图片。media_id 来自临时素材</summary>
    /// <param name="mediaId">图片 media_id</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage Image(String mediaId) => Media("image", mediaId);

    /// <summary>语音。仅 AMR，media_id 来自临时素材</summary>
    /// <param name="mediaId">语音 media_id</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage Voice(String mediaId) => Media("voice", mediaId);

    /// <summary>文件</summary>
    /// <param name="mediaId">文件 media_id</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage File(String mediaId) => Media("file", mediaId);

    /// <summary>视频。标题和描述可选</summary>
    /// <param name="mediaId">视频 media_id</param>
    /// <param name="title">标题</param>
    /// <param name="description">描述</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage Video(String mediaId, String title = null, String description = null)
    {
        var payload = new Dictionary<String, Object> { ["media_id"] = mediaId ?? "" };
        QyWeixinValues.Set(payload, "title", title);
        QyWeixinValues.Set(payload, "description", description);
        return new QyWeixinAppMessage { MsgType = "video", Payload = payload };
    }

    /// <summary>文本卡片</summary>
    /// <param name="title">标题，最长 128 字节</param>
    /// <param name="description">描述，最长 512 字节</param>
    /// <param name="url">点击跳转链接</param>
    /// <param name="btnTxt">按钮文字，默认「详情」</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage TextCard(String title, String description, String url, String btnTxt = "详情") => new()
    {
        MsgType = "textcard",
        Payload = new Dictionary<String, Object>
        {
            ["title"] = title ?? "",
            ["description"] = description ?? "",
            ["url"] = url ?? "",
            ["btntxt"] = btnTxt ?? "",
        },
    };

    /// <summary>外链图文，1 到 8 条</summary>
    /// <param name="articles">图文列表</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage News(IEnumerable<QyWeixinNewsArticle> articles)
    {
        var list = (articles ?? Array.Empty<QyWeixinNewsArticle>()).Select(e => e.ToBody()).ToList();
        if (list.Count == 0) throw new ArgumentException("news 至少 1 条", nameof(articles));
        return new QyWeixinAppMessage
        {
            MsgType = "news",
            Payload = new Dictionary<String, Object> { ["articles"] = list },
        };
    }

    /// <summary>企微内打开的图文，1 到 8 条</summary>
    /// <param name="articles">mpnews 列表</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage MpNews(IEnumerable<QyWeixinMpNewsArticle> articles)
    {
        var list = (articles ?? Array.Empty<QyWeixinMpNewsArticle>()).Select(e => e.ToBody()).ToList();
        if (list.Count == 0) throw new ArgumentException("mpnews 至少 1 条", nameof(articles));
        return new QyWeixinAppMessage
        {
            MsgType = "mpnews",
            Payload = new Dictionary<String, Object> { ["articles"] = list },
        };
    }

    /// <summary>markdown。仅企业微信内支持，微信插件会退化成文本</summary>
    /// <param name="content">markdown 正文，最长 2048 字节</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage Markdown(String content) => new()
    {
        MsgType = "markdown",
        Payload = new Dictionary<String, Object> { ["content"] = content ?? "" },
    };

    /// <summary>
    /// 小程序通知。只发成员，不支持部门、标签和 @all。
    /// 文档要求 appid 为小程序，与页面绑定在同一企业。
    /// </summary>
    /// <param name="appId">小程序 appid</param>
    /// <param name="title">标题，4–12 个汉字</param>
    /// <param name="items">内容项，最多 10 条</param>
    /// <param name="page">点击跳转的小程序页面</param>
    /// <param name="description">描述，4–12 个汉字</param>
    /// <param name="emphasisFirstItem">是否放大第一项</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage MiniProgramNotice(String appId, String title, IEnumerable<QyWeixinMiniProgramItem> items, String page = null, String description = null, Boolean emphasisFirstItem = false)
    {
        var payload = new Dictionary<String, Object>
        {
            ["appid"] = appId ?? "",
            ["title"] = title ?? "",
            ["emphasis_first_item"] = emphasisFirstItem,
            ["content_item"] = (items ?? Array.Empty<QyWeixinMiniProgramItem>()).Select(e => e.ToBody()).ToList(),
        };
        QyWeixinValues.Set(payload, "page", page);
        QyWeixinValues.Set(payload, "description", description);
        return new QyWeixinAppMessage { MsgType = "miniprogram_notice", Payload = payload };
    }

    /// <summary>模板卡片。card_type 等字段由调用方放在 templateCard 里</summary>
    /// <param name="templateCard">template_card 对象</param>
    /// <returns>未填接收人的消息</returns>
    public static QyWeixinAppMessage TemplateCard(IDictionary<String, Object> templateCard)
    {
        if (templateCard == null) throw new ArgumentNullException(nameof(templateCard));
        return new QyWeixinAppMessage { MsgType = "template_card", Payload = templateCard };
    }

    private static QyWeixinAppMessage Media(String msgType, String mediaId) => new()
    {
        MsgType = msgType,
        Payload = new Dictionary<String, Object> { ["media_id"] = mediaId ?? "" },
    };
}

/// <summary>应用群聊消息体。没有 agentid，接收人是 chatid</summary>
public class QyWeixinChatMessage
{
    /// <summary>群聊 id</summary>
    public String ChatId { get; set; }

    /// <summary>消息类型</summary>
    public String MsgType { get; set; }

    /// <summary>正文，结构与应用消息相同类型一致</summary>
    public Object Payload { get; set; }

    /// <summary>是否保密。不设则不传</summary>
    public Int32? Safe { get; set; }

    /// <summary>组装 appchat/send 请求体</summary>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToBody()
    {
        if (ChatId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ChatId));
        if (MsgType.IsNullOrEmpty()) throw new ArgumentNullException(nameof(MsgType));
        var body = new Dictionary<String, Object>
        {
            ["chatid"] = ChatId,
            ["msgtype"] = MsgType,
        };
        if (Payload != null) body[MsgType] = Payload;
        if (Safe != null) body["safe"] = Safe.Value;
        return body;
    }

    /// <summary>从应用消息复制类型和正文，改投到群聊</summary>
    /// <param name="chatId">群聊 id</param>
    /// <param name="message">已组装的应用消息</param>
    /// <returns>群聊消息</returns>
    public static QyWeixinChatMessage FromApp(String chatId, QyWeixinAppMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        return new QyWeixinChatMessage
        {
            ChatId = chatId,
            MsgType = message.MsgType,
            Payload = message.Payload,
            Safe = message.Safe,
        };
    }
}
