using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>创建或更新部门的请求。文档 https://developer.work.weixin.qq.com/document/path/90205</summary>
public class QyWeixinDepartment
{
    /// <summary>部门 id。创建时可选且必须大于 1；更新时必填</summary>
    public Int32? Id { get; set; }

    /// <summary>部门名称。同级不能重复</summary>
    public String Name { get; set; }

    /// <summary>英文名。需后台开启多语言才生效</summary>
    public String NameEn { get; set; }

    /// <summary>父部门 id。根部门的子部门父 id 为 1</summary>
    public Int32? ParentId { get; set; }

    /// <summary>次序。值大的排前面</summary>
    public Int32? Order { get; set; }

    /// <summary>创建请求体。name 与 parentid 必填</summary>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToCreateBody()
    {
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name));
        if (ParentId == null) throw new ArgumentNullException(nameof(ParentId));
        var body = new Dictionary<String, Object>
        {
            ["name"] = Name,
            ["parentid"] = ParentId.Value,
        };
        QyWeixinValues.Set(body, "name_en", NameEn);
        if (Order != null) body["order"] = Order.Value;
        if (Id != null) body["id"] = Id.Value;
        return body;
    }

    /// <summary>更新请求体。id 必填，其余字段有值才写入</summary>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToUpdateBody()
    {
        if (Id == null) throw new ArgumentNullException(nameof(Id));
        var body = new Dictionary<String, Object> { ["id"] = Id.Value };
        QyWeixinValues.Set(body, "name", Name);
        QyWeixinValues.Set(body, "name_en", NameEn);
        if (ParentId != null) body["parentid"] = ParentId.Value;
        if (Order != null) body["order"] = Order.Value;
        return body;
    }
}

/// <summary>
/// 创建或更新成员的请求。文档 https://developer.work.weixin.qq.com/document/path/90195
/// 只覆盖自建应用通讯录常用字段，扩展属性请用 <see cref="Extra"/> 并入。
/// </summary>
public class QyWeixinMember
{
    /// <summary>成员 userid</summary>
    public String UserId { get; set; }

    /// <summary>姓名。创建时必填</summary>
    public String Name { get; set; }

    /// <summary>别名</summary>
    public String Alias { get; set; }

    /// <summary>手机。与邮箱不能同时为空（创建时）</summary>
    public String Mobile { get; set; }

    /// <summary>邮箱</summary>
    public String Email { get; set; }

    /// <summary>企业邮箱</summary>
    public String BizMail { get; set; }

    /// <summary>部门 id 列表</summary>
    public Int32[] Department { get; set; }

    /// <summary>部门内次序，与 Department 一一对应</summary>
    public Int32[] Order { get; set; }

    /// <summary>职务</summary>
    public String Position { get; set; }

    /// <summary>性别。1 男，2 女</summary>
    public Int32? Gender { get; set; }

    /// <summary>座机</summary>
    public String Telephone { get; set; }

    /// <summary>是否部门负责人，与 Department 一一对应。1 是 0 否</summary>
    public Int32[] IsLeaderInDept { get; set; }

    /// <summary>启用。1 启用 0 禁用</summary>
    public Int32? Enable { get; set; }

    /// <summary>头像临时素材 id</summary>
    public String AvatarMediaId { get; set; }

    /// <summary>地址</summary>
    public String Address { get; set; }

    /// <summary>主部门</summary>
    public Int32? MainDepartment { get; set; }

    /// <summary>创建时是否邀请。null 表示不传，由企微默认（邀请）</summary>
    public Boolean? ToInvite { get; set; }

    /// <summary>文档中的其他字段，原样并入。同名时覆盖上面的属性</summary>
    public IDictionary<String, Object> Extra { get; set; }

    /// <summary>创建请求体</summary>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToCreateBody()
    {
        if (UserId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(UserId));
        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Name));
        var body = ToUpdateBody();
        body["name"] = Name;
        if (ToInvite != null) body["to_invite"] = ToInvite.Value;
        return body;
    }

    /// <summary>更新请求体。userid 必填</summary>
    /// <returns>请求字典</returns>
    public Dictionary<String, Object> ToUpdateBody()
    {
        if (UserId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(UserId));
        var body = new Dictionary<String, Object> { ["userid"] = UserId };
        QyWeixinValues.Set(body, "name", Name);
        QyWeixinValues.Set(body, "alias", Alias);
        QyWeixinValues.Set(body, "mobile", Mobile);
        QyWeixinValues.Set(body, "email", Email);
        QyWeixinValues.Set(body, "biz_mail", BizMail);
        QyWeixinValues.Set(body, "position", Position);
        QyWeixinValues.Set(body, "telephone", Telephone);
        QyWeixinValues.Set(body, "address", Address);
        QyWeixinValues.Set(body, "avatar_mediaid", AvatarMediaId);
        if (Department != null) body["department"] = Department;
        if (Order != null) body["order"] = Order;
        if (Gender != null) body["gender"] = Gender.Value.ToString();
        if (IsLeaderInDept != null) body["is_leader_in_dept"] = IsLeaderInDept;
        if (Enable != null) body["enable"] = Enable.Value;
        if (MainDepartment != null) body["main_department"] = MainDepartment.Value;
        if (Extra != null)
        {
            foreach (var kv in Extra)
                if (kv.Value != null) body[kv.Key] = kv.Value;
        }
        return body;
    }
}

