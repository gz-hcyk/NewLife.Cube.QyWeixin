using System.Text;
using NewLife.Web.OAuth;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信应用消息发送器。基于魔方 <see cref="QyWeiXin"/> 派生扩展发送能力
/// （父类 PostAsync 为 protected virtual，派生类可访问），不修改框架源码。
/// </summary>
public class QyWeiXinSender : QyWeiXin
{
    /// <summary>发送文本消息。touser/toparty 至少一个非空，多个目标用 | 分隔</summary>
    /// <param name="touser">企微账号列表，@all 表示全员，可空</param>
    /// <param name="toparty">企微部门 ID 列表，可空</param>
    /// <param name="content">文本内容（最长 2048 字节，超长由调用方截断）</param>
    public Task SendTextAsync(String touser, String toparty, String content)
        => SendAsync(touser, toparty, new Dictionary<String, Object>
        {
            ["msgtype"] = "text",
            ["text"] = new { content },
        });

    /// <summary>发送文本卡片消息（可点击跳转）。</summary>
    /// <param name="touser">企微账号列表，@all 表示全员，可空</param>
    /// <param name="toparty">企微部门 ID 列表，可空</param>
    /// <param name="title">标题（最长 128 字节）</param>
    /// <param name="description">描述（最长 512 字节）</param>
    /// <param name="url">点击跳转链接</param>
    /// <param name="btnTxt">按钮文字</param>
    public Task SendTextCardAsync(String touser, String toparty, String title, String description, String url, String btnTxt = "查看")
        => SendAsync(touser, toparty, new Dictionary<String, Object>
        {
            ["msgtype"] = "textcard",
            ["textcard"] = new { title, description, url, btntxt = btnTxt },
        });

    /// <summary>发送模板卡片消息（card_type 由调用方决定，如 text_notice/button_interaction）。</summary>
    /// <param name="touser">企微账号列表，@all 表示全员，可空</param>
    /// <param name="toparty">企微部门 ID 列表，可空</param>
    /// <param name="templateCard">template_card 消息体</param>
    public Task SendTemplateCardAsync(String touser, String toparty, IDictionary<String, Object> templateCard)
        => SendAsync(touser, toparty, new Dictionary<String, Object>
        {
            ["msgtype"] = "template_card",
            ["template_card"] = templateCard,
        });

    /// <summary>更新任务卡片（update_taskcard）：把按钮替换为终态文案（如「已处理」），仅影响指定用户的卡片。</summary>
    /// <param name="userIds">企微账号列表（点击人）</param>
    /// <param name="taskId">任务 id（发送侧 task_id 原样回传）</param>
    /// <param name="replacedName">按钮替换文案</param>
    public async Task UpdateTaskCardAsync(IEnumerable<String> userIds, String taskId, String replacedName)
    {
        var token = await GetAccessToken();
        await PostAsync<Object>("message/update_taskcard?access_token=" + token,
            new { userids = userIds.ToArray(), agentid = AgentId, task_id = taskId, replaced_name = replacedName }, "");
    }

    private async Task SendAsync(String touser, String toparty, IDictionary<String, Object> body)
    {
        if ((touser + toparty).IsNullOrEmpty())
            throw new ArgumentNullException(nameof(touser), "touser/toparty 不能同时为空");

        body["touser"] = touser.IsNullOrEmpty() ? "" : touser;
        body["toparty"] = toparty.IsNullOrEmpty() ? "" : toparty;
        body["agentid"] = AgentId;

        var token = await GetAccessToken();
        await PostAsync<Object>("message/send?access_token=" + token, body, "");
    }
}
