using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;

namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 企业微信「接收消息」通用回调端点。
/// GET = URL 验证（解密 echostr 回显）；POST = 回调事件（验签 → 解密 → 分发到 <see cref="QyWeixinEvents.Handlers"/>）。
/// 匿名端点（企微服务器调用，无登录态），安全性由 msg_signature 验签 + AES 解密 + receiveid 校验保证。
/// 开关与 Token/EncodingAESKey 见 <see cref="QyWeixinCallbackSetting"/>，须与企微后台「接收消息」API 配置一致。
/// </summary>
[Route("/QyWeixin/Callback")]
public class QyWeixinCallbackController : ControllerBase
{
    /// <summary>URL 验证：解密 echostr 并回显明文</summary>
    [HttpGet]
    public IActionResult Verify(String msg_signature, String timestamp, String nonce, String echostr)
    {
        var crypt = CreateCrypt();
        if (crypt == null || echostr.IsNullOrEmpty()) return NotFound();

        if (!crypt.VerifySignature(timestamp, nonce, echostr, msg_signature))
        {
            XTrace.WriteLine("企微回调 URL 验证失败：msg_signature 校验不通过");
            return BadRequest("invalid signature");
        }

        try
        {
            return Content(crypt.DecryptEcho(echostr), "text/plain");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return BadRequest("decrypt failed");
        }
    }

    /// <summary>回调事件：验签解密后分发给已注册的事件处理器</summary>
    [HttpPost]
    public async Task<IActionResult> Event(String msg_signature, String timestamp, String nonce)
    {
        var crypt = CreateCrypt();
        if (crypt == null) return NotFound();

        String encrypted;
        try
        {
            var doc = XElement.Parse(await new StreamReader(Request.Body).ReadToEndAsync());
            encrypted = doc.Element("Encrypt")?.Value;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return BadRequest("invalid xml");
        }
        if (encrypted.IsNullOrEmpty()) return BadRequest("missing Encrypt");

        if (!crypt.VerifySignature(timestamp, nonce, encrypted, msg_signature))
        {
            XTrace.WriteLine("企微回调验签失败：msg_signature 校验不通过");
            return BadRequest("invalid signature");
        }

        String message;
        try
        {
            message = crypt.Decrypt(encrypted).Message;
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            return BadRequest("decrypt failed");
        }

        try
        {
            var doc = XElement.Parse(message);
            foreach (var handler in QyWeixinEvents.Handlers)
            {
                if (await handler.HandleAsync(doc)) break;
            }
        }
        catch (Exception ex)
        {
            // 处理失败不回 4xx（企微会按重试策略重推事件），仅记日志；处理器应自行保证幂等
            XTrace.WriteException(ex);
        }

        // 事件类回调要求返回空串（200 + 空包体）
        return Content(String.Empty);
    }

    /// <summary>组装加解密器。开关未启用或配置不全时返回 null（端点表现为 404）</summary>
    private static QyWeixinCrypt CreateCrypt()
    {
        var set = QyWeixinCallbackSetting.Current;
        if (!set.Enable || set.Token.IsNullOrEmpty() || set.EncodingAESKey.IsNullOrEmpty()) return null;

        // receiveid 校验用企业 ID：取魔方 OAuth 配置（企业微信）的 AppId 复合写法 corp#agent 前半段
        var cfg = QyWeixinCredential.FindOAuthConfig();
        var corpId = cfg?.AppId?.Split('#')[0];

        return new QyWeixinCrypt(set.Token, set.EncodingAESKey, corpId);
    }
}
