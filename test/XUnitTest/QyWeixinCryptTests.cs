using System.ComponentModel;
using Xunit;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NewLife.Cube.QyWeixin;

namespace XUnitTest;

public class QyWeixinCryptTests
{
    private static QyWeixinCrypt Create(String corpId = "ww123")
    {
        var key = new Byte[32];
        RandomNumberGenerator.Fill(key);
        var aesKey = Convert.ToBase64String(key).TrimEnd('=');
        return new QyWeixinCrypt("token", aesKey, corpId);
    }

    [Fact]
    [DisplayName("加解密回环_明文与receiveid还原")]
    public void Encrypt_Roundtrip_RestoresMessageAndCorpId()
    {
        var crypt = Create("wwcorp");
        var (message, receiveId) = crypt.Decrypt(crypt.Encrypt("<xml>hi</xml>"));
        Assert.Equal("<xml>hi</xml>", message);
        Assert.Equal("wwcorp", receiveId);
    }

    [Fact]
    [DisplayName("echostr_按官方封装拆包_只回显明文")]
    public void DecryptEcho_Packed_ReturnsMessageOnly()
    {
        var crypt = Create("wwcorp");
        var echo = crypt.DecryptEcho(crypt.Encrypt("ping"));
        Assert.Equal("ping", echo);
    }

    [Fact]
    [DisplayName("签名_字典序_校验通过且大小写不敏感")]
    public void VerifySignature_SortedSha1_IgnoresCase()
    {
        var crypt = Create();
        var sign = crypt.Sign("1409659813", "nonce", "cipher");
        Assert.Equal(40, sign.Length);
        Assert.True(crypt.VerifySignature("1409659813", "nonce", "cipher", sign.ToUpperInvariant()));
        Assert.False(crypt.VerifySignature("1409659813", "nonce", "cipher", "00"));
    }

    [Fact]
    [DisplayName("receiveid不匹配_抛异常")]
    public void Decrypt_WrongCorp_Throws()
    {
        var writer = Create("wwA");
        var reader = new QyWeixinCrypt("token", EncodingAesKeyOf(writer), "wwB");
        Assert.Throws<InvalidDataException>(() => reader.Decrypt(writer.Encrypt("x")));
    }

    [Fact]
    [DisplayName("EncodingAESKey不是32字节_抛异常")]
    public void Ctor_BadKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new QyWeixinCrypt("t", "abc", "ww"));
    }

    [Fact]
    [DisplayName("被动回复_加密包可验签并解出明文")]
    public void BuildEncryptedReply_Roundtrip()
    {
        var crypt = Create("wwcorp");
        var plain = QyWeixinReply.ToXml(QyWeixinReply.Text("user", "wwcorp", "收到"));
        var packed = crypt.BuildEncryptedReply(plain, "1700000000", "n1");
        var doc = XElement.Parse(packed);
        var encrypt = doc.Element("Encrypt").Value;
        Assert.True(crypt.VerifySignature("1700000000", "n1", encrypt, doc.Element("MsgSignature").Value));
        Assert.Equal(plain, crypt.Decrypt(encrypt).Message);
        Assert.Equal("1700000000", doc.Element("TimeStamp").Value);
    }

    private static String EncodingAesKeyOf(QyWeixinCrypt crypt)
    {
        var field = typeof(QyWeixinCrypt).GetField("_aesKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var key = (Byte[])field.GetValue(crypt);
        return Convert.ToBase64String(key).TrimEnd('=');
    }
}
