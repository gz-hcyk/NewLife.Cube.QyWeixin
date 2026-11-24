using System.ComponentModel;
using NewLife.Cube.QyWeixin;
using NewLife.Cube.Web.Models;
using XCode.Membership;
using Xunit;

namespace XUnitTest;

public class QyWeixinPushPlannerTests
{
    [Fact]
    [DisplayName("预演_全新组织_根绑定1其余新建_人员待部门创建")]
    public void Plan_BrandNew_CreatesUnderRoot()
    {
        var depts = new[]
        {
            Dept(10, "本公司", 0),
            Dept(11, "研发部", 10),
        };
        var users = new[]
        {
            Usr(100, "zhangsan", "张三", "13800000000", 11),
        };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, users, null, null, remoteDepts, null);

        var root = FindDept(plan, 10);
        Assert.Equal(QyWeixinPushActions.Bind, root.Action);
        Assert.Equal("1", root.ExternalId);

        var child = FindDept(plan, 11);
        Assert.Equal(QyWeixinPushActions.Create, child.Action);
        Assert.Null(child.ExternalId);

        var user = FindUser(plan, 100);
        Assert.Equal(QyWeixinPushActions.Create, user.Action);
        Assert.Equal("zhangsan", user.ExternalId);
        Assert.Contains("待部门创建", user.Message);
    }

    [Fact]
    [DisplayName("预演_人员匹配_映射优先于同名与手机")]
    public void Plan_UserMapping_Wins()
    {
        var users = new[]
        {
            Usr(100, "zhangsan", "张三", "13800000000", 0),
        };
        var maps = new[] { Map(OrgObjectKinds.User, "wx_zhangsan", 100) };
        var remoteUsers = new[]
        {
            RUser("wx_zhangsan", "张三", "13900000000"),
            RUser("zhangsan", "张三丰", "13800000000"),
        };

        var plan = QyWeixinPushPlanner.Plan(null, users, null, maps, null, remoteUsers);

        var user = FindUser(plan, 100);
        Assert.Equal("wx_zhangsan", user.ExternalId);
        Assert.Equal(QyWeixinPushActions.Update, user.Action);
        Assert.Contains("映射", user.Message);
        Assert.Contains("手机", user.Message);
    }

    [Fact]
    [DisplayName("预演_人员匹配_同名先于手机")]
    public void Plan_UserByName_BeforeMobile()
    {
        var users = new[]
        {
            Usr(100, "zhangsan", "张三", "13800000000", 0),
        };
        var remoteUsers = new[]
        {
            RUser("zhangsan", "张三", "13900000000"),
            RUser("lisi", "李四", "13800000000"),
        };

        var plan = QyWeixinPushPlanner.Plan(null, users, null, null, null, remoteUsers);

        var user = FindUser(plan, 100);
        Assert.Equal("zhangsan", user.ExternalId);
        Assert.Contains("同名", user.Message);
        Assert.DoesNotContain("手机命中", user.Message);
    }

    [Fact]
    [DisplayName("预演_人员匹配_同名未命中再按手机")]
    public void Plan_UserByMobile_WhenNameMissing()
    {
        var users = new[]
        {
            Usr(100, "wangwu", "王五", "13800000000", 0),
        };
        var remoteUsers = new[] { RUser("wx_wangwu", "王五", "13800000000") };

        var plan = QyWeixinPushPlanner.Plan(null, users, null, null, null, remoteUsers);

        var user = FindUser(plan, 100);
        Assert.Equal("wx_wangwu", user.ExternalId);
        Assert.Equal(QyWeixinPushActions.Bind, user.Action);
        Assert.Contains("手机", user.Message);
    }

    [Fact]
    [DisplayName("预演_人员匹配_手机命中多个时报冲突")]
    public void Plan_UserMobileConflict()
    {
        var users = new[]
        {
            Usr(100, "wangwu", "王五", "13800000000", 0),
        };
        var remoteUsers = new[]
        {
            RUser("a", "甲", "13800000000"),
            RUser("b", "乙", "13800000000"),
        };

        var plan = QyWeixinPushPlanner.Plan(null, users, null, null, null, remoteUsers);

        var user = FindUser(plan, 100);
        Assert.Equal(QyWeixinPushActions.Conflict, user.Action);
        Assert.Contains("多个", user.Message);
    }

    [Fact]
    [DisplayName("预演_人员匹配_User.Name非法时冲突")]
    public void Plan_UserInvalidUserId()
    {
        var depts = new[] { Dept(10, "本公司", 0) };
        var users = new[] { Usr(100, "张三", null, null, 10) };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, users, null, null, remoteDepts, null);

        var user = FindUser(plan, 100);
        Assert.Equal(QyWeixinPushActions.Conflict, user.Action);
        Assert.Contains("userid", user.Message);
    }

    [Fact]
    [DisplayName("预演_部门映射失效_按新建处理且人员待部门创建")]
    public void Plan_StaleDepartmentMapping_Creates()
    {
        var depts = new[]
        {
            Dept(10, "本公司", 0),
            Dept(11, "研发部", 10),
        };
        var users = new[] { Usr(100, "zhangsan", "张三", "13800000000", 11) };
        var maps = new[]
        {
            Map(OrgObjectKinds.Department, "1", 10),
            Map(OrgObjectKinds.Department, "99", 11),
        };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, users, maps, null, remoteDepts, null);

        Assert.Equal(QyWeixinPushActions.Skip, FindDept(plan, 10).Action);

        var child = FindDept(plan, 11);
        Assert.Equal(QyWeixinPushActions.Create, child.Action);
        Assert.Contains("映射失效", child.Message);

        var user = FindUser(plan, 100);
        Assert.Equal(QyWeixinPushActions.Create, user.Action);
        Assert.Contains("待部门创建", user.Message);
    }

    [Fact]
    [DisplayName("预演_企微已有同名同级部门但未映射_冲突")]
    public void Plan_RemoteSiblingUnmapped_Conflict()
    {
        var depts = new[]
        {
            Dept(10, "本公司", 0),
            Dept(11, "研发部", 10),
        };
        var remoteDepts = new[]
        {
            RDept("1", "本公司", "1"),
            RDept("2", "研发部", "1"),
        };

        var plan = QyWeixinPushPlanner.Plan(depts, null, null, null, remoteDepts, null);

        var child = FindDept(plan, 11);
        Assert.Equal(QyWeixinPushActions.Conflict, child.Action);
        Assert.Contains("未映射", child.Message);
    }

    [Fact]
    [DisplayName("预演_本地同级重名_冲突")]
    public void Plan_LocalSiblingDuplicate_Conflict()
    {
        var depts = new[]
        {
            Dept(10, "本公司", 0),
            Dept(11, "研发部", 10),
            Dept(12, "研发部", 10),
        };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, null, null, null, remoteDepts, null);

        Assert.Equal(QyWeixinPushActions.Conflict, FindDept(plan, 11).Action);
        Assert.Equal(QyWeixinPushActions.Conflict, FindDept(plan, 12).Action);
    }

    [Fact]
    [DisplayName("预演_多个本地根_首个绑根其余顶层新建")]
    public void Plan_MultipleLocalRoots()
    {
        var depts = new[]
        {
            Dept(10, "甲公司", 0),
            Dept(12, "乙公司", 0),
        };
        var remoteDepts = new[] { RDept("1", "集团", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, null, null, null, remoteDepts, null);

        Assert.Equal(QyWeixinPushActions.Bind, FindDept(plan, 10).Action);
        Assert.Equal("1", FindDept(plan, 10).ExternalId);

        var second = FindDept(plan, 12);
        Assert.Equal(QyWeixinPushActions.Create, second.Action);
        Assert.Contains("顶层", second.Message);
    }

    [Fact]
    [DisplayName("预演_企微成员已绑定其他本地用户_冲突")]
    public void Plan_RemoteUserBoundToOtherLocal_Conflict()
    {
        var users = new[]
        {
            Usr(100, "zhangsan", "张三", "13800000000", 0),
            Usr(200, "other", "其他人", null, 0),
        };
        var maps = new[] { Map(OrgObjectKinds.User, "zhangsan", 200) };
        var remoteUsers = new[] { RUser("zhangsan", "张三", "13800000000") };

        var plan = QyWeixinPushPlanner.Plan(null, users, null, maps, null, remoteUsers);

        var user = FindUser(plan, 100);
        Assert.Equal(QyWeixinPushActions.Conflict, user.Action);
        Assert.Contains("已绑定本地用户", user.Message);
    }

    [Fact]
    [DisplayName("预演_部门名称为空_冲突")]
    public void Plan_EmptyDepartmentName_Conflict()
    {
        var depts = new[] { Dept(10, "", 0) };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, null, null, null, remoteDepts, null);

        var root = FindDept(plan, 10);
        Assert.Equal(QyWeixinPushActions.Conflict, root.Action);
        Assert.Contains("名称为空", root.Message);
    }

    [Fact]
    [DisplayName("预演_企微根1已被其他本地部门映射_冲突")]
    public void Plan_RootAlreadyMappedElsewhere_Conflict()
    {
        var depts = new[] { Dept(10, "本公司", 0) };
        var maps = new[] { Map(OrgObjectKinds.Department, "1", 99) };
        var remoteDepts = new[] { RDept("1", "本公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, null, maps, null, remoteDepts, null);

        var root = FindDept(plan, 10);
        Assert.Equal(QyWeixinPushActions.Conflict, root.Action);
        Assert.Contains("已被其他本地部门映射", root.Message);
    }

    [Fact]
    [DisplayName("预演_已有映射但名称不同_计划更新")]
    public void Plan_MappedDepartmentNameDiff_Update()
    {
        var depts = new[] { Dept(10, "本公司", 0) };
        var maps = new[] { Map(OrgObjectKinds.Department, "1", 10) };
        var remoteDepts = new[] { RDept("1", "旧公司", "1") };

        var plan = QyWeixinPushPlanner.Plan(depts, null, maps, null, remoteDepts, null);

        var root = FindDept(plan, 10);
        Assert.Equal(QyWeixinPushActions.Update, root.Action);
        Assert.Contains("名称", root.Message);
    }

    private static Department Dept(Int32 id, String name, Int32 parentId, Int32 sort = 0) => new()
    {
        ID = id,
        Name = name,
        ParentID = parentId,
        Sort = sort,
        TenantId = 0,
        Enable = true,
    };

    private static User Usr(Int32 id, String name, String displayName, String mobile, Int32 departmentId) => new()
    {
        ID = id,
        Name = name,
        DisplayName = displayName,
        Mobile = mobile,
        DepartmentID = departmentId,
        Enable = true,
    };

    private static OrgExternalMap Map(OrgObjectKinds kind, String externalId, Int32 localId) => new()
    {
        TenantId = 0,
        Provider = "qywx:ww1",
        ObjectType = (Int32)kind,
        ExternalId = externalId,
        LocalId = localId,
    };

    private static DepartmentInfo RDept(String id, String name, String parentId) => new()
    {
        Id = id,
        Name = name,
        ParentId = parentId,
    };

    private static UserInfo RUser(String id, String name, String mobile, params String[] departments) => new()
    {
        Id = id,
        Name = name,
        Mobile = mobile,
        Department = departments,
    };

    private static QyWeixinPushItem FindDept(QyWeixinPushPlan plan, Int32 localId)
        => plan.Departments.Single(e => e.LocalId == localId);

    private static QyWeixinPushItem FindUser(QyWeixinPushPlan plan, Int32 localId)
        => plan.Users.Single(e => e.LocalId == localId);
}
