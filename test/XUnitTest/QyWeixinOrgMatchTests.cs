using System.ComponentModel;
using NewLife.Cube.Web.Models;
using NewLife.Cube.QyWeixin;
using XCode.Membership;
using Xunit;

namespace XUnitTest;

public class QyWeixinOrgMatchTests
{
    [Fact]
    [DisplayName("部门路径_包含源根并可生成去根候选")]
    public void BuildNodes_WithRoot()
    {
        var remote = new[]
        {
            new DepartmentInfo { Id = "1", Name = "XX公司", ParentId = "1" },
            new DepartmentInfo { Id = "2", Name = "研发部", ParentId = "1" },
            new DepartmentInfo { Id = "3", Name = "后端组", ParentId = "2" },
        };

        var nodes = QyWeixinOrgMatch.BuildDepartmentNodes(remote);

        Assert.Equal(3, nodes.Count);
        Assert.True(nodes["1"].IsRoot);
        Assert.Equal("XX公司", nodes["1"].Path);
        Assert.Equal("XX公司/研发部", nodes["2"].Path);
        Assert.Equal("XX公司/研发部/后端组", nodes["3"].Path);
        Assert.Equal("XX公司", nodes["3"].RootName);
        Assert.Equal(2, nodes["3"].Depth);

        Assert.Equal(new[] { "XX公司/研发部/后端组", "研发部/后端组" }, QyWeixinOrgMatch.GetMatchPaths(nodes["3"]).ToArray());
        Assert.Equal(new[] { "XX公司" }, QyWeixinOrgMatch.GetMatchPaths(nodes["1"]).ToArray());
    }

    [Fact]
    [DisplayName("顶层部门_根记录缺失时逐个返回")]
    public void FindTopDepartments_WithoutRootRecord()
    {
        var remote = new[]
        {
            new DepartmentInfo { Id = "2", Name = "研发部", ParentId = "1" },
            new DepartmentInfo { Id = "4", Name = "市场部", ParentId = "1" },
        };

        var tops = QyWeixinOrgMatch.FindTopDepartments(remote);

        Assert.Equal(2, tops.Length);
        Assert.Contains(tops, e => e.Id == "2");
        Assert.Contains(tops, e => e.Id == "4");
    }

    [Fact]
    [DisplayName("顶层部门_存在根记录时只返回根")]
    public void FindTopDepartments_WithRoot()
    {
        var remote = new[]
        {
            new DepartmentInfo { Id = "1", Name = "XX公司", ParentId = "0" },
            new DepartmentInfo { Id = "2", Name = "研发部", ParentId = "1" },
        };

        var tops = QyWeixinOrgMatch.FindTopDepartments(remote);

        Assert.Single(tops);
        Assert.Equal("1", tops[0].Id);
    }

    [Fact]
    [DisplayName("本地部门路径_按租户构建并排除重复路径")]
    public void BuildLocalPaths_FiltersTenantAndDuplicates()
    {
        var local = new[]
        {
            new Department { ID = 10, Name = "研发部", ParentID = 0, TenantId = 0 },
            new Department { ID = 11, Name = "后端组", ParentID = 10, TenantId = 0 },
            new Department { ID = 20, Name = "市场部", ParentID = 0, TenantId = 0 },
            new Department { ID = 21, Name = "市场部", ParentID = 0, TenantId = 0 },
            new Department { ID = 30, Name = "研发部", ParentID = 0, TenantId = 1 },
        };

        var map = QyWeixinOrgMatch.BuildLocalDepartmentPaths(local, 0);

        Assert.Equal(10, map["研发部"].ID);
        Assert.Equal(11, map["研发部/后端组"].ID);
        Assert.False(map.ContainsKey("市场部"));
        Assert.Equal(2, map.Count);
    }
}
