using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>从企微 JSON 字典里取值。解析器可能把数字收成字符串或长整型，这里统一转成文本再解析。</summary>
public static class QyWeixinValues
{
    /// <summary>按键名（忽略大小写）读取文本。都不存在时返回 null</summary>
    /// <param name="raw">响应字典，可空</param>
    /// <param name="keys">候选键，按顺序命中</param>
    /// <returns>文本值；缺失时为 null</returns>
    public static String Pick(IDictionary<String, Object> raw, params String[] keys)
    {
        if (raw == null || keys == null) return null;
        foreach (var key in keys)
        {
            if (key.IsNullOrEmpty()) continue;
            if (raw.TryGetValue(key, out var direct) && direct != null) return direct.ToString();
            foreach (var kv in raw)
            {
                if (kv.Key.EqualIgnoreCase(key) && kv.Value != null) return kv.Value.ToString();
            }
        }
        return null;
    }

    /// <summary>读取 32 位整数。缺失或非数字时返回 0</summary>
    /// <param name="raw">响应字典</param>
    /// <param name="key">键名</param>
    /// <returns>整数值</returns>
    public static Int32 PickInt(IDictionary<String, Object> raw, String key)
    {
        var text = Pick(raw, key);
        return Int32.TryParse(text, out var n) ? n : 0;
    }

    /// <summary>读取 64 位整数。缺失或非数字时返回 0</summary>
    /// <param name="raw">响应字典</param>
    /// <param name="key">键名</param>
    /// <returns>长整型值</returns>
    public static Int64 PickLong(IDictionary<String, Object> raw, String key)
    {
        var text = Pick(raw, key);
        return Int64.TryParse(text, out var n) ? n : 0;
    }

    /// <summary>应用 ID 能解析成整数时按数字写入 JSON，否则保留原文本。message/send 仍用字符串，与历史发送器一致</summary>
    /// <param name="agentId">应用 ID</param>
    /// <returns>int 或原始字符串</returns>
    public static Object Agent(String agentId)
        => Int32.TryParse(agentId, out var n) ? n : agentId;

    /// <summary>把本地时间转成企微使用的 Unix 秒。Unspecified 按本地时区解释</summary>
    /// <param name="time">本地时间</param>
    /// <returns>Unix 秒</returns>
    public static Int64 ToUnix(DateTime time)
    {
        if (time == DateTime.MinValue) return 0;
        if (time.Kind == DateTimeKind.Unspecified)
            time = DateTime.SpecifyKind(time, DateTimeKind.Local);
        return new DateTimeOffset(time).ToUnixTimeSeconds();
    }

    /// <summary>写入可选字段。null 或空白字符串不写入，避免把「未设置」变成空串覆盖企微侧</summary>
    /// <param name="body">目标字典</param>
    /// <param name="key">字段名</param>
    /// <param name="value">字段值</param>
    public static void Set(IDictionary<String, Object> body, String key, Object value)
    {
        if (body == null || key.IsNullOrEmpty() || value == null) return;
        if (value is String text && text.Length == 0) return;
        body[key] = value;
    }
}
