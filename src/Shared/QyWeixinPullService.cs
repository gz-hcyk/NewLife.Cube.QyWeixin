using System.ComponentModel;
using NewLife;
using NewLife.Cube.Jobs;
using NewLife.Cube.Web.Models;
using NewLife.Log;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web.OAuth;
using XCode.Membership;

namespace NewLife.Cube.QyWeixin;

/// <summary>企业微信通讯录拉取参数（作业参数 JSON）</summary>
public class QyWeixinPullJobArgument
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

    /// <summary>
    /// 来源实例标识。写入映射表，用于区分同一厂商的多个主体。
    /// 为空时按 qywx:{CorpID} 生成，没有 CorpID 时使用 qywx。
    /// </summary>
    [DisplayName("来源标识")]
    [Description("映射表中的来源实例。为空时按 qywx:{企业Id} 生成")]
    public String Source { get; set; }

    /// <summary>兼容旧作业参数：原先把凭据 JSON 塞在 HTTP Body 里</summary>
    [DisplayName("请求参数")]
    [Description("兼容旧配置。Body 为 JSON：CorpID / AgentID / Secret；新配置请直接填写上方三项")]
    public String Body { get; set; }

    /// <summary>
    /// 人员拉取策略。默认 Upsert：企微为准，覆盖本地人员的显示名、启用、主部门。
    /// InsertOnly：只补本地没有的人员，不改已有人员。部门不受该策略影响。
    /// 本作业从不调用通讯录写接口，不会把本地数据推回企微。
    /// </summary>
    [DisplayName("拉取策略")]
    [Description("Upsert=企微覆盖本地人员映射字段；InsertOnly=只插入本地不存在的人员。部门始终以本地为主")]
    public QyWeixinPullMode PullMode { get; set; } = QyWeixinPullMode.Upsert;
}

/// <summary>
/// 定时同步只从企微拉到魔方，方向固定。
/// 部门优先建立“外部部门 → 本地部门”的映射；没有映射且本地不存在时创建，已有本地部门不做修改。
/// 人员仍按 <see cref="QyWeixinPullMode"/> 决定是否覆盖本地显示名、启用和主部门。
/// 写接口（<see cref="QyWeixinClient"/> 的部门/成员/标签）只推到企微，不改本地 Department/User。
/// </summary>
public enum QyWeixinPullMode
{
    /// <summary>已有本地人员也按企微更新映射字段</summary>
    Upsert = 0,

    /// <summary>只插入本地不存在的人员</summary>
    InsertOnly = 1,
}

/// <summary>同步冲突策略的可测试入口。作业本身不写企微</summary>
public static class QyWeixinSyncPolicy
{
    /// <summary>该模式下是否允许更新已存在的本地人员。部门不再更新，不受此策略影响</summary>
    /// <param name="mode">拉取策略</param>
    /// <returns>Upsert 为 true，InsertOnly 为 false</returns>
    public static Boolean ShouldUpdateExisting(QyWeixinPullMode mode) => mode != QyWeixinPullMode.InsertOnly;
}

/// <summary>
/// 从企业微信拉取部门与人员，把外部身份写入 <see cref="OrgExternalMap"/>。
/// 部门优先按映射挂接；没有映射时按完整路径匹配（含去掉源根名的短路径），
/// 仍未命中时在映射父级下按同级同名挂接，仍找不到则创建，
/// 已有本地部门不做修改。企微部门 Id 始终以字符串保存在映射表，本地 Department.Code 保持业务编码，
/// 不写入外部 Id，避免字符串编码与 32 位部门 Id 混用。人员按映射表、同名 userid、手机、邮箱单步归并，
/// 并把绑定固化到映射表。凭据优先作业参数（含旧 Body JSON），缺省回退魔方 OAuth 配置中的企业微信条目。
/// 只读拉取，不调用通讯录写接口。
/// </summary>
[DisplayName("企业微信通讯录拉取")]
[Description("从企业微信定时拉取组织和人员信息")]
[CronJob("QyWeixinPull", "0 0 0/4 * * ? *", Enable = false)]
public class QyWeixinPullService : CubeJobBase<QyWeixinPullJobArgument>
{
    private readonly ITracer _tracer;

