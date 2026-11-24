using System.ComponentModel;
using NewLife;
using NewLife.Cube.Jobs;
using NewLife.Cube.Web.Models;
using NewLife.Log;
using NewLife.Web.OAuth;
using XCode.Membership;

namespace NewLife.Cube.QyWeixin;

/// <summary>企业微信通讯录推送参数（作业参数 JSON）</summary>
public class QyWeixinPushJobArgument
{
    /// <summary>企业 Id。可写成 corp#agent 复合值</summary>
    [DisplayName("企业Id")]
    public String CorpID { get; set; }

    /// <summary>应用 Id</summary>
    [DisplayName("应用Id")]
    public String AgentID { get; set; }

    /// <summary>应用凭证。读取通讯录需要通讯录 Secret</summary>
    [DisplayName("应用凭证")]
    [Description("读取通讯录需要通讯录 Secret")]
    public String Secret { get; set; }

    /// <summary>
    /// 来源实例标识。用于读取映射表。为空时按 qywx:{CorpID} 生成，没有 CorpID 时使用 qywx。
    /// </summary>
    [DisplayName("来源标识")]
    [Description("映射表中的来源实例。为空时按 qywx:{企业Id} 生成")]
    public String Source { get; set; }
}

/// <summary>
/// 本地推送到企业微信的只读预演作业。拉取企微部门与成员，和本地 Department/User/OrgExternalMap 比对，
/// 输出每个对象将执行的动作。作业不调用任何写接口，也不写映射表。
/// 人员按映射 → User.Name 对比企微 userid → 手机 判断是否已存在。
/// </summary>
[DisplayName("企业微信通讯录推送")]
[Description("只读预演：本地部门/人员推送到企业微信的计划，不写企微、不写映射表")]
[CronJob("QyWeixinPush", "0 0 3 * * ? *", Enable = false)]
public class QyWeixinPushService : CubeJobBase<QyWeixinPushJobArgument>
{
    private readonly ITracer _tracer;

    /// <summary>实例化企业微信通讯录推送作业</summary>
    public QyWeixinPushService(ITracer tracer) => _tracer = tracer;

    /// <summary>执行作业</summary>
    protected override async Task<String> OnExecute(QyWeixinPushJobArgument argument)
    {
        using var span = _tracer?.NewSpan("QyWeixinPush", argument);

        var (client, source) = ResolveClient(argument);
        if (client == null)
            return $"[{DateTime.Now}]未配置企业微信凭据（作业参数 CorpID/Secret，或魔方 OAuth 配置中的企业微信），跳过预演";

        XTrace.WriteLine("企业微信通讯录推送预演：凭据来源={0}，只读，不写企微/映射表", source);

        var remoteDepts = await client.GetDepartments().ConfigureAwait(false);
        if (remoteDepts == null || remoteDepts.Length == 0)
            return $"[{DateTime.Now}]企业微信未返回部门，无法预演";

        var remoteUsers = new List<UserInfo>();
        foreach (var top in QyWeixinOrgMatch.FindTopDepartments(remoteDepts))
        {
            var users = await client.GetUsers(top.Id, true).ConfigureAwait(false);
            if (users != null) remoteUsers.AddRange(users);
        }

        var provider = ResolveProvider(argument, client);
        var plan = QyWeixinPushPlanner.Plan(
            Department.FindAll(),
            User.FindAll(),
            OrgExternalMap.FindAllByProvider(provider, OrgObjectKinds.Department, TenantId),
            OrgExternalMap.FindAllByProvider(provider, OrgObjectKinds.User, TenantId),
            remoteDepts,
            remoteUsers.ToArray(),
            TenantId);

        WriteDetail(plan);

        return $"[{DateTime.Now}]企业微信通讯录推送预演（只读，未写企微/映射表）：{plan.Summary}";
    }

    /// <summary>输出非跳过明细，避免大组织刷屏</summary>
    private static void WriteDetail(QyWeixinPushPlan plan)
    {
        const Int32 MaxDetail = 200;

        var details = plan.Departments.Concat(plan.Users)
            .Where(e => e.Action != QyWeixinPushActions.Skip)
            .ToList();

        foreach (var item in details.Take(MaxDetail))
            XTrace.WriteLine("推送预演 {0} id={1} {2} {3} {4}", item.KindName, item.LocalId, item.Action, item.LocalName, item.Message);

        if (details.Count > MaxDetail)
            XTrace.WriteLine("推送预演：其余 {0} 条明细已省略", details.Count - MaxDetail);
    }

    /// <summary>解析企微客户端。优先级：作业参数顶层字段 → 魔方 OAuth 配置。</summary>
    private static (QyWeiXin Client, String Source) ResolveClient(QyWeixinPushJobArgument argument)
    {
        var options = argument == null ? null : new QyWeixinOptions
        {
            CorpID = argument.CorpID,
            AgentID = argument.AgentID,
            Secret = argument.Secret,
        };
        return QyWeixinCredential.ResolveClient(options);
    }

    /// <summary>映射表来源实例。多主体时用 Source 显式区分，默认带上企业 Id</summary>
    private static String ResolveProvider(QyWeixinPushJobArgument argument, QyWeiXin client)
    {
        if (argument != null && !argument.Source.IsNullOrEmpty()) return argument.Source.Trim();

        var corpId = client?.CorpId;
        return corpId.IsNullOrEmpty() ? "qywx" : "qywx:" + corpId;
    }

    /// <summary>与拉取作业保持一致，当前只处理租户 0</summary>
    private const Int32 TenantId = 0;
}