/// <summary>批量接口返回的任务。文档 https://developer.work.weixin.qq.com/document/path/90983</summary>
public class QyWeixinBatchResult
{
    /// <summary>任务状态。1 开始，2 进行中，3 已完成</summary>
    public Int32 Status { get; set; }

    /// <summary>操作类型</summary>
    public String Type { get; set; }

    /// <summary>总条数</summary>
    public Int32 Total { get; set; }

    /// <summary>完成百分比</summary>
    public Int32 Percentage { get; set; }

    /// <summary>结果明细。完成时可能是 JSON 文本，原样保留</summary>
    public String Result { get; set; }

    /// <summary>原始响应</summary>
    public IDictionary<String, Object> Raw { get; set; }

    /// <summary>从 batch/getresult 响应组装</summary>
    /// <param name="raw">响应字典</param>
    /// <returns>任务结果</returns>
    public static QyWeixinBatchResult From(IDictionary<String, Object> raw) => new()
    {
        Raw = raw,
        Status = QyWeixinValues.PickInt(raw, "status"),
        Type = QyWeixinValues.Pick(raw, "type"),
        Total = QyWeixinValues.PickInt(raw, "total"),
        Percentage = QyWeixinValues.PickInt(raw, "percentage"),
        Result = QyWeixinValues.Pick(raw, "result"),
    };
}

public partial class QyWeixinClient
{
    /// <summary>创建部门。返回新建 id。不写魔方 Department</summary>
    /// <param name="department">部门</param>
    /// <returns>部门 id</returns>
    public async Task<Int32> CreateDepartmentAsync(QyWeixinDepartment department)
    {
        if (department == null) throw new ArgumentNullException(nameof(department));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("department/create?access_token=" + token, department.ToCreateBody()).ConfigureAwait(false);
        return QyWeixinValues.PickInt(raw, "id");
    }

    /// <summary>更新部门。文档 https://developer.work.weixin.qq.com/document/path/90206</summary>
    /// <param name="department">至少含 Id</param>
    /// <returns>更新任务</returns>
    public async Task UpdateDepartmentAsync(QyWeixinDepartment department)
    {
        if (department == null) throw new ArgumentNullException(nameof(department));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("department/update?access_token=" + token, department.ToUpdateBody()).ConfigureAwait(false);
    }

