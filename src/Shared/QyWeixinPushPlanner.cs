using NewLife;
using NewLife.Cube.Web.Models;
using XCode.Membership;

namespace NewLife.Cube.QyWeixin;

/// <summary>推送预演对象类型</summary>
public enum QyWeixinPushKinds
{
    /// <summary>部门</summary>
    Department = 1,

    /// <summary>人员</summary>
    User = 2,
}

/// <summary>推送预演动作</summary>
public enum QyWeixinPushActions
{
    /// <summary>已存在且无差异，跳过</summary>
    Skip = 0,

    /// <summary>企微已存在，将建立映射</summary>
    Bind = 1,

    /// <summary>企微已存在，将更新字段</summary>
    Update = 2,

    /// <summary>企微不存在，将新建</summary>
    Create = 3,

    /// <summary>无法安全处理，需人工确认</summary>
    Conflict = 4,

    /// <summary>依赖未解析（父部门或归属部门），暂不处理</summary>
    Pending = 5,
}

/// <summary>推送预演明细</summary>
public sealed class QyWeixinPushItem
{
    /// <summary>对象类型</summary>
    public QyWeixinPushKinds Kind { get; set; }

    /// <summary>本地编号。部门为 Department.ID，人员为 User.ID</summary>
    public Int32 LocalId { get; set; }

    /// <summary>本地名称。人员为 User.Name，将被当作企微 userid 提交</summary>
    public String LocalName { get; set; }

    /// <summary>企微 Id。将新建时为空，人员为计划使用的 userid</summary>
    public String ExternalId { get; set; }

    /// <summary>预演动作</summary>
    public QyWeixinPushActions Action { get; set; }

    /// <summary>说明</summary>
    public String Message { get; set; }

    /// <summary>对象类型名称</summary>
    public String KindName => Kind == QyWeixinPushKinds.Department ? "部门" : "人员";
}

/// <summary>推送预演结果</summary>
public sealed class QyWeixinPushPlan
{
    /// <summary>部门计划</summary>
    public List<QyWeixinPushItem> Departments { get; } = new();

    /// <summary>人员计划</summary>
    public List<QyWeixinPushItem> Users { get; } = new();

    /// <summary>汇总</summary>
    public String Summary => $"部门 {BuildSummary(Departments)}；人员 {BuildSummary(Users)}";

    private static String BuildSummary(IReadOnlyList<QyWeixinPushItem> items)
        => $"绑定{Count(items, QyWeixinPushActions.Bind)} 跳过{Count(items, QyWeixinPushActions.Skip)} " +
           $"更新{Count(items, QyWeixinPushActions.Update)} 新建{Count(items, QyWeixinPushActions.Create)} " +
           $"冲突{Count(items, QyWeixinPushActions.Conflict)} 待定{Count(items, QyWeixinPushActions.Pending)}";

    private static Int32 Count(IReadOnlyList<QyWeixinPushItem> items, QyWeixinPushActions action)
    {
        var count = 0;
        foreach (var item in items)
        {
            if (item.Action == action) count++;
        }
        return count;
    }
}

/// <summary>
/// 本地推送到企业微信的只读预演。只计算计划，不写企微、不写映射表。
/// 部门先复用有效映射；缺失时根部门绑定企微根 1，其余按父级顺序计划新建。
/// 人员按映射 → User.Name 对比企微 userid → 手机 判断是否已存在，最后才计划新建。
/// </summary>
public static class QyWeixinPushPlanner
{
    /// <summary>企微根部门固定 Id</summary>
    public const String RootExternalId = "1";

    private const String PlannedPrefix = "~new:";

