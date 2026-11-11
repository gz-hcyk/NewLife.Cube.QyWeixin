using System.ComponentModel;
using NewLife;
using XCode;
using XCode.DataAccessLayer;

namespace NewLife.Cube.QyWeixin;

/// <summary>外部组织对象类型</summary>
public enum OrgObjectKinds
{
    /// <summary>部门</summary>
    Department = 1,

    /// <summary>用户</summary>
    User = 2,
}

/// <summary>
/// 组织外部身份映射。把企业微信、飞书、钉钉等外部系统的部门/用户映射到魔方本地部门/用户。
/// 唯一键为（租户, 来源实例, 对象类型, 外部Id），同一本地对象可被多个外部对象映射。
/// </summary>
[Serializable]
[DataObject]
[Description("组织外部身份映射。外部部门/用户与魔方本地部门/用户的对应关系")]
[BindIndex("IU_OrgExternalMap_TenantId_Provider_ObjectType_ExternalId", true, "TenantId,Provider,ObjectType,ExternalId")]
[BindIndex("IX_OrgExternalMap_TenantId_ObjectType_LocalId", false, "TenantId,ObjectType,LocalId")]
[BindTable("OrgExternalMap", Description = "组织外部身份映射。外部部门/用户与魔方本地部门/用户的对应关系", ConnName = "Membership", DbType = DatabaseType.None)]
[ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
public class OrgExternalMap : Entity<OrgExternalMap>
{
    #region 属性
    private Int32 _ID;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("ID", "编号", "")]
    public Int32 ID { get => _ID; set { if (OnPropertyChanging("ID", value)) { _ID = value; OnPropertyChanged("ID"); } } }

    private Int32 _TenantId;
    /// <summary>租户</summary>
    [DisplayName("租户")]
    [Description("租户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TenantId", "租户", "")]
    public Int32 TenantId { get => _TenantId; set { if (OnPropertyChanging("TenantId", value)) { _TenantId = value; OnPropertyChanged("TenantId"); } } }

    private String _Provider;
    /// <summary>来源实例。建议写成 qywx:{corpId}，以便同一厂商多个主体互不干扰</summary>
    [DisplayName("来源实例")]
    [Description("来源实例。建议写成 qywx:{corpId}")]
    [DataObjectField(false, false, false, 50)]
    [BindColumn("Provider", "来源实例", "", Master = true)]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private Int32 _ObjectType;
    /// <summary>对象类型。<see cref="OrgObjectKinds"/></summary>
    [DisplayName("对象类型")]
    [Description("对象类型。1部门 2用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ObjectType", "对象类型", "")]
    public Int32 ObjectType { get => _ObjectType; set { if (OnPropertyChanging("ObjectType", value)) { _ObjectType = value; OnPropertyChanged("ObjectType"); } } }

    private String _ExternalId;
    /// <summary>外部Id。企微部门/成员 Id，飞书、钉钉按字符串原样保存</summary>
    [DisplayName("外部Id")]
    [Description("外部Id。企微部门/成员 Id，飞书、钉钉按字符串原样保存")]
    [DataObjectField(false, false, false, 100)]
    [BindColumn("ExternalId", "外部Id", "")]
    public String ExternalId { get => _ExternalId; set { if (OnPropertyChanging("ExternalId", value)) { _ExternalId = value; OnPropertyChanged("ExternalId"); } } }

    private String _ExternalParentId;
    /// <summary>外部父Id。仅用于排查源内层级</summary>
    [DisplayName("外部父Id")]
    [Description("外部父Id。仅用于排查源内层级")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ExternalParentId", "外部父Id", "")]
    public String ExternalParentId { get => _ExternalParentId; set { if (OnPropertyChanging("ExternalParentId", value)) { _ExternalParentId = value; OnPropertyChanged("ExternalParentId"); } } }

    private String _ExternalName;
    /// <summary>外部名称。最近一次同步看到的名称，便于人工核对</summary>
    [DisplayName("外部名称")]
    [Description("外部名称。最近一次同步看到的名称，便于人工核对")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ExternalName", "外部名称", "")]
    public String ExternalName { get => _ExternalName; set { if (OnPropertyChanging("ExternalName", value)) { _ExternalName = value; OnPropertyChanged("ExternalName"); } } }

    private String _ExternalPath;
    /// <summary>外部全路径。用 / 分隔，便于人工核对</summary>
    [DisplayName("外部全路径")]
    [Description("外部全路径。用 / 分隔，便于人工核对")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ExternalPath", "外部全路径", "")]
    public String ExternalPath { get => _ExternalPath; set { if (OnPropertyChanging("ExternalPath", value)) { _ExternalPath = value; OnPropertyChanged("ExternalPath"); } } }

    private Int32 _LocalId;
    /// <summary>本地编号。部门映射为 Department.ID，用户映射为 User.ID</summary>
    [DisplayName("本地编号")]
    [Description("本地编号。部门映射为 Department.ID，用户映射为 User.ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("LocalId", "本地编号", "")]
    public Int32 LocalId { get => _LocalId; set { if (OnPropertyChanging("LocalId", value)) { _LocalId = value; OnPropertyChanged("LocalId"); } } }

    private DateTime _SyncTime;
    /// <summary>同步时间</summary>
    [DisplayName("同步时间")]
    [Description("同步时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("SyncTime", "同步时间", "")]
    public DateTime SyncTime { get => _SyncTime; set { if (OnPropertyChanging("SyncTime", value)) { _SyncTime = value; OnPropertyChanged("SyncTime"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 查询
    /// <summary>按外部身份查找映射</summary>
    /// <param name="provider">来源实例</param>
    /// <param name="kind">对象类型</param>
    /// <param name="externalId">外部Id</param>
    /// <param name="tenantId">租户</param>
    /// <returns>映射，未找到返回 null</returns>
    public static OrgExternalMap FindByExternal(String provider, OrgObjectKinds kind, String externalId, Int32 tenantId = 0)
    {
        if (provider.IsNullOrEmpty() || externalId.IsNullOrEmpty()) return null;

        var table = Meta.Table;
        return Find(
            (table.FindByName(nameof(TenantId)) == tenantId) &
            (table.FindByName(nameof(Provider)) == provider) &
            (table.FindByName(nameof(ObjectType)) == (Int32)kind) &
            (table.FindByName(nameof(ExternalId)) == externalId));
    }

    /// <summary>查找某来源实例下指定类型的全部映射</summary>
    /// <param name="provider">来源实例</param>
    /// <param name="kind">对象类型</param>
    /// <param name="tenantId">租户</param>
    /// <returns>映射列表</returns>
    public static IList<OrgExternalMap> FindAllByProvider(String provider, OrgObjectKinds kind, Int32 tenantId = 0)
    {
        if (provider.IsNullOrEmpty()) return new List<OrgExternalMap>();

        var table = Meta.Table;
        return FindAll(
            (table.FindByName(nameof(TenantId)) == tenantId) &
            (table.FindByName(nameof(Provider)) == provider) &
            (table.FindByName(nameof(ObjectType)) == (Int32)kind));
    }
    #endregion
}
