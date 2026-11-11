using System.Runtime.Serialization;
using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>打卡记录。文档 https://developer.work.weixin.qq.com/document/path/90262</summary>
public class QyWeixinCheckInData
{
    /// <summary>用户标识</summary>
    public String UserId { get; set; }

    /// <summary>打卡组</summary>
    public String GroupName { get; set; }

    /// <summary>打卡类型。上班打卡、下班打卡、外出打卡</summary>
    [DataMember(Name = "checkin_type")]
    public String CheckInType { get; set; }

    /// <summary>异常类型</summary>
    [DataMember(Name = "exception_type")]
    public String ExceptionType { get; set; }

    /// <summary>打卡时间。Unix 秒</summary>
    [DataMember(Name = "checkin_time")]
    public Int32 CheckInTime { get; set; }

    /// <summary>位置标题</summary>
    [DataMember(Name = "location_title")]
    public String LocationTitle { get; set; }

    /// <summary>位置明细</summary>
    [DataMember(Name = "location_detail")]
    public String LocationDetail { get; set; }

    /// <summary>纬度</summary>
    [DataMember(Name = "lat")]
    public Int32 Latitude { get; set; }

    /// <summary>经度</summary>
    [DataMember(Name = "lng")]
    public Int32 Longitude { get; set; }

    /// <summary>设备</summary>
    public String DeviceId { get; set; }

    /// <summary>Wifi 名称</summary>
    public String WifiName { get; set; }

    /// <summary>Wifi 的 MAC 地址</summary>
    public String WifiMac { get; set; }

    /// <summary>备注</summary>
    public String Notes { get; set; }

    /// <summary>相片媒体</summary>
    public String[] MediaIds { get; set; }

    /// <summary>标准打卡时间，指此次打卡时间对应的标准上班时间或标准下班时间</summary>
    [DataMember(Name = "sch_checkin_time")]
    public Int32 SchCheckinTime { get; set; }

    /// <summary>考勤组</summary>
    public Int32 GroupId { get; set; }

    /// <summary>计划标识</summary>
    [DataMember(Name = "schedule_id")]
    public Int32 ScheduleId { get; set; }

    /// <summary>考勤段</summary>
    [DataMember(Name = "timeline_id")]
    public Int32 TimelineId { get; set; }

    /// <summary>已重载。显示打卡人与时间</summary>
    /// <returns></returns>
    public override String ToString() => $"[{UserId}]{CheckInType} {CheckInTime.ToDateTime().ToLocalTime().ToFullString()}";
}

public partial class QyWeixinClient
{
    /// <summary>获取打卡记录。时间跨度不超过 30 天，userid 不超过 100</summary>
    /// <param name="openCheckinDataType">1 上下班打卡，2 外出打卡，3 全部打卡</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="userIds">成员 userid 列表，最多 100 个</param>
    /// <returns>打卡记录</returns>
    public async Task<QyWeixinCheckInData[]> GetCheckInListAsync(Int32 openCheckinDataType, DateTime startTime, DateTime endTime, String[] userIds)
    {
        if (openCheckinDataType < 1 || openCheckinDataType > 3) throw new ArgumentException("打卡类型错误", nameof(openCheckinDataType));
        if (userIds == null || userIds.Length == 0) throw new ArgumentException("用户列表不能为空", nameof(userIds));
        if (userIds.Length > 100) throw new ArgumentException("用户不能超过100个", nameof(userIds));
        if (endTime <= startTime) throw new ArgumentException("结束时间必须大于开始时间", nameof(endTime));
        if (startTime.AddDays(30) < endTime) throw new ArgumentException("时间跨度不能超过30天", nameof(endTime));

        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostJsonAsync("checkin/getcheckindata?access_token=" + token, new
        {
            opencheckindatatype = openCheckinDataType,
            starttime = startTime.ToInt(),
            endtime = endTime.ToInt(),
            useridlist = userIds,
        }).ConfigureAwait(false);

        if (raw == null || !raw.TryGetValue("checkindata", out var value) || value is not IEnumerable<Object> items)
            return Array.Empty<QyWeixinCheckInData>();

        return items.OfType<IDictionary<String, Object>>().Select(ParseCheckIn).ToArray();
    }

    private static QyWeixinCheckInData ParseCheckIn(IDictionary<String, Object> raw) => new()
    {
        UserId = QyWeixinValues.Pick(raw, "userid"),
        GroupName = QyWeixinValues.Pick(raw, "groupname"),
        CheckInType = QyWeixinValues.Pick(raw, "checkin_type"),
        ExceptionType = QyWeixinValues.Pick(raw, "exception_type"),
        CheckInTime = QyWeixinValues.PickInt(raw, "checkin_time"),
        LocationTitle = QyWeixinValues.Pick(raw, "location_title"),
        LocationDetail = QyWeixinValues.Pick(raw, "location_detail"),
        Latitude = QyWeixinValues.PickInt(raw, "lat"),
        Longitude = QyWeixinValues.PickInt(raw, "lng"),
        DeviceId = QyWeixinValues.Pick(raw, "deviceid"),
        WifiName = QyWeixinValues.Pick(raw, "wifiname"),
        WifiMac = QyWeixinValues.Pick(raw, "wifimac"),
        Notes = QyWeixinValues.Pick(raw, "notes"),
        SchCheckinTime = QyWeixinValues.PickInt(raw, "sch_checkin_time"),
        GroupId = QyWeixinValues.PickInt(raw, "groupid"),
        ScheduleId = QyWeixinValues.PickInt(raw, "schedule_id"),
        TimelineId = QyWeixinValues.PickInt(raw, "timeline_id"),
        MediaIds = raw.TryGetValue("mediaids", out var media) && media is IEnumerable<Object> list
            ? list.Select(e => e + "").ToArray()
            : null,
    };
}
