using System.Net.Http.Headers;
using System.Text;
using NewLife;
using NewLife.Remoting;
using NewLife.Web.OAuth;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信自建应用服务端 API 客户端。派生自魔方 <see cref="QyWeiXin"/>，
/// 复用 protected 的 GetAsync / PostAsync / GetQyClient / GetAccessToken，不改框架源码。
/// </summary>
/// <remarks>
/// 凭据：显式 CorpId、Secret、AgentId，或魔方 OAuth 的 AppId 写成 corp#agent。见 <see cref="QyWeixinCredential"/>。
/// 不实现客户联系（externalcontact）、获客助手、微信客服、家校、政民、会议全量、微盘全量、企业支付、会话存档。
/// 通讯录写接口只推到企微，不改魔方部门/用户；本地拉取见 <see cref="QyWeixinService"/> 的冲突策略。
/// </remarks>
public partial class QyWeixinClient : QyWeiXin
{
    /// <summary>cgi-bin 根地址，与魔方客户端 BaseAddress 一致</summary>
    public const String ApiRoot = "https://qyapi.weixin.qq.com/cgi-bin/";

    private String _corpTicket;
    private DateTime _corpTicketExpire;
    private String _agentTicket;
    private DateTime _agentTicketExpire;

    /// <summary>POST JSON。dataName 为空时返回整包，errcode 非 0 由基类抛 ApiException</summary>
    /// <param name="action">相对路径，可带 query</param>
    /// <param name="body">请求体，匿名对象或字典</param>
    /// <returns>响应字典</returns>
    protected Task<IDictionary<String, Object>> PostJsonAsync(String action, Object body)
        => PostAsync<IDictionary<String, Object>>(action, body, "");

    /// <summary>GET JSON。参数拼在 query 上</summary>
    /// <param name="action">相对路径</param>
    /// <param name="args">query 参数，可空</param>
    /// <returns>响应字典</returns>
    protected Task<IDictionary<String, Object>> GetJsonAsync(String action, Object args = null)
        => GetAsync<IDictionary<String, Object>>(action, args, "");

    /// <summary>取 access_token。沿用基类缓存：未过期（提前 1 分钟）则复用</summary>
    /// <returns>access_token</returns>
    protected Task<String> TokenAsync() => GetAccessToken();

    /// <summary>解析应用 ID。参数优先，否则用 Init 后的 AgentId</summary>
    /// <param name="agentId">显式应用 ID，可空</param>
    /// <returns>应用 ID</returns>
    protected String RequireAgent(String agentId = null)
    {
        var id = agentId.IsNullOrEmpty() ? AgentId : agentId;
        if (id.IsNullOrEmpty()) throw new ArgumentNullException(nameof(agentId), "未设置 AgentId（AppId 需为 corp#agent，或显式传入）");
        return id;
    }

    /// <summary>读取 HTTP 响应文本并按企微 errcode 处理</summary>
    /// <param name="response">原始响应</param>
    /// <returns>成功时的整包字典</returns>
    protected async Task<IDictionary<String, Object>> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (Log != null && Log.Enable) WriteLog("RAW {0} {1}", response.RequestMessage?.RequestUri, text);
        return ApiHelper.ProcessResponse<IDictionary<String, Object>>(text, null, "");
    }

    /// <summary>上传 multipart。成功响应走 errcode 校验</summary>
    /// <param name="action">相对路径，已含 access_token 与 type</param>
    /// <param name="data">文件字节</param>
    /// <param name="fileName">文件名，会作为消息里展示的名字</param>
    /// <param name="contentType">内容类型</param>
    /// <returns>响应字典</returns>
    protected async Task<IDictionary<String, Object>> PostMultipartAsync(String action, Byte[] data, String fileName, String contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType.IsNullOrEmpty() ? "application/octet-stream" : contentType);
        form.Add(file, "media", fileName.IsNullOrEmpty() ? "file" : fileName);

        using var response = await GetQyClient().PostAsync(action, form).ConfigureAwait(false);
        return await ReadJsonAsync(response).ConfigureAwait(false);
    }

    /// <summary>下载临时素材。企微成功时带 Content-Disposition；失败时是 JSON</summary>
    /// <param name="action">相对路径</param>
    /// <returns>文件内容</returns>
    protected async Task<QyWeixinMediaFile> DownloadAsync(String action)
    {
        using var response = await GetQyClient().GetAsync(action).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName;
        if (!fileName.IsNullOrEmpty()) fileName = fileName.Trim('"');

        if (fileName.IsNullOrEmpty() && bytes != null && bytes.Length > 0 && bytes[0] == (Byte)'{')
        {
            var text = Encoding.UTF8.GetString(bytes);
            if (text.Contains("errcode"))
                ApiHelper.ProcessResponse<Object>(text, null, "");
        }

        return new QyWeixinMediaFile
        {
            Content = bytes,
            FileName = fileName,
            ContentType = response.Content.Headers.ContentType?.MediaType,
        };
    }
}
