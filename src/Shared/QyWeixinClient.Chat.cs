using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>工作台关键数据的一项</summary>
public class QyWeixinWorkbenchItem
{
    /// <summary>关键数据名称，如「待审批」</summary>
    public String Key { get; set; }

    /// <summary>关键数据值</summary>
    public String Data { get; set; }

    /// <summary>点击跳转 URL。与 PagePath 二选一</summary>
    public String JumpUrl { get; set; }

    /// <summary>小程序页面路径</summary>
    public String PagePath { get; set; }

    /// <summary>转成 items 元素</summary>
    /// <returns>字典</returns>
    public Dictionary<String, Object> ToBody()
    {
        var body = new Dictionary<String, Object>();
        QyWeixinValues.Set(body, "key", Key);
        QyWeixinValues.Set(body, "data", Data);
        QyWeixinValues.Set(body, "jump_url", JumpUrl);
        QyWeixinValues.Set(body, "pagepath", PagePath);
        return body;
    }
}

public partial class QyWeixinClient
{
    /// <summary>
    /// 创建应用群聊。文档 https://developer.work.weixin.qq.com/document/path/90245
    /// 仅自建应用，且可见范围须为根部门。至少 2 人。
    /// </summary>
    /// <param name="userIds">成员 userid，2 到 2000</param>
    /// <param name="name">群名，可空</param>
    /// <param name="owner">群主 userid，可空则随机</param>
    /// <param name="chatId">指定群 id，可空则由企微生成。只允许字母数字，最长 32</param>
    /// <returns>chatid</returns>
    public async Task<String> CreateAppChatAsync(IEnumerable<String> userIds, String name = null, String owner = null, String chatId = null)
    {
        var users = userIds?.Where(e => !e.IsNullOrEmpty()).ToArray() ?? Array.Empty<String>();
        if (users.Length < 2) throw new ArgumentException("userlist 至少 2 人", nameof(userIds));
        var body = new Dictionary<String, Object> { ["userlist"] = users };
        QyWeixinValues.Set(body, "name", name);
        QyWeixinValues.Set(body, "owner", owner);
        QyWeixinValues.Set(body, "chatid", chatId);
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("appchat/create?access_token=" + token, body).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "chatid");
    }

    /// <summary>
    /// 修改应用群聊。文档 https://developer.work.weixin.qq.com/document/path/98913
    /// 群必须由本应用创建。
    /// </summary>
    /// <param name="chatId">群 id</param>
    /// <param name="name">新群名，可空表示不改</param>
    /// <param name="owner">新群主，可空表示不改</param>
    /// <param name="addUserIds">要加入的成员</param>
    /// <param name="delUserIds">要移出的成员</param>
    /// <returns>修改任务</returns>
    public async Task UpdateAppChatAsync(String chatId, String name = null, String owner = null, IEnumerable<String> addUserIds = null, IEnumerable<String> delUserIds = null)
    {
        if (chatId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(chatId));
        var body = new Dictionary<String, Object> { ["chatid"] = chatId };
        QyWeixinValues.Set(body, "name", name);
        QyWeixinValues.Set(body, "owner", owner);
        var add = addUserIds?.Where(e => !e.IsNullOrEmpty()).ToArray();
        var del = delUserIds?.Where(e => !e.IsNullOrEmpty()).ToArray();
        if (add != null && add.Length > 0) body["add_user_list"] = add;
        if (del != null && del.Length > 0) body["del_user_list"] = del;
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("appchat/update?access_token=" + token, body).ConfigureAwait(false);
    }

    /// <summary>获取应用群聊。接口 appchat/get，与创建/修改同属「应用发送消息到群聊会话」</summary>
    /// <param name="chatId">群 id</param>
    /// <returns>chat_info 所在的整包</returns>
    public async Task<IDictionary<String, Object>> GetAppChatAsync(String chatId)
    {
        if (chatId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(chatId));
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("appchat/get", new { access_token = token, chatid = chatId }).ConfigureAwait(false);
    }

    /// <summary>
    /// 往应用群聊发消息。文档 https://developer.work.weixin.qq.com/document/path/90248
    /// 正文结构与应用消息相同，用 <see cref="QyWeixinChatMessage.FromApp"/> 转换。
    /// </summary>
    /// <param name="message">群消息</param>
    /// <returns>发送任务</returns>
    public async Task SendAppChatAsync(QyWeixinChatMessage message)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("appchat/send?access_token=" + token, message.ToBody()).ConfigureAwait(false);
    }

    /// <summary>
    /// 设置工作台模板。文档 https://developer.work.weixin.qq.com/document/path/92535
    /// type 为 keydata、image、list、webview。本方法提供 keydata 的组装，其他类型用 <see cref="SetWorkbenchTemplateRawAsync"/>。
    /// </summary>
    /// <param name="items">关键数据，最多 4 项（以文档为准）</param>
    /// <param name="replaceUserData">是否覆盖用户个性化数据</param>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>设置任务</returns>
    public Task SetWorkbenchKeyDataTemplateAsync(IEnumerable<QyWeixinWorkbenchItem> items, Boolean replaceUserData = true, String agentId = null)
    {
        var body = KeyDataBody(agentId, items);
        body["replace_user_data"] = replaceUserData;
        return SetWorkbenchTemplateRawAsync(body);
    }

    /// <summary>设置工作台模板，请求体原样提交。调用方需自带 type 与对应节点；agentid 若缺则补上</summary>
    /// <param name="body">请求体</param>
    /// <returns>设置任务</returns>
    public async Task SetWorkbenchTemplateRawAsync(IDictionary<String, Object> body)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (!body.ContainsKey("agentid")) body["agentid"] = QyWeixinValues.Agent(RequireAgent());
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("agent/set_workbench_template?access_token=" + token, body).ConfigureAwait(false);
    }

    /// <summary>读取工作台模板。接口 agent/get_workbench_template</summary>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>模板</returns>
    public async Task<IDictionary<String, Object>> GetWorkbenchTemplateAsync(String agentId = null)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("agent/get_workbench_template", new { access_token = token, agentid = RequireAgent(agentId) }).ConfigureAwait(false);
    }

    /// <summary>给指定成员设置工作台关键数据。接口 agent/set_workbench_data</summary>
    /// <param name="userId">成员 userid</param>
    /// <param name="items">关键数据</param>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>设置任务</returns>
    public Task SetWorkbenchKeyDataAsync(String userId, IEnumerable<QyWeixinWorkbenchItem> items, String agentId = null)
    {
        if (userId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(userId));
        var body = KeyDataBody(agentId, items);
        body["userid"] = userId;
        return SetWorkbenchDataRawAsync(body);
    }

    /// <summary>设置工作台数据，请求体原样提交</summary>
    /// <param name="body">请求体，需含 userid 或批量字段</param>
    /// <returns>设置任务</returns>
    public async Task SetWorkbenchDataRawAsync(IDictionary<String, Object> body)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        if (!body.ContainsKey("agentid")) body["agentid"] = QyWeixinValues.Agent(RequireAgent());
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("agent/set_workbench_data?access_token=" + token, body).ConfigureAwait(false);
    }

    private Dictionary<String, Object> KeyDataBody(String agentId, IEnumerable<QyWeixinWorkbenchItem> items)
    {
        var list = (items ?? Array.Empty<QyWeixinWorkbenchItem>()).Select(e => e.ToBody()).ToList();
        if (list.Count == 0) throw new ArgumentException("keydata.items 不能为空", nameof(items));
        return new Dictionary<String, Object>
        {
            ["agentid"] = QyWeixinValues.Agent(RequireAgent(agentId)),
            ["type"] = "keydata",
            ["keydata"] = new Dictionary<String, Object> { ["items"] = list },
        };
    }
}
