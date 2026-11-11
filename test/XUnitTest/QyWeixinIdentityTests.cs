using System.ComponentModel;
using Xunit;
using NewLife.Cube.QyWeixin;
using NewLife.Web.OAuth;

namespace XUnitTest;

public class QyWeixinIdentityTests
{
    [Fact]
    [DisplayName("JS-SDK签名_官方示例向量")]
    public void Sign_OfficialVector()
    {
        var sign = QyWeixinJsSignature.Sign(
            "sM4AOVdWfPE4DxkXGEs8VMCPGGVi4C3VM0P37wVUCFvkVAy_90u5h9nbSlYy3-Sl-HhTdfl2fzFy1AOcHKP7qg",
            "Wm3WZYTPz0wzccnW",
            1414587457,
            "http://mp.weixin.qq.com?params=value");
        Assert.Equal("0f9de62fce790f9a083d5c99e95740ceb90c27ed", sign);
    }

    [Fact]
    [DisplayName("签名URL_去掉hash且不把ticket下发")]
    public void Build_StripsHash()
    {
        var config = QyWeixinJsSignature.Build("ticket", "https://a.example/p?x=1#/home", "nonce", 10);
        Assert.Equal("https://a.example/p?x=1", config.Url);
        Assert.Equal("nonce", config.NonceStr);
        Assert.Equal(10, config.Timestamp);
        Assert.Equal(QyWeixinJsSignature.Sign("ticket", "nonce", 10, config.Url), config.Signature);
        Assert.DoesNotContain("ticket", config.Signature);
    }

    [Fact]
    [DisplayName("网页授权_自建链接带agentid_魔方内打开不带")]
    public void AuthorizeUrl_CubeAndOfficial()
    {
        var official = QyWeixinOAuth.BuildInAppAuthorizeUrl("ww1", "https://a.example/cb", "st", QyWeixinOAuth.ScopePrivate, "100");
        Assert.StartsWith("https://open.weixin.qq.com/connect/oauth2/authorize?", official);
        Assert.Contains("scope=snsapi_privateinfo", official);
        Assert.Contains("agentid=100", official);
        Assert.EndsWith("#wechat_redirect", official);
        Assert.Throws<ArgumentException>(() => QyWeixinOAuth.BuildInAppAuthorizeUrl("ww1", "https://a.example/cb", scope: QyWeixinOAuth.ScopePrivate));

        var client = new QyWeiXin
        {
            CorpId = "ww1#100",
            Secret = "sec",
        };
        var inside = QyWeixinOAuth.BuildLikeCube(client, "https://a.example/cb", "st", "Mozilla/5.0 wxwork/4.0");
        Assert.Contains("/connect/oauth2/authorize?", inside);
        Assert.Contains("appid=ww1", inside);
        Assert.Contains("scope=snsapi_base", inside);
        Assert.DoesNotContain("agentid=", inside);

        var scan = QyWeixinOAuth.BuildLikeCube(client, "https://a.example/cb", "st", "Mozilla/5.0");
        Assert.Contains("qrConnect", scan);
        Assert.Contains("agentid=100", scan);
        Assert.Contains("appid=ww1", scan);
    }

    [Fact]
    [DisplayName("code身份_兼容UserId与userid")]
    public void CodeIdentity_FieldNames()
    {
        var modern = QyWeixinCodeIdentity.From(new Dictionary<String, Object>
        {
            ["userid"] = "zhangsan",
            ["user_ticket"] = "t1",
        });
        Assert.Equal("zhangsan", modern.UserId);
        Assert.Equal("t1", modern.UserTicket);

        var legacy = QyWeixinCodeIdentity.From(new Dictionary<String, Object> { ["UserId"] = "lisi" });
        Assert.Equal("lisi", legacy.UserId);
    }

    [Fact]
    [DisplayName("部门与成员请求体_省略空字段")]
    public void ContactBody_OmitsEmpty()
    {
        var dept = new QyWeixinDepartment { Name = "研发", ParentId = 1, Order = 2 }.ToCreateBody();
        Assert.Equal("研发", dept["name"]);
        Assert.Equal(1, dept["parentid"]);
        Assert.Equal(2, dept["order"]);
        Assert.False(dept.ContainsKey("id"));
        Assert.False(dept.ContainsKey("name_en"));

        var member = new QyWeixinMember { UserId = "u1", Name = "张三", Mobile = "13800000000", Gender = 1 }.ToCreateBody();
        Assert.Equal("u1", member["userid"]);
        Assert.Equal("1", member["gender"]);
        Assert.False(member.ContainsKey("email"));
        Assert.False(member.ContainsKey("to_invite"));
        Assert.Throws<ArgumentNullException>(() => new QyWeixinMember { UserId = "u1" }.ToCreateBody());
    }

    [Fact]
    [DisplayName("日程最小对象_Unix秒与参与人")]
    public void Schedule_Shape()
    {
        var start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);
        var schedule = QyWeixinClient.Schedule("例会", start, end, new[] { "u1", "" }, "说明");
        Assert.Equal("例会", schedule["summary"]);
        Assert.Equal("说明", schedule["description"]);
        var attendees = Assert.IsType<List<Object>>(schedule["attendees"]);
        Assert.Single(attendees);
        Assert.True((Int64)schedule["end_time"] > (Int64)schedule["start_time"]);
    }

    [Fact]
    [DisplayName("工作台keydata_含type与items")]
    public void WorkbenchItem_Shape()
    {
        var item = new QyWeixinWorkbenchItem { Key = "待办", Data = "3", JumpUrl = "https://a.example" }.ToBody();
        Assert.Equal("待办", item["key"]);
        Assert.Equal("3", item["data"]);
        Assert.False(item.ContainsKey("pagepath"));
    }

    [Fact]
    [DisplayName("同步策略_InsertOnly不覆盖已有记录")]
    public void SyncPolicy_InsertOnly()
    {
        Assert.True(QyWeixinSyncPolicy.ShouldUpdateExisting(QyWeixinPullMode.Upsert));
        Assert.False(QyWeixinSyncPolicy.ShouldUpdateExisting(QyWeixinPullMode.InsertOnly));
    }
}