    /// <summary>实例化企业微信通讯录拉取作业</summary>
    public QyWeixinPullService(ITracer tracer) => _tracer = tracer;

    /// <summary>执行作业</summary>
    protected override async Task<String> OnExecute(QyWeixinPullJobArgument argument)
    {
        using var span = _tracer?.NewSpan("QyWeixinSync", argument);

        var (client, source) = ResolveClient(argument);
        if (client == null)
            return $"[{DateTime.Now}]未配置企业微信凭据（作业参数 CorpID/Secret，或魔方 OAuth 配置中的企业微信），跳过同步";

        XTrace.WriteLine("企业微信通讯录同步：凭据来源={0}", source);

        var remoteDepts = await client.GetDepartments().ConfigureAwait(false);
        if (remoteDepts == null || remoteDepts.Length == 0)
            return $"[{DateTime.Now}]企业微信未返回部门，跳过人员同步";

        var provider = ResolveProvider(argument, client);
        var mode = argument?.PullMode ?? QyWeixinPullMode.Upsert;

        // 部门只建映射，不写本地部门
        var deptResult = SyncDepartments(remoteDepts, provider);
        XTrace.WriteLine("企业微信部门映射：{0}", deptResult.Summary);

        var remoteUsers = new List<UserInfo>();
        foreach (var top in QyWeixinOrgMatch.FindTopDepartments(remoteDepts))
        {
            var users = await client.GetUsers(top.Id, true).ConfigureAwait(false);
            if (users != null) remoteUsers.AddRange(users);
        }

        var userResult = SyncUsers(remoteUsers.ToArray(), deptResult.Map, provider, mode);

        return $"[{DateTime.Now}]企业微信通讯录同步完成：部门 {deptResult.Summary}；人员 {userResult.Summary}";
    }

    /// <summary>把外部部门挂接到本地部门；没有映射且本地不存在时创建，已有本地部门不做修改</summary>
    private static DepartmentSyncResult SyncDepartments(DepartmentInfo[] remoteDepts, String provider)
    {
        var result = new DepartmentSyncResult();
        var nodes = QyWeixinOrgMatch.BuildDepartmentNodes(remoteDepts);
        var local = Department.FindAll();
        var localPaths = QyWeixinOrgMatch.BuildLocalDepartmentPaths(local, TenantId);

        var maps = OrgExternalMap.FindAllByProvider(provider, OrgObjectKinds.Department, TenantId)
            .GroupBy(e => e.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);
        var usedLocal = new HashSet<Int32>(maps.Values.Select(e => e.LocalId));

        // 父级优先，保证同一路径下的处理顺序稳定
        foreach (var node in nodes.Values.OrderBy(e => e.Depth).ThenBy(e => e.Info.Order))
        {
            var info = node.Info;
            maps.TryGetValue(info.Id, out var map);

            if (map != null)
            {
                var dep = Department.FindByID(map.LocalId);
                if (dep != null && dep.TenantId == TenantId)
                {
                    result.Map[info.Id] = dep;
                    UpdateMap(map, info, node.Path);
                    result.Bound++;
                    continue;
                }

                // 本地部门已被删除，下面重新匹配并改写映射
                result.Invalid++;
            }

            var matched = MatchDepartment(node, localPaths, usedLocal);
            var created = false;

            // 完整路径可能因本地根名不同而失效，再按映射父级下的同级名称查找
            matched ??= FindByLocalParent(local, node, maps, usedLocal);

            if (matched == null)
            {
                matched = CreateLocalDepartment(node, maps, result);
                if (matched == null) continue;
                created = true;
            }

            map ??= new OrgExternalMap
            {
                TenantId = TenantId,
                Provider = provider,
                ObjectType = (Int32)OrgObjectKinds.Department,
                ExternalId = info.Id,
            };
            map.LocalId = matched.ID;
            map.ExternalParentId = info.ParentId;
            map.ExternalName = info.Name;
            map.ExternalPath = node.Path;
            map.SyncTime = DateTime.Now;
            map.Save();

            maps[info.Id] = map;
            result.Map[info.Id] = matched;
            usedLocal.Add(matched.ID);
            if (!created) result.AutoMapped++;
        }

        return result;
    }

