using NewLife;
using NewLife.Cube.Web.Models;
using XCode.Membership;

namespace NewLife.Cube.QyWeixin;

/// <summary>外部部门节点。带有从源根到当前部门的完整名称路径</summary>
public class QyWeixinDepartmentNode
{
    /// <summary>原始部门信息</summary>
    public DepartmentInfo Info { get; set; }

    /// <summary>完整路径。名称以 / 连接，包含可识别的源根部门</summary>
    public String Path { get; set; }

    /// <summary>源根部门名称。没有可识别根部门时为空</summary>
    public String RootName { get; set; }

    /// <summary>层级。顶层为 0，用于保证父级先处理</summary>
    public Int32 Depth { get; set; }

    /// <summary>是否源根部门</summary>
    public Boolean IsRoot { get; set; }
}

/// <summary>
/// 多源组织匹配规则。本地部门是主数据，外部部门只按全路径名称挂接，
/// 因此这里的逻辑必须与数据库无关，便于单独验证。
/// </summary>
public static class QyWeixinOrgMatch
{
    /// <summary>为全部外部部门构建节点与名称路径</summary>
    /// <param name="remote">外部部门列表</param>
    /// <returns>以外部部门 Id 为键的节点字典</returns>
    public static IDictionary<String, QyWeixinDepartmentNode> BuildDepartmentNodes(DepartmentInfo[] remote)
    {
        var byId = new Dictionary<String, DepartmentInfo>(StringComparer.OrdinalIgnoreCase);
        if (remote != null)
        {
            foreach (var item in remote)
            {
                if (item == null || item.Id.IsNullOrEmpty()) continue;
                byId[item.Id] = item;
            }
        }

        var nodes = new Dictionary<String, QyWeixinDepartmentNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in byId.Values)
            nodes[item.Id] = BuildNode(item, byId);

        return nodes;
    }

    /// <summary>
    /// 找出拉取人员所需的顶层部门。企微通常只返回根部门 1；
    /// 没有根记录时可能有多个顶层部门，需要逐个拉取。
    /// </summary>
    /// <param name="remote">外部部门列表</param>
    /// <returns>顶层部门列表</returns>
    public static DepartmentInfo[] FindTopDepartments(DepartmentInfo[] remote)
    {
        if (remote == null || remote.Length == 0) return Array.Empty<DepartmentInfo>();

        var root = remote.FirstOrDefault(IsRoot);
        if (root != null) return new[] { root };

        var ids = new HashSet<String>(remote.Where(e => e != null && !e.Id.IsNullOrEmpty()).Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
        var tops = remote.Where(e => e != null && !e.Id.IsNullOrEmpty() && (e.ParentId.IsNullOrEmpty() || !ids.Contains(e.ParentId))).ToArray();
        return tops.Length > 0 ? tops : new[] { remote[0] };
    }

    /// <summary>生成用于和本地部门路径比对的候选路径。优先完整路径，其次去掉源根名称后的路径</summary>
    /// <param name="node">外部部门节点</param>
    /// <returns>候选路径</returns>
    public static IEnumerable<String> GetMatchPaths(QyWeixinDepartmentNode node)
    {
        if (node == null || node.Path.IsNullOrEmpty()) yield break;

        yield return node.Path;

        // 本地树可能没有“公司”这一层，允许去掉源根名称后再比对
        if (!node.IsRoot && !node.RootName.IsNullOrEmpty())
        {
            var prefix = node.RootName + "/";
            if (node.Path.Length > prefix.Length && node.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return node.Path[prefix.Length..];
        }
    }

    /// <summary>建立本地部门的全路径索引。重复路径不参与自动匹配，避免误挂</summary>
    /// <param name="local">本地部门集合</param>
    /// <param name="tenantId">租户</param>
    /// <returns>路径到本地部门的字典，忽略大小写</returns>
    public static IDictionary<String, Department> BuildLocalDepartmentPaths(IEnumerable<Department> local, Int32 tenantId)
    {
        var list = local == null ? new List<Department>() : local.Where(e => e != null && e.TenantId == tenantId).ToList();
        var byId = list.ToDictionary(e => e.ID);
        var cache = new Dictionary<Int32, String>();

        String GetPath(Department item)
        {
            if (cache.TryGetValue(item.ID, out var path)) return path;

            var names = new List<String>();
            var guard = new HashSet<Int32>();
            var cur = item;
            while (cur != null && guard.Add(cur.ID))
            {
                if (!cur.Name.IsNullOrEmpty()) names.Add(cur.Name.Trim());
                if (cur.ParentID == 0 || !byId.TryGetValue(cur.ParentID, out cur)) break;
            }
            names.Reverse();

            path = String.Join("/", names);
            cache[item.ID] = path;
            return path;
        }

        var map = new Dictionary<String, Department>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list)
        {
            var path = GetPath(item);
            if (path.IsNullOrEmpty()) continue;

            if (map.ContainsKey(path))
                duplicates.Add(path);
            else
                map[path] = item;
        }

        foreach (var path in duplicates)
            map.Remove(path);

        return map;
    }

    /// <summary>是否源根部门。父级为空、0，或父级指向自己，均视为根</summary>
    /// <param name="info">部门信息</param>
    /// <returns>是否根部门</returns>
    public static Boolean IsRoot(DepartmentInfo info)
    {
        if (info == null) return false;

        var parentId = info.ParentId;
        return parentId.IsNullOrEmpty() || parentId == "0" || parentId.EqualIgnoreCase(info.Id);
    }

    private static QyWeixinDepartmentNode BuildNode(DepartmentInfo info, IDictionary<String, DepartmentInfo> byId)
    {
        var names = new List<String>();
        var guard = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        String rootName = null;
        var depth = 0;

        var cur = info;
        while (cur != null && guard.Add(cur.Id))
        {
            if (!cur.Name.IsNullOrEmpty()) names.Add(cur.Name.Trim());

            if (IsRoot(cur))
            {
                rootName = cur.Name;
                break;
            }

            // 父级记录缺失时，把当前部门视作顶层，路径从自己开始
            if (cur.ParentId.IsNullOrEmpty() || !byId.TryGetValue(cur.ParentId, out cur)) break;

            depth++;
        }

        names.Reverse();

        return new QyWeixinDepartmentNode
        {
            Info = info,
            Path = String.Join("/", names),
            RootName = rootName,
            Depth = depth,
            IsRoot = IsRoot(info),
        };
    }
}