    /// <summary>
    /// 计算推送预演计划。
    /// </summary>
    /// <param name="localDepartments">本地部门</param>
    /// <param name="localUsers">本地用户</param>
    /// <param name="departmentMaps">当前来源实例下的部门映射</param>
    /// <param name="userMaps">当前来源实例下的用户映射</param>
    /// <param name="remoteDepartments">企微部门列表</param>
    /// <param name="remoteUsers">企微成员列表</param>
    /// <param name="tenantId">租户</param>
    /// <returns>预演计划</returns>
    public static QyWeixinPushPlan Plan(
        IEnumerable<Department> localDepartments,
        IEnumerable<User> localUsers,
        IEnumerable<OrgExternalMap> departmentMaps,
        IEnumerable<OrgExternalMap> userMaps,
        DepartmentInfo[] remoteDepartments,
        UserInfo[] remoteUsers,
        Int32 tenantId = 0)
    {
        var plan = new QyWeixinPushPlan();

        var depts = (localDepartments ?? Array.Empty<Department>())
            .Where(e => e != null && e.TenantId == tenantId)
            .ToList();
        var users = (localUsers ?? Array.Empty<User>()).Where(e => e != null).ToList();

        var remoteDepts = BuildRemoteDepartmentIndex(remoteDepartments);
        var (remoteById, remoteByMobile) = BuildRemoteUserIndex(remoteUsers);

        var deptMapByLocal = (departmentMaps ?? Array.Empty<OrgExternalMap>())
            .Where(e => e != null && e.LocalId > 0 && !e.ExternalId.IsNullOrEmpty())
            .GroupBy(e => e.LocalId)
            .ToDictionary(e => e.Key, e => e.First());
        var userMapByLocal = (userMaps ?? Array.Empty<OrgExternalMap>())
            .Where(e => e != null && e.LocalId > 0 && !e.ExternalId.IsNullOrEmpty())
            .GroupBy(e => e.LocalId)
            .ToDictionary(e => e.Key, e => e.First());

        var extByLocal = PlanDepartments(plan, depts, deptMapByLocal, remoteDepts);
        PlanUsers(plan, users, userMapByLocal, remoteById, remoteByMobile, extByLocal);

        return plan;
    }

    /// <summary>判断本地用户名是否可以作为企微 userid 提交</summary>
    /// <param name="userId">本地 User.Name</param>
    /// <returns>合法时返回 true</returns>
    public static Boolean IsValidUserId(String userId)
    {
        if (userId.IsNullOrEmpty()) return false;

        var text = userId.Trim();
        if (text.Length < 1 || text.Length > 64) return false;

        foreach (var ch in text)
        {
            var ok = ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '@' or '.';
            if (!ok) return false;
        }

        return true;
    }

    private static Dictionary<String, DepartmentInfo> BuildRemoteDepartmentIndex(DepartmentInfo[] remote)
    {
        var dic = new Dictionary<String, DepartmentInfo>(StringComparer.OrdinalIgnoreCase);
        if (remote == null) return dic;

        foreach (var item in remote)
        {
            if (item == null || item.Id.IsNullOrEmpty()) continue;
            dic[item.Id] = item;
        }
        return dic;
    }

    private static (Dictionary<String, UserInfo> ById, Dictionary<String, List<UserInfo>> ByMobile) BuildRemoteUserIndex(UserInfo[] remote)
    {
        var byId = new Dictionary<String, UserInfo>(StringComparer.OrdinalIgnoreCase);
        var byMobile = new Dictionary<String, List<UserInfo>>(StringComparer.OrdinalIgnoreCase);
        if (remote == null) return (byId, byMobile);

        foreach (var item in remote)
        {
            if (item == null || item.Id.IsNullOrEmpty()) continue;
            byId[item.Id] = item;

            if (item.Mobile.IsNullOrEmpty()) continue;
            if (!byMobile.TryGetValue(item.Mobile, out var list)) byMobile[item.Mobile] = list = new List<UserInfo>();
            list.Add(item);
        }
        return (byId, byMobile);
    }

