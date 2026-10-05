using NewLife;

namespace NewLife.Cube.QyWeixin;

public partial class QyWeixinClient
{
    /// <summary>
    /// 发送应用消息并返回 msgid。文档 https://developer.work.weixin.qq.com/document/path/90236
    /// 接收人已写在 <paramref name="message"/> 上。agentid 取当前客户端。
    /// </summary>
    /// <param name="message">消息体</param>
    /// <returns>msgid 及无效接收人</returns>
    public async Task<QyWeixinSendResult> SendMessageAsync(QyWeixinAppMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("message/send?access_token=" + token, message.ToBody(RequireAgent())).ConfigureAwait(false);
        return QyWeixinSendResult.From(raw);
    }

    /// <summary>发送文本。签名与历史版本一致，不返回 msgid。需要 msgid 时用 <see cref="SendMessageAsync"/></summary>
    /// <param name="touser">成员列表，@all 表示全员，可空</param>
    /// <param name="toparty">部门 ID 列表，可空</param>
    /// <param name="content">文本，最长 2048 字节</param>
    /// <returns>发送任务</returns>
    public Task SendTextAsync(String touser, String toparty, String content)
        => SendMessageAsync(QyWeixinAppMessage.Text(content).To(touser, toparty));

    /// <summary>发送文本卡片。按钮文字默认「查看」，与历史版本一致</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门 ID 列表，可空</param>
    /// <param name="title">标题</param>
    /// <param name="description">描述</param>
    /// <param name="url">跳转链接</param>
    /// <param name="btnTxt">按钮文字</param>
    /// <returns>发送任务</returns>
    public Task SendTextCardAsync(String touser, String toparty, String title, String description, String url, String btnTxt = "查看")
        => SendMessageAsync(QyWeixinAppMessage.TextCard(title, description, url, btnTxt).To(touser, toparty));

    /// <summary>发送模板卡片。card_type 由调用方放在 templateCard 中</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门 ID 列表，可空</param>
    /// <param name="templateCard">template_card 对象</param>
    /// <returns>发送任务</returns>
    public Task SendTemplateCardAsync(String touser, String toparty, IDictionary<String, Object> templateCard)
        => SendMessageAsync(QyWeixinAppMessage.TemplateCard(templateCard).To(touser, toparty));

    /// <summary>发送图片</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="mediaId">图片 media_id</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendImageAsync(String touser, String toparty, String mediaId, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.Image(mediaId).To(touser, toparty, totag));

    /// <summary>发送语音</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="mediaId">语音 media_id</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendVoiceAsync(String touser, String toparty, String mediaId, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.Voice(mediaId).To(touser, toparty, totag));

    /// <summary>发送视频</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="mediaId">视频 media_id</param>
    /// <param name="title">标题，可空</param>
    /// <param name="description">描述，可空</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendVideoAsync(String touser, String toparty, String mediaId, String title = null, String description = null, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.Video(mediaId, title, description).To(touser, toparty, totag));

    /// <summary>发送文件</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="mediaId">文件 media_id</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendFileAsync(String touser, String toparty, String mediaId, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.File(mediaId).To(touser, toparty, totag));

    /// <summary>发送外链图文</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="articles">1 到 8 条</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendNewsAsync(String touser, String toparty, IEnumerable<QyWeixinNewsArticle> articles, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.News(articles).To(touser, toparty, totag));

    /// <summary>发送 mpnews</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="articles">1 到 8 条</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendMpNewsAsync(String touser, String toparty, IEnumerable<QyWeixinMpNewsArticle> articles, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.MpNews(articles).To(touser, toparty, totag));

    /// <summary>发送 markdown</summary>
    /// <param name="touser">成员列表，可空</param>
    /// <param name="toparty">部门列表，可空</param>
    /// <param name="content">markdown 正文</param>
    /// <param name="totag">标签列表，可空</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendMarkdownAsync(String touser, String toparty, String content, String totag = null)
        => SendMessageAsync(QyWeixinAppMessage.Markdown(content).To(touser, toparty, totag));

