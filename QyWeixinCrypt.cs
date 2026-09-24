using System.Security.Cryptography;
using System.Text;
using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信「接收消息」回调加解密（WXBizMsgCrypt 算法）。
/// AES-256-CBC（Key=Base64(EncodingAESKey+"=")，IV=Key 前 16 字节，PKCS7），
/// 消息体格式：random(16) + msg_len(4, 大端) + msg + receiveid(corpid)；
/// 签名 = SHA1(字典序排序 token/timestamp/nonce/密文 后拼接)。
/// 零第三方依赖，按官方算法直接实现。
/// </summary>
public class QyWeixinCrypt
{
    private readonly String _token;
    private readonly Byte[] _aesKey;
    private readonly String _corpId;

    /// <summary>实例化</summary>
    /// <param name="token">企微后台「接收消息」配置的 Token</param>
    /// <param name="encodingAESKey">企微后台生成的 EncodingAESKey（43 字符，内部补 = 后 Base64 解码为 32 字节）</param>
    /// <param name="corpId">企业 ID（解密后校验消息尾部 receiveid，空=不校验）</param>
    public QyWeixinCrypt(String token, String encodingAESKey, String corpId = null)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));
        if (encodingAESKey.IsNullOrEmpty()) throw new ArgumentNullException(nameof(encodingAESKey));

        _token = token;
        _corpId = corpId;
        var key = Convert.FromBase64String(encodingAESKey.TrimEnd('=') + "=");
        if (key.Length != 32) throw new ArgumentException($"EncodingAESKey 解码后应为 32 字节，实际 {key.Length}", nameof(encodingAESKey));
        _aesKey = key;
    }

    /// <summary>校验消息签名。msg_signature = SHA1(字典序排序(token, timestamp, nonce, encryptMsg) 拼接)</summary>
    /// <param name="timestamp">时间戳</param>
    /// <param name="nonce">随机串</param>
    /// <param name="encryptMsg">密文（GET 的 echostr 或 POST 报文的 Encrypt）</param>
    /// <param name="signature">待校验的 msg_signature</param>
    /// <returns>true=签名一致</returns>
    public Boolean VerifySignature(String timestamp, String nonce, String encryptMsg, String signature)
    {
        var plain = String.Join("", new[] { _token, timestamp, nonce, encryptMsg }.OrderBy(s => s, StringComparer.Ordinal));
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(plain));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return expected.EqualIgnoreCase(signature);
    }

    /// <summary>
    /// 解密消息体并拆包，返回明文与消息尾部的 receiveid。
    /// </summary>
    /// <param name="encrypted">Base64 密文</param>
    /// <returns>明文；receiveid 与 corpId 不匹配时抛异常</returns>
    public (String Message, String ReceiveId) Decrypt(String encrypted)
    {
        var bytes = DecryptRaw(encrypted);

        // 拆包：random(16) + msg_len(4 大端) + msg + receiveid
        if (bytes.Length < 20) throw new InvalidDataException("解密结果过短，不是合法的消息体");
        var len = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        if (len < 0 || 20 + len > bytes.Length) throw new InvalidDataException("解密结果的消息长度字段非法");
        var msg = Encoding.UTF8.GetString(bytes, 20, len);
        var receiveId = Encoding.UTF8.GetString(bytes, 20 + len, bytes.Length - 20 - len);

        if (!_corpId.IsNullOrEmpty() && receiveId != _corpId)
            throw new InvalidDataException($"消息 receiveid({receiveId}) 与企业 ID({_corpId}) 不匹配");

        return (msg, receiveId);
    }

    /// <summary>解密 URL 验证的 echostr（无消息体封装，直接 AES 解密去填充）。</summary>
    /// <param name="encrypted">Base64 密文</param>
    /// <returns>明文 echostr，原样回显即完成 URL 验证</returns>
    public String DecryptEcho(String encrypted) => Encoding.UTF8.GetString(DecryptRaw(encrypted));

    /// <summary>AES 解密 + 去 PKCS7 填充（供消息体拆包与 echostr 共用）。</summary>
    private Byte[] DecryptRaw(String encrypted)
    {
        if (encrypted.IsNullOrEmpty()) throw new ArgumentNullException(nameof(encrypted));
        var data = Convert.FromBase64String(encrypted);

        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = _aesKey.Take(16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(data, 0, data.Length);
        return plain;
    }

    /// <summary>
    /// 按消息体格式打包并加密（random16 + len + msg + receiveid，PKCS7 填充）。
    /// 仅用于测试回环与联调自检，生产消息由企微下发。
    /// </summary>
    public String Encrypt(String message)
    {
        if (_corpId.IsNullOrEmpty()) throw new InvalidOperationException("加密需要 corpId（作为 receiveid）");
        var msg = Encoding.UTF8.GetBytes(message);
        var corp = Encoding.UTF8.GetBytes(_corpId);
        var random = new Byte[16];
        RandomNumberGenerator.Fill(random);

        using var ms = new MemoryStream();
        ms.Write(random, 0, 16);
        ms.WriteByte((Byte)(msg.Length >> 24));
        ms.WriteByte((Byte)(msg.Length >> 16));
        ms.WriteByte((Byte)(msg.Length >> 8));
        ms.WriteByte((Byte)msg.Length);
        ms.Write(msg, 0, msg.Length);
        ms.Write(corp, 0, corp.Length);

        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = _aesKey.Take(16).ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        return Convert.ToBase64String(encryptor.TransformFinalBlock(ms.GetBuffer(), 0, (Int32)ms.Length));
    }
}