    /// <summary>删除部门。不能删除根部门，不能删除含子部门或成员的部门。文档 https://developer.work.weixin.qq.com/document/path/90207</summary>
    /// <param name="id">部门 id</param>
    /// <returns>删除任务</returns>
    public async Task DeleteDepartmentAsync(Int32 id)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        await GetJsonAsync("department/delete", new { access_token = token, id }).ConfigureAwait(false);
    }

    /// <summary>获取单个部门详情</summary>
    /// <param name="id">部门 id</param>
    /// <returns>部门对象，字段在 department 节点或整包中，以 Raw 方式返回整包</returns>
    public async Task<IDictionary<String, Object>> GetDepartmentAsync(Int32 id)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("department/get", new { access_token = token, id }).ConfigureAwait(false);
    }

    /// <summary>创建成员。不写魔方 User</summary>
    /// <param name="member">成员</param>
    /// <returns>创建任务</returns>
    public async Task CreateMemberAsync(QyWeixinMember member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("user/create?access_token=" + token, member.ToCreateBody()).ConfigureAwait(false);
    }

    /// <summary>更新成员。文档 https://developer.work.weixin.qq.com/document/path/90197</summary>
    /// <param name="member">至少含 UserId</param>
    /// <returns>更新任务</returns>
    public async Task UpdateMemberAsync(QyWeixinMember member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("user/update?access_token=" + token, member.ToUpdateBody()).ConfigureAwait(false);
    }

    /// <summary>删除成员。文档 https://developer.work.weixin.qq.com/document/path/90198</summary>
    /// <param name="userId">userid</param>
    /// <returns>删除任务</returns>
    public async Task DeleteMemberAsync(String userId)
    {
        if (userId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(userId));
        var token = await TokenAsync().ConfigureAwait(false);
        await GetJsonAsync("user/delete", new { access_token = token, userid = userId }).ConfigureAwait(false);
    }

    /// <summary>批量删除成员。文档 https://developer.work.weixin.qq.com/document/path/90199</summary>
    /// <param name="userIds">userid 列表，最多 200</param>
    /// <returns>删除任务</returns>
    public async Task DeleteMembersAsync(IEnumerable<String> userIds)
    {
        var ids = userIds?.Where(e => !e.IsNullOrEmpty()).ToArray() ?? Array.Empty<String>();
        if (ids.Length == 0) throw new ArgumentException("useridlist 不能为空", nameof(userIds));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("user/batchdelete?access_token=" + token, new { useridlist = ids }).ConfigureAwait(false);
    }

    /// <summary>手机号换 userid</summary>
    /// <param name="mobile">手机号</param>
    /// <returns>userid</returns>
    public async Task<String> GetUserIdByMobileAsync(String mobile)
    {
        if (mobile.IsNullOrEmpty()) throw new ArgumentNullException(nameof(mobile));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("user/getuserid?access_token=" + token, new { mobile }).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "userid");
    }

    /// <summary>邮箱换 userid</summary>
    /// <param name="email">邮箱</param>
    /// <param name="emailType">1 企业邮箱，2 个人邮箱</param>
    /// <returns>userid</returns>
    public async Task<String> GetUserIdByEmailAsync(String email, Int32 emailType = 1)
    {
        if (email.IsNullOrEmpty()) throw new ArgumentNullException(nameof(email));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("user/get_userid_by_email?access_token=" + token, new { email, email_type = emailType }).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "userid");
    }

    /// <summary>
    /// 邀请成员。文档 https://developer.work.weixin.qq.com/document/path/90975
    /// user、party、tag 至少填一类。
    /// </summary>
    /// <param name="userIds">成员 userid</param>
    /// <param name="partyIds">部门 id</param>
    /// <param name="tagIds">标签 id</param>
    /// <returns>邀请结果，含非法 id 列表时在响应里</returns>
    public async Task<IDictionary<String, Object>> InviteAsync(IEnumerable<String> userIds = null, IEnumerable<Int32> partyIds = null, IEnumerable<Int32> tagIds = null)
    {
        var body = new Dictionary<String, Object>();
        var users = userIds?.Where(e => !e.IsNullOrEmpty()).ToArray();
        var parties = partyIds?.ToArray();
        var tags = tagIds?.ToArray();
        if (users != null && users.Length > 0) body["user"] = users;
        if (parties != null && parties.Length > 0) body["party"] = parties;
        if (tags != null && tags.Length > 0) body["tag"] = tags;
        if (body.Count == 0) throw new ArgumentException("user/party/tag 至少填一项");

        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync("batch/invite?access_token=" + token, body).ConfigureAwait(false);
    }

    /// <summary>创建标签。文档 https://developer.work.weixin.qq.com/document/path/90210</summary>
    /// <param name="tagName">标签名</param>
    /// <param name="tagId">指定 id 时必须未占用；空则由企微分配</param>
    /// <returns>标签 id</returns>
    public async Task<Int32> CreateTagAsync(String tagName, Int32? tagId = null)
    {
        if (tagName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(tagName));
        var body = new Dictionary<String, Object> { ["tagname"] = tagName };
        if (tagId != null) body["tagid"] = tagId.Value;
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("tag/create?access_token=" + token, body).ConfigureAwait(false);
        return QyWeixinValues.PickInt(raw, "tagid");
    }

    /// <summary>更新标签名。文档 https://developer.work.weixin.qq.com/document/path/90211</summary>
    /// <param name="tagId">标签 id</param>
    /// <param name="tagName">新名称</param>
    /// <returns>更新任务</returns>
    public async Task UpdateTagAsync(Int32 tagId, String tagName)
    {
        if (tagName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(tagName));
        var token = await TokenAsync().ConfigureAwait(false);
        await PostJsonAsync("tag/update?access_token=" + token, new { tagid = tagId, tagname = tagName }).ConfigureAwait(false);
    }

    /// <summary>删除标签。文档 https://developer.work.weixin.qq.com/document/path/90212</summary>
    /// <param name="tagId">标签 id</param>
    /// <returns>删除任务</returns>
    public async Task DeleteTagAsync(Int32 tagId)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        await GetJsonAsync("tag/delete", new { access_token = token, tagid = tagId }).ConfigureAwait(false);
    }

    /// <summary>获取标签成员。文档 https://developer.work.weixin.qq.com/document/path/90213</summary>
    /// <param name="tagId">标签 id</param>
    /// <returns>userlist 与 partylist 所在的整包</returns>
    public async Task<IDictionary<String, Object>> GetTagAsync(Int32 tagId)
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("tag/get", new { access_token = token, tagid = tagId }).ConfigureAwait(false);
    }

    /// <summary>增加标签成员。文档 https://developer.work.weixin.qq.com/document/path/90214</summary>
    /// <param name="tagId">标签 id</param>
    /// <param name="userIds">成员，可空</param>
    /// <param name="partyIds">部门，可空</param>
    /// <returns>可能含非法列表的响应</returns>
    public Task<IDictionary<String, Object>> AddTagMembersAsync(Int32 tagId, IEnumerable<String> userIds = null, IEnumerable<Int32> partyIds = null)
        => PostTagMembersAsync("tag/addtagusers", tagId, userIds, partyIds);

    /// <summary>删除标签成员。文档 https://developer.work.weixin.qq.com/document/path/90215</summary>
    /// <param name="tagId">标签 id</param>
    /// <param name="userIds">成员，可空</param>
    /// <param name="partyIds">部门，可空</param>
    /// <returns>响应</returns>
    public Task<IDictionary<String, Object>> DeleteTagMembersAsync(Int32 tagId, IEnumerable<String> userIds = null, IEnumerable<Int32> partyIds = null)
        => PostTagMembersAsync("tag/deltagusers", tagId, userIds, partyIds);

    /// <summary>获取标签列表。文档 https://developer.work.weixin.qq.com/document/path/90216</summary>
    /// <returns>taglist 所在的整包</returns>
    public async Task<IDictionary<String, Object>> ListTagsAsync()
    {
        var token = await TokenAsync().ConfigureAwait(false);
        return await GetJsonAsync("tag/list", new { access_token = token }).ConfigureAwait(false);
    }

    /// <summary>增量更新成员。上传 CSV 后把 media_id 传入。文档 https://developer.work.weixin.qq.com/document/path/90980</summary>
    /// <param name="mediaId">CSV 的 media_id</param>
    /// <param name="toInvite">是否邀请。null 不传</param>
    /// <returns>jobid</returns>
    public Task<String> SyncUsersBatchAsync(String mediaId, Boolean? toInvite = null)
        => SubmitBatchAsync("batch/syncuser", mediaId, toInvite);

    /// <summary>全量覆盖成员。文档 https://developer.work.weixin.qq.com/document/path/90981</summary>
    /// <param name="mediaId">CSV 的 media_id</param>
    /// <param name="toInvite">是否邀请。null 不传</param>
    /// <returns>jobid</returns>
    public Task<String> ReplaceUsersBatchAsync(String mediaId, Boolean? toInvite = null)
        => SubmitBatchAsync("batch/replaceuser", mediaId, toInvite);

    /// <summary>全量覆盖部门。文档 https://developer.work.weixin.qq.com/document/path/90982</summary>
    /// <param name="mediaId">CSV 的 media_id</param>
    /// <returns>jobid</returns>
    public Task<String> ReplaceDepartmentsBatchAsync(String mediaId)
        => SubmitBatchAsync("batch/replaceparty", mediaId, null);

    /// <summary>查询异步任务。文档 https://developer.work.weixin.qq.com/document/path/90983</summary>
    /// <param name="jobId">提交接口返回的 jobid</param>
    /// <returns>任务进度</returns>
    public async Task<QyWeixinBatchResult> GetBatchResultAsync(String jobId)
    {
        if (jobId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(jobId));
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await GetJsonAsync("batch/getresult", new { access_token = token, jobid = jobId }).ConfigureAwait(false);
        return QyWeixinBatchResult.From(raw);
    }

    private async Task<String> SubmitBatchAsync(String action, String mediaId, Boolean? toInvite)
    {
        if (mediaId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(mediaId));
        var body = new Dictionary<String, Object> { ["media_id"] = mediaId };
        if (toInvite != null) body["to_invite"] = toInvite.Value;
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync(action + "?access_token=" + token, body).ConfigureAwait(false);
        return QyWeixinValues.Pick(raw, "jobid");
    }

    private async Task<IDictionary<String, Object>> PostTagMembersAsync(String action, Int32 tagId, IEnumerable<String> userIds, IEnumerable<Int32> partyIds)
    {
        var body = new Dictionary<String, Object> { ["tagid"] = tagId };
        var users = userIds?.Where(e => !e.IsNullOrEmpty()).ToArray();
        var parties = partyIds?.ToArray();
        if (users != null && users.Length > 0) body["userlist"] = users;
        if (parties != null && parties.Length > 0) body["partylist"] = parties;
        if (body.Count == 1) throw new ArgumentException("userlist/partylist 至少填一项");
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync(action + "?access_token=" + token, body).ConfigureAwait(false);
    }
}