    private static Dictionary<Int32, String> PlanDepartments(
        QyWeixinPushPlan plan,
        IList<Department> depts,
        IDictionary<Int32, OrgExternalMap> maps,
        IDictionary<String, DepartmentInfo> remoteDepts)
    {
        var extByLocal = new Dictionary<Int32, String>();
        var byId = new Dictionary<Int32, Department>();
        foreach (var dept in depts)
        {
            if (!byId.ContainsKey(dept.ID)) byId[dept.ID] = dept;
        }

        var duplicateSiblings = FindDuplicateSiblings(depts);
        var rootClaimed = false;

        foreach (var dept in depts.OrderBy(e => GetDepth(e, byId)).ThenBy(e => e.Sort).ThenBy(e => e.ID))
        {
            var name = dept.Name?.Trim();
            var item = new QyWeixinPushItem
            {
                Kind = QyWeixinPushKinds.Department,
                LocalId = dept.ID,
                LocalName = name,
            };

            var map = maps.TryGetValue(dept.ID, out var found) ? found : null;
            var stale = map != null && !remoteDepts.ContainsKey(map.ExternalId);

            if (name.IsNullOrEmpty())
            {
                item.Action = QyWeixinPushActions.Conflict;
                item.Message = (stale ? "映射失效；" : "") + "部门名称为空";
                plan.Departments.Add(item);
                continue;
            }

            if (map != null && !stale)
            {
                extByLocal[dept.ID] = map.ExternalId;
                if (dept.ParentID == 0 && map.ExternalId.EqualIgnoreCase(RootExternalId)) rootClaimed = true;

                item.ExternalId = map.ExternalId;
                var diffs = GetDepartmentDiffs(dept, map.ExternalId, extByLocal, remoteDepts);
                if (diffs.Count == 0)
                {
                    item.Action = QyWeixinPushActions.Skip;
                    item.Message = "映射有效，无差异";
                }
                else
                {
                    item.Action = QyWeixinPushActions.Update;
                    item.Message = "映射有效；将更新：" + String.Join("、", diffs);
                }

                plan.Departments.Add(item);
                continue;
            }

            if (dept.ParentID == 0)
            {
                if (stale)
                {
                    item.Action = QyWeixinPushActions.Conflict;
                    item.Message = "根部门映射失效，不能自动改绑，需人工确认";
                    plan.Departments.Add(item);
                    continue;
                }

                if (!remoteDepts.ContainsKey(RootExternalId))
                {
                    item.Action = QyWeixinPushActions.Conflict;
                    item.Message = "企业微信未返回根部门 1";
                    plan.Departments.Add(item);
                    continue;
                }

                if (!rootClaimed)
                {
                    if (maps.Values.Any(e => e.LocalId != dept.ID && e.ExternalId.EqualIgnoreCase(RootExternalId)))
                    {
                        item.Action = QyWeixinPushActions.Conflict;
                        item.Message = "企微根部门 1 已被其他本地部门映射，需人工确认";
                        plan.Departments.Add(item);
                        continue;
                    }

                    rootClaimed = true;
                    extByLocal[dept.ID] = RootExternalId;
                    item.ExternalId = RootExternalId;
                    item.Action = QyWeixinPushActions.Bind;
                    item.Message = "将绑定企微根部门 1";
                    if (remoteDepts.TryGetValue(RootExternalId, out var root) && !root.Name.IsNullOrEmpty() &&
                        !root.Name.Trim().EqualIgnoreCase(name))
                        item.Message += $"（企微根名：{root.Name}）";

                    plan.Departments.Add(item);
                    continue;
                }

                // 多个本地根部门时，第一个绑定企微根，其余作为企微根下的顶层部门新建
                if (HasRemoteSibling(remoteDepts, RootExternalId, name))
                {
                    item.Action = QyWeixinPushActions.Conflict;
                    item.Message = "企微已存在同名顶层部门但未映射，需人工确认";
                    plan.Departments.Add(item);
                    continue;
                }

                item.Action = QyWeixinPushActions.Create;
                item.Message = "将新建（企微根 1 下的顶层部门）";
                item.ExternalId = null;
                extByLocal[dept.ID] = PlannedPrefix + dept.ID;
                plan.Departments.Add(item);
                continue;
            }

            if (duplicateSiblings.Contains(dept.ID))
            {
                item.Action = QyWeixinPushActions.Conflict;
                item.Message = (stale ? "映射失效；" : "") + "本地同级存在重名部门，企微会拒绝";
                plan.Departments.Add(item);
                continue;
            }

            if (!extByLocal.TryGetValue(dept.ParentID, out var parentExternal) || parentExternal.IsNullOrEmpty())
            {
                item.Action = QyWeixinPushActions.Pending;
                item.Message = (stale ? "映射失效；" : "") + "父级部门未映射，暂不处理";
                plan.Departments.Add(item);
                continue;
            }

            var parentPlanned = parentExternal.StartsWith(PlannedPrefix, StringComparison.Ordinal);
            if (!parentPlanned && HasRemoteSibling(remoteDepts, parentExternal, name))
            {
                item.Action = QyWeixinPushActions.Conflict;
                item.Message = (stale ? "映射失效；" : "") + "企微已存在同名同级部门但未映射，需人工确认";
                plan.Departments.Add(item);
                continue;
            }

            item.Action = QyWeixinPushActions.Create;
            item.Message = (stale ? "映射失效，将新建" : "将新建") + (parentPlanned ? "（父级将新建）" : "");
            item.ExternalId = null;
            extByLocal[dept.ID] = PlannedPrefix + dept.ID;
            plan.Departments.Add(item);
        }

        return extByLocal;
    }