    /// <summary>按完整路径找本地部门。候选路径依次尝试，已被占用的本地部门不参与自动匹配</summary>
    private static Department MatchDepartment(QyWeixinDepartmentNode node, IDictionary<String, Department> localPaths, ISet<Int32> usedLocal)
    {
        foreach (var path in QyWeixinOrgMatch.GetMatchPaths(node))
        {
            if (!localPaths.TryGetValue(path, out var dep)) continue;
            if (usedLocal.Contains(dep.ID)) continue;

            return dep;
        }

        return null;
    }

    /// <summary>在映射到的本地父级下按同名查找；根部门退化为唯一根</summary>
    private static Department FindByLocalParent(IList<Department> local, QyWeixinDepartmentNode node, IDictionary<String, OrgExternalMap> maps, ISet<Int32> usedLocal)
    {
        var parentId = GetLocalParentId(node, maps);
        if (parentId < 0) return null;

        var matches = local.Where(e => e.TenantId == TenantId && e.ParentID == parentId &&
            e.Name.EqualIgnoreCase(node.Info.Name) && !usedLocal.Contains(e.ID)).Take(2).ToList();
        if (matches.Count == 1) return matches[0];

        // 根部门名称可能与其他系统不一致，只有一个本地根时直接挂接
        if (node.IsRoot && matches.Count == 0)
        {
            var roots = local.Where(e => e.TenantId == TenantId && e.ParentID == 0 && !usedLocal.Contains(e.ID)).Take(2).ToList();
            if (roots.Count == 1) return roots[0];
        }

        return null;
    }

    /// <summary>取外部父部门对应的本地部门编号。根部门为 0，父级尚未映射时返回 -1</summary>
    private static Int32 GetLocalParentId(QyWeixinDepartmentNode node, IDictionary<String, OrgExternalMap> maps)
    {
        if (node.IsRoot) return 0;

        var parentId = node.Info.ParentId;
        if (parentId.IsNullOrEmpty()) return 0;

        return maps.TryGetValue(parentId, out var map) ? map.LocalId : -1;
    }

    /// <summary>按企微部门创建本地部门。父级未映射时跳过，避免挂到错误层级</summary>
    private static Department CreateLocalDepartment(QyWeixinDepartmentNode node, IDictionary<String, OrgExternalMap> maps, DepartmentSyncResult result)
    {
        var info = node.Info;
        var parentId = GetLocalParentId(node, maps);
        if (parentId < 0 || info.Name.IsNullOrEmpty())
        {
            result.Unmapped.Add(node.Path);
            return null;
        }

        // 企微部门 Id 只写 OrgExternalMap。Code 是本地业务编码，不能被外部 Id 覆盖，也不保证能转成 Int32。
        var department = new Department
        {
            TenantId = TenantId,
            Name = info.Name,
            ParentID = parentId,
            Sort = info.Order,
            Enable = true,
            Visible = true,
        };
        department.Save();

        if (department.ID <= 0)
        {
            result.Unmapped.Add(node.Path);
            return null;
        }

        result.Created++;
        return department;
    }

    private static void UpdateMap(OrgExternalMap map, DepartmentInfo info, String path)
    {
        map.ExternalParentId = info.ParentId;
        map.ExternalName = info.Name;
        map.ExternalPath = path;
        map.SyncTime = DateTime.Now;
        map.Save();
    }

