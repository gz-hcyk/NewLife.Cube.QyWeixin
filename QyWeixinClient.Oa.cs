using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 日程 / 打卡 / 审批的薄封装。复杂字段（重复规则、审批控件）用匿名对象或字典原样提交，避免半套模型。
/// 魔方基类已有 GetCheckIn（固定类型 3）和 GetApprovals（固定审批通过）。这里补可指定参数的读接口，不替换基类方法。
/// </summary>
public partial class QyWeixinClient
{
    /// <summary>
    /// 创建日程。文档 https://developer.work.weixin.qq.com/document/path/93648
    /// schedule 为文档中的 schedule 对象。
    /// </summary>
    /// <param name="schedule">日程对象</param>
    /// <returns>含 schedule_id 的响应</returns>
    public Task<IDictionary<String, Object>> AddScheduleAsync(Object schedule)
        => PostScheduleAsync("oa/schedule/add", new { schedule });

    /// <summary>
    /// 更新日程。文档 https://developer.work.weixin.qq.com/document/path/97720
    /// body 含 schedule，以及可选的 skip_attendees、op_mode、op_start_time。
    /// </summary>
    /// <param name="body">更新请求体</param>
    /// <returns>响应</returns>
    public Task<IDictionary<String, Object>> UpdateScheduleAsync(Object body)
        => PostScheduleAsync("oa/schedule/update", body);

    /// <summary>获取日程详情。文档 https://developer.work.weixin.qq.com/document/path/97724</summary>
    /// <param name="scheduleIds">日程 id，最多 1000</param>
    /// <returns>schedule_list 所在的整包</returns>
    public Task<IDictionary<String, Object>> GetSchedulesAsync(IEnumerable<String> scheduleIds)
    {
        var ids = scheduleIds?.Where(e => !e.IsNullOrEmpty()).ToArray() ?? Array.Empty<String>();
        if (ids.Length == 0) throw new ArgumentException("schedule_id_list 不能为空", nameof(scheduleIds));
        return PostScheduleAsync("oa/schedule/get", new { schedule_id_list = ids });
    }

    /// <summary>取消日程。接口 oa/schedule/del</summary>
    /// <param name="scheduleId">日程 id</param>
    /// <returns>响应</returns>
    public Task<IDictionary<String, Object>> CancelScheduleAsync(String scheduleId)
    {
        if (scheduleId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(scheduleId));
        return PostScheduleAsync("oa/schedule/del", new { schedule_id = scheduleId });
    }

    /// <summary>
    /// 最小日程对象：标题、起止时间、参与人。重复规则等请自行组字典。
    /// </summary>
    /// <param name="summary">标题</param>
    /// <param name="start">开始</param>
    /// <param name="end">结束</param>
    /// <param name="attendeeUserIds">参与人 userid</param>
    /// <param name="description">描述，可空</param>
    /// <returns>schedule 对象</returns>
    public static Dictionary<String, Object> Schedule(String summary, DateTime start, DateTime end, IEnumerable<String> attendeeUserIds, String description = null)
    {
        var attendees = (attendeeUserIds ?? Array.Empty<String>())
            .Where(e => !e.IsNullOrEmpty())
            .Select(e => (Object)new Dictionary<String, Object> { ["userid"] = e })
            .ToList();
        var schedule = new Dictionary<String, Object>
        {
            ["summary"] = summary ?? "",
            ["start_time"] = QyWeixinValues.ToUnix(start),
            ["end_time"] = QyWeixinValues.ToUnix(end),
            ["attendees"] = attendees,
        };
        QyWeixinValues.Set(schedule, "description", description);
        return schedule;
    }

    /// <summary>
    /// 获取打卡记录。文档 https://developer.work.weixin.qq.com/document/path/96497
    /// 基类 GetCheckIn 固定 opencheckindatatype=3。本方法由调用方指定类型。
    /// 时间跨度不超过 30 天，userid 不超过 100。
    /// </summary>
    /// <param name="openCheckInDataType">1 上下班，2 外出，3 全部</param>
    /// <param name="start">开始</param>
    /// <param name="end">结束</param>
    /// <param name="userIds">成员</param>
    /// <returns>checkindata 所在的整包</returns>
    public Task<IDictionary<String, Object>> GetCheckInDataAsync(Int32 openCheckInDataType, DateTime start, DateTime end, IEnumerable<String> userIds)
        => PostCheckInAsync("checkin/getcheckindata", openCheckInDataType, start, end, userIds);