    /// <summary>发送小程序通知。只应填 touser</summary>
    /// <param name="touser">成员列表。不要用 @all</param>
    /// <param name="appId">小程序 appid</param>
    /// <param name="title">标题</param>
    /// <param name="items">内容项</param>
    /// <param name="page">小程序页面，可空</param>
    /// <param name="description">描述，可空</param>
    /// <param name="emphasisFirstItem">是否放大第一项</param>
    /// <returns>发送结果</returns>
    public Task<QyWeixinSendResult> SendMiniProgramNoticeAsync(String touser, String appId, String title, IEnumerable<QyWeixinMiniProgramItem> items, String page = null, String description = null, Boolean emphasisFirstItem = false)
        => SendMessageAsync(QyWeixinAppMessage.MiniProgramNotice(appId, title, items, page, description, emphasisFirstItem).To(touser));

    /// <summary>
    /// 撤回 24 小时内的应用消息。仅企业微信端，微信插件端不撤回。
    /// 文档 https://developer.work.weixin.qq.com/document/path/94947
    /// </summary>
    /// <param name="msgId">message/send 返回的 msgid</param>
    /// <returns>撤回任务</returns>
    public async Task RecallMessageAsync(String msgId)
    {
        if (msgId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(msgId));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("message/recall?access_token=" + token, new { msgid = msgId }).ConfigureAwait(false);
    }

    /// <summary>
    /// 更新任务卡片按钮文案。历史接口 message/update_taskcard，签名保持不变。
    /// 新卡片请用 <see cref="UpdateTemplateCardAsync"/>（文档 94888）。
    /// </summary>
    /// <param name="userIds">企微账号列表</param>
    /// <param name="taskId">发送时的 task_id</param>
    /// <param name="replacedName">替换后的按钮文案</param>
    /// <returns>更新任务</returns>
    public async Task UpdateTaskCardAsync(IEnumerable<String> userIds, String taskId, String replacedName)
    {
        if (userIds == null) throw new ArgumentNullException(nameof(userIds));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostAsync<Object>("message/update_taskcard?access_token=" + token,
            new { userids = userIds.ToArray(), agentid = AgentId, task_id = taskId, replaced_name = replacedName }, "");
    }

    /// <summary>
    /// 更新模板卡片。文档 https://developer.work.weixin.qq.com/document/path/94888
    /// replaceName 与 templateCard 二选一：前者只替换按钮文案，后者整卡替换。
    /// response_code 来自 template_card_event，72 小时内且只能使用一次。
    /// </summary>
    /// <param name="responseCode">回调里的 ResponseCode</param>
    /// <param name="replaceName">按钮替换文案。与 templateCard 互斥</param>
    /// <param name="templateCard">整张新卡片。与 replaceName 互斥</param>
    /// <param name="userIds">要更新的成员，可空表示按 response_code 覆盖</param>
    /// <param name="partyIds">部门，可空</param>
    /// <param name="tagIds">标签，可空</param>
    /// <param name="atAll">是否更新全部接收人</param>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>接口响应</returns>
    public async Task<IDictionary<String, Object>> UpdateTemplateCardAsync(String responseCode, String replaceName = null, IDictionary<String, Object> templateCard = null, IEnumerable<String> userIds = null, IEnumerable<String> partyIds = null, IEnumerable<String> tagIds = null, Boolean atAll = false, String agentId = null)
    {
        if (responseCode.IsNullOrEmpty()) throw new ArgumentNullException(nameof(responseCode));
        var hasName = !replaceName.IsNullOrEmpty();
        var hasCard = templateCard != null;
        if (hasName == hasCard)
            throw new ArgumentException("replaceName 与 templateCard 必须且只能填一个");

        var body = new Dictionary<String, Object>
        {
            ["agentid"] = QyWeixinValues.Agent(RequireAgent(agentId)),
            ["response_code"] = responseCode,
            ["atall"] = atAll ? 1 : 0,
        };
        if (userIds != null) body["userids"] = userIds.ToArray();
        if (partyIds != null) body["partyids"] = partyIds.ToArray();
        if (tagIds != null) body["tagids"] = tagIds.ToArray();
        if (!replaceName.IsNullOrEmpty())
            body["button"] = new Dictionary<String, Object> { ["replace_name"] = replaceName };
        else
            body["template_card"] = templateCard;

        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync("message/update_template_card?access_token=" + token, body).ConfigureAwait(false);
    }
}