    /// <summary>按企微 userid 写入本地用户，并把身份绑定固化到映射表</summary>
    private static UserSyncResult SyncUsers(UserInfo[] remoteUsers, IDictionary<String, Department> deptMap, String provider, QyWeixinPullMode mode)
    {
        var result = new UserSyncResult();
        if (remoteUsers == null || remoteUsers.Length == 0) return result;

        var userMaps = OrgExternalMap.FindAllByProvider(provider, OrgObjectKinds.User, TenantId)
            .GroupBy(e => e.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var allUsers = User.FindAll();
        var byId = allUsers.ToDictionary(e => e.ID);
        var byName = BuildUserIndex(allUsers, e => e.Name);
        var byMobile = BuildUserIndex(allUsers, e => e.Mobile);
        var byMail = BuildUserIndex(allUsers, e => e.Mail);
        var boundByLocal = userMaps.Values.Where(e => e.LocalId > 0).GroupBy(e => e.LocalId).ToDictionary(e => e.Key, e => e.First());

        var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in remoteUsers)
        {
            if (info == null || info.Id.IsNullOrEmpty() || !seen.Add(info.Id)) continue;

            userMaps.TryGetValue(info.Id, out var map);
            User user = null;
            if (map != null && map.LocalId > 0) byId.TryGetValue(map.LocalId, out user);
            if (map != null && user == null) result.Invalid++;

            if (user == null)
            {
                user = MatchUser(info, byName, byMobile, byMail, boundByLocal, out var conflict);
                if (conflict != null)
                {
                    result.Conflicts.Add(conflict);
                    continue;
                }
            }

            if (user == null)
            {
                user = new User
                {
                    Name = info.Id,
                    RegisterTime = DateTime.Now,
                    Password = Rand.NextString(16, true),
                };
                if (!info.Mobile.IsNullOrEmpty()) user.Mobile = info.Mobile;
                if (!info.Mail.IsNullOrEmpty()) user.Mail = info.Mail;
                // 企微：1 已激活、4 未激活；2 已禁用、5 退出企业
                user.Enable = info.Status is not (2 or 5);

                var deptId = ResolveUserDepartment(info, deptMap);
                if (deptId > 0) user.DepartmentID = deptId;
                else result.NoDepartment++;

                user.Save();
                result.Created++;
            }
            else if (QyWeixinSyncPolicy.ShouldUpdateExisting(mode))
            {
                if (!info.Name.IsNullOrEmpty()) user.DisplayName = info.Name;
                user.Enable = info.Status is not (2 or 5);

                var deptId = ResolveUserDepartment(info, deptMap);
                if (deptId > 0) user.DepartmentID = deptId;

                user.Save();
                result.Updated++;
            }
            else
            {
                result.Skipped++;
            }

            if (map == null)
            {
                map = new OrgExternalMap
                {
                    TenantId = TenantId,
                    Provider = provider,
                    ObjectType = (Int32)OrgObjectKinds.User,
                    ExternalId = info.Id,
                };
                userMaps[info.Id] = map;
                boundByLocal[user.ID] = map;
            }

            map.LocalId = user.ID;
            map.ExternalName = info.Name;
            map.SyncTime = DateTime.Now;
            map.Save();
            result.Bound++;
        }

        return result;
    }

    /// <summary>按本地同名 userid、手机、邮箱依次归并。返回冲突原因时表示不能绑定该用户</summary>
    private static User MatchUser(UserInfo info, IDictionary<String, List<User>> byName, IDictionary<String, List<User>> byMobile, IDictionary<String, List<User>> byMail, IDictionary<Int32, OrgExternalMap> boundByLocal, out String conflict)
    {
        conflict = null;
        User found = null;

        // 本地 User.Name 即企微 userid：本库新建用户以 userid 作 Name，WxWorkAgent 推送成员时也以 Name 作 userid
        if (!info.Id.IsNullOrEmpty() && byName.TryGetValue(info.Id, out var names))
        {
            if (names.Count > 1)
            {
                conflict = $"本地存在多个同名用户：{info.Id}";
                return null;
            }
            found = names[0];
        }

        if (found == null && !info.Mobile.IsNullOrEmpty() && byMobile.TryGetValue(info.Mobile, out var mobiles))
        {
            if (mobiles.Count > 1)
            {
                conflict = $"手机号匹配到多个本地用户：{info.Mobile}（{info.Id}）";
                return null;
            }
            found = mobiles[0];
        }

        if (found == null && !info.Mail.IsNullOrEmpty() && byMail.TryGetValue(info.Mail, out var mails))
        {
            if (mails.Count > 1)
            {
                conflict = $"邮箱匹配到多个本地用户：{info.Mail}（{info.Id}）";
                return null;
            }
            found = mails[0];
        }

        if (found != null && boundByLocal.TryGetValue(found.ID, out var other) && !other.ExternalId.EqualIgnoreCase(info.Id))
        {
            conflict = $"本地用户 {found.Name} 已绑定外部身份 {other.ExternalId}，不能再绑定 {info.Id}";
            return null;
        }

        return found;
    }

