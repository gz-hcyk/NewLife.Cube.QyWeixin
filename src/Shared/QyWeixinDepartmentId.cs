namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信部门 Id 转换。企微接口要求 32 位无符号正整数，本地 <see cref="XCode.Membership.Department.Code"/> 是业务字符串，
/// 两者不能互相直接转换。所有进入企微接口的部门 Id 都应先经过本类校验，避免溢出、截断或静默变成 0。
/// </summary>
public static class QyWeixinDepartmentId
{
    /// <summary>企微部门 Id 上限。32 位无符号整数的最大值</summary>
    public const Int64 MaxValue = UInt32.MaxValue;

    /// <summary>尝试把外部部门 Id 解析为企微要求的 32 位无符号正整数。非法值不会返回 0 冒充成功</summary>
    /// <param name="externalId">外部部门 Id，通常来自企微响应或映射表</param>
    /// <param name="id">解析结果</param>
    /// <returns>是合法企微部门 Id 时返回 true</returns>
    public static Boolean TryParse(String externalId, out Int64 id)
    {
        id = 0;
        if (externalId.IsNullOrWhiteSpace()) return false;

        // 只接受纯数字，拒绝小数、正负号、千分位和空白字符，避免 Int64.TryParse 放宽后的意外命中
        var text = externalId.Trim();
        foreach (var ch in text)
        {
            if (ch < '0' || ch > '9') return false;
        }

        // 超范围时要把解析结果清零，不能把越界值留给调用方
        if (!Int64.TryParse(text, out id) || id <= 0 || id > MaxValue)
        {
            id = 0;
            return false;
        }

        return true;
    }

    /// <summary>把外部部门 Id 解析为企微要求的 32 位无符号正整数。非法时抛出，禁止截断或退化为根部门</summary>
    /// <param name="externalId">外部部门 Id</param>
    /// <returns>合法的企微部门 Id</returns>
    /// <exception cref="ArgumentOutOfRangeException">不是 32 位无符号正整数</exception>
    public static Int64 Parse(String externalId)
    {
        if (!TryParse(externalId, out var id))
            throw new ArgumentOutOfRangeException(nameof(externalId), externalId, "企业微信部门 Id 必须是 32 位无符号正整数");

        return id;
    }

    /// <summary>判断本地部门编号是否可以作为企微部门 Id 使用</summary>
    /// <param name="id">部门编号</param>
    /// <returns>合法范围内时返回 true</returns>
    public static Boolean IsValid(Int64 id) => id > 0 && id <= MaxValue;
}
