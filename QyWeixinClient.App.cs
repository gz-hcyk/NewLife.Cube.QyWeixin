using NewLife;

namespace NewLife.Cube.QyWeixin;

public partial class QyWeixinClient
{
    /// <summary>
    /// 获取应用。文档 https://developer.work.weixin.qq.com/document/path/90227
    /// </summary>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>应用详情</returns>
    public async Task<IDictionary<String, Object>> GetAgentAsync(String agentId = null)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("agent/get", new { access_token = token, agentid = RequireAgent(agentId) }).ConfigureAwait(false);
    }

    /// <summary>
    /// 设置应用。文档 https://developer.work.weixin.qq.com/document/path/90228
    /// 调用方放入要改的字段（name、description、redirect_domain、logo_mediaid、report_location_flag、isreportenter、home_url 等）。
    /// agentid 由本方法写入。
    /// </summary>
    /// <param name="fields">要更新的字段</param>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>设置任务</returns>
    public async Task SetAgentAsync(IDictionary<String, Object> fields, String agentId = null)
    {
        if (fields == null) throw new ArgumentNullException(nameof(fields));
        var body = new Dictionary<String, Object>(fields) { ["agentid"] = QyWeixinValues.Agent(RequireAgent(agentId)) };
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("agent/set?access_token=" + token, body).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建菜单。文档 https://developer.work.weixin.qq.com/document/path/90231
    /// body 形如 button 数组，由调用方按文档组装。
    /// </summary>
    /// <param name="menu">菜单 JSON 对象</param>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>创建任务</returns>
    public async Task CreateMenuAsync(Object menu, String agentId = null)
    {
        if (menu == null) throw new ArgumentNullException(nameof(menu));
        var token = await TokenAsync().ConfigureAwait(false);
        var id = Uri.EscapeDataString(RequireAgent(agentId));
        await PostJsonAsync($"menu/create?access_token={token}&agentid={id}", menu).ConfigureAwait(false);
    }

    /// <summary>获取菜单。文档 https://developer.work.weixin.qq.com/document/path/90232</summary>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>菜单</returns>
    public async Task<IDictionary<String, Object>> GetMenuAsync(String agentId = null)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("menu/get", new { access_token = token, agentid = RequireAgent(agentId) }).ConfigureAwait(false);
    }

    /// <summary>删除菜单。文档 https://developer.work.weixin.qq.com/document/path/90233</summary>
    /// <param name="agentId">应用 ID，空则用当前客户端</param>
    /// <returns>删除任务</returns>
    public async Task DeleteMenuAsync(String agentId = null)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        await GetJsonAsync("menu/delete", new { access_token = token, agentid = RequireAgent(agentId) }).ConfigureAwait(false);
    }

    /// <summary>
    /// userid 转 openid。文档 https://developer.work.weixin.qq.com/document/path/90202
    /// </summary>
    /// <param name="userId">成员 userid</param>
    /// <returns>openid</returns>
    public async Task<String> ConvertToOpenIdAsync(String userId)
    {
        if (userId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(userId));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("user/convert_to_openid?access_token=" + token, new { userid = userId }).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "openid");
    }

    /// <summary>
    /// openid 转 userid。与转换接口同一文档 https://developer.work.weixin.qq.com/document/path/90202
    /// </summary>
    /// <param name="openId">openid</param>
    /// <returns>userid</returns>
    public async Task<String> ConvertToUserIdAsync(String openId)
    {
        if (openId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(openId));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("user/convert_to_userid?access_token=" + token, new { openid = openId }).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "userid");
    }

    /// <summary>
    /// 网页授权 code 换身份。使用当前官方 auth/getuserinfo。
    /// 文档 https://developer.work.weixin.qq.com/document/path/91023
    /// 魔方登录回调仍走 QyWeiXin.UserUrl（user/getuserinfo），本方法不替换该流程。
    /// </summary>
    /// <param name="code">授权码，一次性</param>
    /// <returns>userid / user_ticket 等</returns>
    public async Task<QyWeixinCodeIdentity> GetUserByCodeAsync(String code)
    {
        if (code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(code));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await GetJsonAsync("auth/getuserinfo", new { access_token = token, code }).ConfigureAwait(false);
        return QyWeixinCodeIdentity.From(raw);
    }

    /// <summary>
    /// 与魔方 OAuth 相同的旧接口 user/getuserinfo。仅在需要和登录回调对齐排查时使用。
    /// </summary>
    /// <param name="code">授权码</param>
    /// <returns>身份。旧接口字段名是 UserId</returns>
    public async Task<QyWeixinCodeIdentity> GetUserByCodeLegacyAsync(String code)
    {
        if (code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(code));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await GetJsonAsync("user/getuserinfo", new { access_token = token, code }).ConfigureAwait(false);
        return QyWeixinCodeIdentity.From(raw);
    }

    /// <summary>
    /// 用 user_ticket 换敏感信息（手机、邮箱等）。文档 https://developer.work.weixin.qq.com/document/path/95833
    /// 成员必须已点过 snsapi_privateinfo 授权。
    /// </summary>
    /// <param name="userTicket">auth/getuserinfo 返回的 user_ticket</param>
    /// <returns>成员敏感字段</returns>
    public async Task<IDictionary<String, Object>> GetUserDetailByTicketAsync(String userTicket)
    {
        if (userTicket.IsNullOrEmpty()) throw new ArgumentNullException(nameof(userTicket));
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync("auth/getuserdetail?access_token=" + token, new { user_ticket = userTicket }).ConfigureAwait(false);
    }

    /// <summary>
    /// 企业 jsapi_ticket，给 wx.config / ww.register 的 getConfigSignature。
    /// 文档 https://developer.work.weixin.qq.com/document/path/90506
    /// 实例内缓存，提前 1 分钟刷新。频率限制很严，不要每次签名都打接口。
    /// </summary>
    /// <param name="refresh">true 时忽略缓存</param>
    /// <returns>ticket</returns>
    public async Task<String> GetJsApiTicketAsync(Boolean refresh = false)
    {
        if (!refresh && !_corpTicket.IsNullOrEmpty() && _corpTicketExpire > DateTime.Now.AddMinutes(1))
            return _corpTicket;

        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await GetJsonAsync("get_jsapi_ticket", new { access_token = token }).ConfigureAwait(false);
        _corpTicket = QyWeixinValues.Pick(raw, "ticket");
        var exp = QyWeixinValues.PickInt(raw, "expires_in");
        _corpTicketExpire = DateTime.Now.AddSeconds(exp > 0 ? exp : 7200);
        return _corpTicket;
    }

    /// <summary>
    /// 应用 jsapi_ticket（type=agent_config），给 agentConfig / getAgentConfigSignature。
    /// 与企业 ticket 不是同一个，缓存分开。
    /// </summary>
    /// <param name="refresh">true 时忽略缓存</param>
    /// <returns>ticket</returns>
    public async Task<String> GetAgentJsApiTicketAsync(Boolean refresh = false)
    {
        if (!refresh && !_agentTicket.IsNullOrEmpty() && _agentTicketExpire > DateTime.Now.AddMinutes(1))
            return _agentTicket;

        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await GetJsonAsync("ticket/get", new { access_token = token, type = "agent_config" }).ConfigureAwait(false);
        _agentTicket = QyWeixinValues.Pick(raw, "ticket");
        var exp = QyWeixinValues.PickInt(raw, "expires_in");
        _agentTicketExpire = DateTime.Now.AddSeconds(exp > 0 ? exp : 7200);
        return _agentTicket;
    }

    /// <summary>计算 wx.config 签名。ticket 不下发</summary>
    /// <param name="url">当前页面 URL，可含 hash，签名前会去掉</param>
    /// <returns>timestamp、nonceStr、signature</returns>
    public async Task<QyWeixinJsConfig> CreateConfigSignatureAsync(String url)
    {
        var ticket = await GetJsApiTicketAsync().ConfigureAwait(false);
        return QyWeixinJsSignature.Build(ticket, url);
    }

    /// <summary>计算 agentConfig 签名</summary>
    /// <param name="url">当前页面 URL</param>
    /// <returns>timestamp、nonceStr、signature</returns>
    public async Task<QyWeixinJsConfig> CreateAgentConfigSignatureAsync(String url)
    {
        var ticket = await GetAgentJsApiTicketAsync().ConfigureAwait(false);
        return QyWeixinJsSignature.Build(ticket, url);
    }
}
