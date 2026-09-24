using System.ComponentModel;
using NewLife;
using NewLife.Cube.Jobs;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web.OAuth;
using XCode.Membership;

namespace NewLife.Cube.QyWeixin;

/// <summary>企业微信通讯录同步参数（作业参数 JSON）</summary>
public class QyWeixinJobArgument
{
    /// <summary>企业 Id。可写成 corp#agent 复合值，Init 后同时得到 AgentId</summary>
    [DisplayName("企业Id")]
    public String CorpID { get; set; }

    /// <summary>应用 Id</summary>
    [DisplayName("应用Id")]
    public String AgentID { get; set; }

    /// <summary>应用凭证。通讯录同步需通讯录 Secret</summary>
    [DisplayName("应用凭证")]
    public String Secret { get; set; }

    /// <summary>兼容旧作业参数：原先把凭据 JSON 塞在 HTTP Body 里</summary>
    [DisplayName("请求参数")]
    [Description("兼容旧配置。Body 为 JSON：CorpID / AgentID / Secret；新配置请直接填写上方三项")]
    public String Body { get; set; }
}

/// <summary>
/// 从企业微信拉取部门与人员，写入魔方 <see cref="Department"/> / <see cref="User"/>。
/// 凭据优先作业参数（含旧 Body JSON），缺省回退魔方 OAuth 配置中的企业微信条目。
/// 企微 userid 落为 <see cref="User.Name"/>，企微部门 Id 落为 <see cref="Department.Code"/>。
/// </summary>
[DisplayName("企业微信定时任务")]
[Description("从企业微信定时拉取组织和人员信息")]
[CronJob("QyWeixinService", "0 0 0/4 * * ? *", Enable = false)]
public class QyWeixinService : CubeJobBase<QyWeixinJobArgument>
{
    private readonly ITracer _tracer;

    /// <summary>实例化企业微信通讯录同步作业</summary>
    public QyWeixinService(ITracer tracer) => _tracer = tracer;

    /// <summary>执行作业</summary>
    protected override async Task<String> OnExecute(QyWeixinJobArgument argument)
    {
        using var span = _tracer?.NewSpan("QyWeixinSync", argument);

        var (client, source) = ResolveClient(argument);
        if (client == null)
            return $"[{DateTime.Now}]未配置企业微信凭据（作业参数 CorpID/Secret，或魔方 OAuth 配置中的企业微信），跳过同步";

        XTrace.WriteLine("企业微信通讯录同步：凭据来源={0}", source);

        var remoteDepts = await client.GetDepartments().ConfigureAwait(false);
        if (remoteDepts == null || remoteDepts.Length == 0)
            return $"[{DateTime.Now}]企业微信未返回部门，跳过人员同步";

        var deptMap = SyncDepartments(remoteDepts);

        var root = remoteDepts.FirstOrDefault(d => IsRootParent(d.ParentId)) ?? remoteDepts[0];
        var remoteUsers = await client.GetUsers(root.Id, true).ConfigureAwait(false);

        var userCount = SyncUsers(remoteUsers, deptMap);

        return $"[{DateTime.Now}]企业微信通讯录同步完成：部门{deptMap.Count} 人员{userCount}";
    }

    /// <summary>按父部门优先的顺序写入本地部门，并回填名称 / 上级 / 层级 / 排序。</summary>
    private static IDictionary<String, Department> SyncDepartments(NewLife.Cube.Web.Models.DepartmentInfo[] remoteDepts)
    {
        var remaining = remoteDepts.Where(d => !d.Id.IsNullOrEmpty()).ToList();
        var map = new Dictionary<String, Department>(StringComparer.OrdinalIgnoreCase);

        // 最多轮转部门总数次，避免环或缺失父级时死循环；剩余的挂到根
        for (var round = 0; remaining.Count > 0 && round <= remoteDepts.Length; round++)
        {
            var progress = 0;
            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var info = remaining[i];
                if (!CanSaveDepartment(info, map)) continue;

                map[info.Id] = UpsertDepartment(info, map);
                remaining.RemoveAt(i);
                progress++;
            }

            if (progress == 0)
            {
                foreach (var info in remaining)
                    map[info.Id] = UpsertDepartment(info, map);
                break;
            }
        }