    private static void PlanUsers(
        QyWeixinPushPlan plan,
        IList<User> users,
        IDictionary<Int32, OrgExternalMap> maps,
        IDictionary<String, UserInfo> remoteById,
        IDictionary<String, List<UserInfo>> remoteByMobile,
        IDictionary<Int32, String> departmentExternals)
    {
        var boundByExternal = maps.Values
            .GroupBy(e => e.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var user in users.OrderBy(e => e.ID))
        {
            var name = user.Name?.Trim();
            var item = new QyWeixinPushItem
            {
                Kind = QyWeixinPushKinds.User,
                LocalId = user.ID,
                LocalName = name,
            };

            var map = maps.TryGetValue(user.ID, out var found) ? found : null;
            var stale = false;
            UserInfo remote = null;
            var via = (String)null;

            if (map != null)
            {
                if (remoteById.TryGetValue(map.ExternalId, out remote))
                    via = "映射";
                else
                    stale = true;
            }

            if (remote == null && !name.IsNullOrEmpty() && remoteById.TryGetValue(name, out var byName))
            {
                remote = byName;
                via = "同名";
            }

            if (remote == null && !user.Mobile.IsNullOrEmpty() && remoteByMobile.TryGetValue(user.Mobile, out var mobiles))
            {
                if (mobiles.Count > 1)
                {
                    item.Action = QyWeixinPushActions.Conflict;
                    item.Message = (stale ? "映射失效；" : "") + $"手机号匹配到多个企微成员：{user.Mobile}";
                    plan.Users.Add(item);
                    continue;
                }

                remote = mobiles[0];
                via = "手机";
            }

            if (remote == null)
            {
                if (!IsValidUserId(name))
                {
                    item.Action = QyWeixinPushActions.Conflict;
                    item.Message = (stale ? "映射失效；" : "") + "User.Name 不是合法的企微 userid（仅限字母、数字、_ - @ .）";
                    plan.Users.Add(item);
                    continue;
                }

                if (user.DepartmentID <= 0 || !departmentExternals.TryGetValue(user.DepartmentID, out var deptExternal))
                {
                    item.Action = QyWeixinPushActions.Pending;
                    item.Message = (stale ? "映射失效；" : "") + "归属部门未映射，暂不处理";
                    plan.Users.Add(item);
                    continue;
                }

                var deptPlanned = deptExternal.StartsWith(PlannedPrefix, StringComparison.Ordinal);
                item.Action = QyWeixinPushActions.Create;
                item.ExternalId = name;
                item.Message = "将新建（userid=User.Name）" + (deptPlanned ? "，待部门创建" : "");
                plan.Users.Add(item);
                continue;
            }

            if (boundByExternal.TryGetValue(remote.Id, out var bound) && bound.LocalId != user.ID)
            {
                item.Action = QyWeixinPushActions.Conflict;
                item.Message = (stale ? "映射失效；" : "") +
                    $"企微成员 {remote.Id} 已绑定本地用户 #{bound.LocalId}，不能再绑定 #{user.ID}";
                plan.Users.Add(item);
                continue;
            }

            item.ExternalId = remote.Id;
            var diffs = GetUserDiffs(user, remote, departmentExternals);
            var prefix = stale ? "映射失效；" : "";

            var deptNote = "";
            if (user.DepartmentID <= 0)
                deptNote = "；本地无部门";
            else if (!departmentExternals.ContainsKey(user.DepartmentID))
                deptNote = "；归属部门未映射";

            if (diffs.Count == 0)
            {
                item.Action = via == "映射" ? QyWeixinPushActions.Skip : QyWeixinPushActions.Bind;
                item.Message = prefix + via + "命中，无差异" +
                    (item.Action == QyWeixinPushActions.Bind ? "，将建立映射" : "") + deptNote;
            }
            else
            {
                item.Action = QyWeixinPushActions.Update;
                item.Message = prefix + via + "命中；将更新：" + String.Join("、", diffs) + deptNote;
            }

            plan.Users.Add(item);
        }
    }