    private static Dictionary<String, List<User>> BuildUserIndex(IEnumerable<User> users, Func<User, String> selector)
    {
        var dic = new Dictionary<String, List<User>>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            var key = selector(user);
            if (key.IsNullOrEmpty()) continue;

            if (!dic.TryGetValue(key, out var list)) dic[key] = list = new List<User>();
            list.Add(user);
        }
        return dic;
    }

    /// <summary>优先主部门，其次通讯录部门列表中第一个能映射到本地的部门。</summary>
    private static Int32 ResolveUserDepartment(UserInfo info, IDictionary<String, Department> deptMap)
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
    private static (QyWeiXin Client, String Source) ResolveClient(QyWeixinPullJobArgument argument)
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

    /// <summary>映射表来源实例。多主体时用 Source 显式区分，默认带上企业 Id</summary>
    private static String ResolveProvider(QyWeixinPullJobArgument argument, QyWeiXin client)
    {
        if (argument != null && !argument.Source.IsNullOrEmpty()) return argument.Source.Trim();

        var corpId = client?.CorpId;
        return corpId.IsNullOrEmpty() ? "qywx" : "qywx:" + corpId;
    }

    /// <summary>兼容旧配置：凭据写在 HTTP Body 的 JSON 里。</summary>
    private static void ApplyLegacyBody(QyWeixinPullJobArgument argument)
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

    /// <summary>与魔方 OAuth 配置保持一致，当前作业只同步租户 0</summary>
    private const Int32 TenantId = 0;

    private sealed class DepartmentSyncResult
    {
        public Dictionary<String, Department> Map { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<String> Unmapped { get; } = new();
        public Int32 Bound { get; set; }
        public Int32 AutoMapped { get; set; }
        public Int32 Created { get; set; }
        public Int32 Invalid { get; set; }

        public String Summary
        {
            get
            {
                var text = $"映射{Bound} 自动匹配{AutoMapped} 新建{Created} 未匹配{Unmapped.Count}";
                if (Invalid > 0) text += $" 失效{Invalid}";
                if (Unmapped.Count > 0)
                {
                    var names = String.Join("、", Unmapped.Take(10));
                    text += $"（{names}{(Unmapped.Count > 10 ? " 等" : "")}）";
                }
                return text;
            }
        }
    }

    private sealed class UserSyncResult
    {
        public Int32 Created { get; set; }
        public Int32 Updated { get; set; }
        public Int32 Skipped { get; set; }
        public Int32 Bound { get; set; }
        public Int32 Invalid { get; set; }
        public Int32 NoDepartment { get; set; }
        public List<String> Conflicts { get; } = new();

        public String Summary
        {
            get
            {
                var text = $"新增{Created} 更新{Updated} 跳过{Skipped} 映射{Bound}";
                if (NoDepartment > 0) text += $" 无部门{NoDepartment}";
                if (Conflicts.Count > 0) text += $" 冲突{Conflicts.Count}";
                if (Invalid > 0) text += $" 失效{Invalid}";
                if (Conflicts.Count > 0) text += $"（{Conflicts[0]}{(Conflicts.Count > 1 ? " 等" : "")}）";
                return text;
            }
        }
    }
}