    /// <summary>获取打卡日报。文档 https://developer.work.weixin.qq.com/document/path/93374</summary>
    /// <param name="start">开始</param>
    /// <param name="end">结束</param>
    /// <param name="userIds">成员</param>
    /// <returns>整包响应</returns>
    public Task<IDictionary<String, Object>> GetCheckInDayDataAsync(DateTime start, DateTime end, IEnumerable<String> userIds)
        => PostCheckInAsync("checkin/getcheckin_daydata", null, start, end, userIds);

    /// <summary>获取打卡月报。文档 https://developer.work.weixin.qq.com/document/path/93387</summary>
    /// <param name="start">开始</param>
    /// <param name="end">结束</param>
    /// <param name="userIds">成员</param>
    /// <returns>整包响应</returns>
    public Task<IDictionary<String, Object>> GetCheckInMonthDataAsync(DateTime start, DateTime end, IEnumerable<String> userIds)
        => PostCheckInAsync("checkin/getcheckin_monthdata", null, start, end, userIds);

    /// <summary>
    /// 批量获取审批单号。不强制 template_id，也不强制 sp_status=2。
    /// 基类 GetApprovals 仍只查「已通过」且必须有模板 id。
    /// filters 元素为 { key, value }，例如 template_id、sp_status。
    /// </summary>
    /// <param name="start">开始</param>
    /// <param name="end">结束。跨度不要超过 31 天</param>
    /// <param name="cursor">分页游标</param>
    /// <param name="size">每页条数，最大 100</param>
    /// <param name="filters">过滤条件，可空</param>
    /// <returns>sp_no_list 所在的整包</returns>
    public async Task<IDictionary<String, Object>> QueryApprovalInfoAsync(DateTime start, DateTime end, Int32 cursor = 0, Int32 size = 100, IEnumerable<Object> filters = null)
    {
        var body = new Dictionary<String, Object>
        {
            ["starttime"] = QyWeixinValues.ToUnix(start),
            ["endtime"] = QyWeixinValues.ToUnix(end),
            ["cursor"] = cursor,
            ["size"] = size,
        };
        if (filters != null) body["filters"] = filters.ToArray();
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync("oa/getapprovalinfo?access_token=" + token, body).ConfigureAwait(false);
    }

    /// <summary>获取审批模板详情。文档 https://developer.work.weixin.qq.com/document/path/91982</summary>
    /// <param name="templateId">模板 id</param>
    /// <returns>模板控件定义</returns>
    public async Task<IDictionary<String, Object>> GetApprovalTemplateAsync(String templateId)
    {
        if (templateId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(templateId));
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync("oa/gettemplatedetail?access_token=" + token, new { template_id = templateId }).ConfigureAwait(false);
    }

    private async Task<IDictionary<String, Object>> PostScheduleAsync(String action, Object body)
    {
        if (body == null) throw new ArgumentNullException(nameof(body));
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync(action + "?access_token=" + token, body).ConfigureAwait(false);
    }

    private async Task<IDictionary<String, Object>> PostCheckInAsync(String action, Int32? openType, DateTime start, DateTime end, IEnumerable<String> userIds)
    {
        var ids = userIds?.Where(e => !e.IsNullOrEmpty()).ToArray() ?? Array.Empty<String>();
        if (ids.Length == 0) throw new ArgumentException("useridlist 不能为空", nameof(userIds));
        var body = new Dictionary<String, Object>
        {
            ["starttime"] = QyWeixinValues.ToUnix(start),
            ["endtime"] = QyWeixinValues.ToUnix(end),
            ["useridlist"] = ids,
        };
        if (openType != null) body["opencheckindatatype"] = openType.Value;
        var token = await TokenAsync().ConfigureAwait(false);
        return await PostJsonAsync(action + "?access_token=" + token, body).ConfigureAwait(false);
    }
}