        return map;
    }

    /// <summary>父级已就绪，或本身就是根部门，才写入，避免第一次同步把子部门错挂到 ID=1。</summary>
    private static Boolean CanSaveDepartment(NewLife.Cube.Web.Models.DepartmentInfo info, IDictionary<String, Department> map)
    {
        if (IsRootParent(info.ParentId)) return true;
        return map.ContainsKey(info.ParentId);
    }

    /// <summary>按企微部门 Id（Code）查找或新建本地部门，并更新名称、上级、层级、排序。</summary>
    private static Department UpsertDepartment(NewLife.Cube.Web.Models.DepartmentInfo info, IDictionary<String, Department> map)
    {
        var dep = Department.FindByCode(info.Id) ?? new Department
        {
            Code = info.Id,
            Enable = true,
            Visible = true,
        };

        if (!info.Name.IsNullOrEmpty()) dep.Name = info.Name;
        dep.Sort = info.Order;
        dep.Enable = true;
        dep.Visible = true;

        if (!IsRootParent(info.ParentId) && map.TryGetValue(info.ParentId, out var parent))
        {
            dep.ParentID = parent.ID;
            dep.Level = parent.Level + 1;
        }
        else
        {
            dep.ParentID = 0;
            dep.Level = 1;
        }

        dep.Save();
        return dep;
    }

    /// <summary>按企微 userid 写入本地用户；部门取主部门，避免多部门用户被后处理的部门覆盖。</summary>
    /// <returns>成功保存的人数</returns>
    private static Int32 SyncUsers(NewLife.Cube.Web.Models.UserInfo[] remoteUsers, IDictionary<String, Department> deptMap)
    {
        if (remoteUsers == null || remoteUsers.Length == 0) return 0;

        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var count = 0;

        foreach (var info in remoteUsers)
        {
            if (info.Id.IsNullOrEmpty() || !seen.Add(info.Id)) continue;

            var user = User.FindByName(info.Id);
            if (user == null)
            {
                user = new User
                {
                    Name = info.Id,
                    RegisterTime = DateTime.Now,
                    Password = Rand.NextString(16, true),
                };
            }

            if (!info.Name.IsNullOrEmpty()) user.DisplayName = info.Name;
            // 企微：1 已激活、4 未激活；2 已禁用、5 退出企业
            user.Enable = info.Status is not (2 or 5);

            var deptId = ResolveUserDepartment(info, deptMap);
            if (deptId > 0) user.DepartmentID = deptId;

            user.Save();
            count++;
        }

        return count;
    }

    /// <summary>优先主部门，其次通讯录部门列表中第一个能映射到本地的部门。</summary>
    private static Int32 ResolveUserDepartment(NewLife.Cube.Web.Models.UserInfo info, IDictionary<String, Department> deptMap)
    {
        if (!info.MainDepartment.IsNullOrEmpty() && deptMap.TryGetValue(info.MainDepartment, out var main))
            return main.ID;

        if (info.Department != null)
        {
            foreach (var code in info.Department)
            {
                if (!code.IsNullOrEmpty() && deptMap.TryGetValue(code, out var dep))
                    return dep.ID;
            }
        }

        return 0;
    }

    /// <summary>解析企微客户端。优先级：作业参数顶层字段 → 旧 Body JSON → 魔方 OAuth 配置。</summary>
    private static (QyWeiXin Client, String Source) ResolveClient(QyWeixinJobArgument argument)
    {
        ApplyLegacyBody(argument);

        var options = argument == null ? null : new QyWeixinOptions
        {
            CorpID = argument.CorpID,
            AgentID = argument.AgentID,
            Secret = argument.Secret,
        };
        return QyWeixinCredential.ResolveClient(options);
    }

    /// <summary>兼容旧配置：凭据写在 HTTP Body 的 JSON 里。</summary>
    private static void ApplyLegacyBody(QyWeixinJobArgument argument)
    {
        if (argument == null || argument.Body.IsNullOrEmpty()) return;
        if (!argument.CorpID.IsNullOrEmpty() && !argument.Secret.IsNullOrEmpty()) return;

        Dictionary<String, Object> dic;
        try
        {
            dic = argument.Body.ToJsonEntity<Dictionary<String, Object>>();
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("企业微信作业参数 Body 不是合法 JSON：{0}", ex.Message);
            return;
        }

        if (dic == null) return;

        if (argument.CorpID.IsNullOrEmpty() && dic.TryGetValue("CorpID", out var corpID))
            argument.CorpID = corpID + "";
        if (argument.AgentID.IsNullOrEmpty() && dic.TryGetValue("AgentID", out var agentID))
            argument.AgentID = agentID + "";
        if (argument.Secret.IsNullOrEmpty() && dic.TryGetValue("Secret", out var secret))
            argument.Secret = secret + "";
    }

    /// <summary>企业微信根部门的 parentid 为 0 或空。</summary>
    private static Boolean IsRootParent(String parentId)
        => parentId.IsNullOrEmpty() || parentId.EqualIgnoreCase("0");
}