    private static List<String> GetDepartmentDiffs(
        Department dept,
        String externalId,
        IDictionary<Int32, String> departmentExternals,
        IDictionary<String, DepartmentInfo> remoteDepts)
    {
        var diffs = new List<String>();
        if (!remoteDepts.TryGetValue(externalId, out var remote)) return diffs;

        var name = dept.Name?.Trim();
        if (!remote.Name.IsNullOrEmpty() && !remote.Name.Trim().EqualIgnoreCase(name))
            diffs.Add($"名称 {remote.Name}→{name}");

        if (dept.ParentID != 0 && departmentExternals.TryGetValue(dept.ParentID, out var parentExternal))
        {
            if (parentExternal.StartsWith(PlannedPrefix, StringComparison.Ordinal))
                diffs.Add("父级（待创建）");
            else if (!remote.ParentId.EqualIgnoreCase(parentExternal))
                diffs.Add("父级");
        }

        return diffs;
    }

    private static List<String> GetUserDiffs(User user, UserInfo remote, IDictionary<Int32, String> departmentExternals)
    {
        var diffs = new List<String>();

        var name = user.DisplayName.IsNullOrEmpty() ? user.Name?.Trim() : user.DisplayName.Trim();
        if (!remote.Name.IsNullOrEmpty() && remote.Name.Trim() != name)
            diffs.Add($"显示名 {remote.Name}→{name}");

        if (!user.Mobile.IsNullOrEmpty() && remote.Mobile != user.Mobile)
            diffs.Add($"手机 {remote.Mobile}→{user.Mobile}");

        if (user.DepartmentID > 0 && departmentExternals.TryGetValue(user.DepartmentID, out var deptExternal))
        {
            if (deptExternal.StartsWith(PlannedPrefix, StringComparison.Ordinal))
                diffs.Add("部门（待创建）");
            else if (remote.Department == null || !remote.Department.Any(e => e.EqualIgnoreCase(deptExternal)))
                diffs.Add("部门");
        }

        return diffs;
    }

    private static HashSet<Int32> FindDuplicateSiblings(IList<Department> depts)
    {
        var duplicates = new HashSet<Int32>();
        var index = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
        foreach (var dept in depts)
        {
            if (dept.Name.IsNullOrEmpty()) continue;

            var key = dept.ParentID + "|" + dept.Name.Trim();
            if (index.TryGetValue(key, out var other))
            {
                duplicates.Add(other);
                duplicates.Add(dept.ID);
            }
            else
            {
                index[key] = dept.ID;
            }
        }
        return duplicates;
    }

    private static Boolean HasRemoteSibling(IDictionary<String, DepartmentInfo> remoteDepts, String parentExternal, String name)
    {
        foreach (var remote in remoteDepts.Values)
        {
            if (remote == null || remote.Name.IsNullOrEmpty()) continue;
            if (remote.ParentId.EqualIgnoreCase(parentExternal) && remote.Name.Trim().EqualIgnoreCase(name)) return true;
        }
        return false;
    }

    private static Int32 GetDepth(Department dept, IDictionary<Int32, Department> byId)
    {
        var depth = 0;
        var guard = new HashSet<Int32>();
        var cur = dept;
        while (cur != null && guard.Add(cur.ID) && cur.ParentID != 0 && byId.TryGetValue(cur.ParentID, out var parent))
        {
            depth++;
            cur = parent;
        }
        return depth;
    }
}
