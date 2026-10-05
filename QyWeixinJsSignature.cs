using System.Security.Cryptography;
using NewLife;
using System.Text;

namespace NewLife.Cube.QyWeixin;

/// <summary>wx.config / agentConfig 用的签名结果。只含下发给页面的字段，不含 jsapi_ticket</summary>
public class QyWeixinJsConfig
{
    /// <summary>秒级时间戳</summary>
    public Int64 Timestamp { get; set; }

    /// <summary>随机串，对应 noncestr</summary>
    public String NonceStr { get; set; }

    /// <summary>SHA1 签名，十六进制小写</summary>
    public String Signature { get; set; }

    /// <summary>参与签名的页面 URL（已去掉 # 及后续片段）</summary>
    public String Url { get; set; }
}

/// <summary>
/// JS-SDK 签名。算法见 https://developer.work.weixin.qq.com/document/path/90506
/// 拼接顺序固定为 jsapi_ticket、noncestr、timestamp、url，不做 URL 编码。
/// </summary>
public static class QyWeixinJsSignature
{
    /// <summary>按官方顺序计算签名</summary>
    /// <param name="ticket">企业或应用 jsapi_ticket</param>
    /// <param name="nonceStr">随机串</param>
    /// <param name="timestamp">秒级时间戳</param>
    /// <param name="url">页面 URL，调用方需已去掉 # 片段</param>
    /// <returns>SHA1 十六进制小写</returns>
    public static String Sign(String ticket, String nonceStr, Int64 timestamp, String url)
    {
        if (ticket.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ticket));
        if (nonceStr.IsNullOrEmpty()) throw new ArgumentNullException(nameof(nonceStr));
        if (url.IsNullOrEmpty()) throw new ArgumentNullException(nameof(url));

        var plain = $"jsapi_ticket={ticket}&noncestr={nonceStr}&timestamp={timestamp}&url={url}";
        return Sha1Hex(plain);
    }

    /// <summary>去掉 URL 中 # 及后续片段。签名必须使用浏览器地址栏里 # 之前的部分</summary>
    /// <param name="url">页面地址，可含 hash</param>
    /// <returns>去掉片段后的 URL</returns>
    public static String NormalizeUrl(String url)
    {
        if (url.IsNullOrEmpty()) return url;
        var hash = url.IndexOf('#');
        return hash >= 0 ? url.Substring(0, hash) : url;
    }

    /// <summary>生成可下发页面的签名包。ticket 只参与计算，不会出现在返回值里</summary>
    /// <param name="ticket">jsapi_ticket</param>
    /// <param name="url">当前页面 URL</param>
    /// <param name="nonceStr">随机串，空则自动生成</param>
    /// <param name="timestamp">秒级时间戳，空则取当前 UTC</param>
    /// <returns>timestamp / nonceStr / signature / url</returns>
    public static QyWeixinJsConfig Build(String ticket, String url, String nonceStr = null, Int64? timestamp = null)
    {
        var normalized = NormalizeUrl(url);
        var nonce = nonceStr.IsNullOrEmpty() ? CreateNonce() : nonceStr;
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new QyWeixinJsConfig
        {
            Timestamp = ts,
            NonceStr = nonce,
            Url = normalized,
            Signature = Sign(ticket, nonce, ts, normalized),
        };
    }

    /// <summary>生成非加密随机串，供 noncestr / 回包 Nonce 使用</summary>
    /// <param name="length">长度，默认 16</param>
    /// <returns>字母数字随机串</returns>
    public static String CreateNonce(Int32 length = 16)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        const String chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new Byte[length];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(length);
        foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    /// <summary>SHA1 十六进制小写</summary>
    /// <param name="plain">待摘要文本，UTF-8</param>
    /// <returns>40 位十六进制</returns>
    public static String Sha1Hex(String plain)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(plain ?? ""));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
