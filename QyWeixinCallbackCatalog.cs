namespace NewLife.Cube.QyWeixin;

/// <summary>
/// 自建应用「接收消息」里常见的 MsgType / Event / ChangeType。
/// 文档：消息 https://developer.work.weixin.qq.com/document/path/90239 ，
/// 事件 https://developer.work.weixin.qq.com/document/path/90240 。
/// 常量只作对照，解析不依赖穷举；未知类型仍可通过 <see cref="QyWeixinInboundMessage.Get"/> 取节点。
/// </summary>
public static class QyWeixinCallbackCatalog
{
    /// <summary>MsgType 取值</summary>
    public static class MsgType
    {
        /// <summary>文本</summary>
        public const String Text = "text";

        /// <summary>图片</summary>
        public const String Image = "image";

        /// <summary>语音</summary>
        public const String Voice = "voice";

        /// <summary>视频</summary>
        public const String Video = "video";

        /// <summary>位置</summary>
        public const String Location = "location";

        /// <summary>链接</summary>
        public const String Link = "link";

        /// <summary>事件。具体种类看 Event</summary>
        public const String Event = "event";
    }

    /// <summary>MsgType=event 时的 Event 取值</summary>
    public static class Event
    {
        /// <summary>成员关注应用</summary>
        public const String Subscribe = "subscribe";

        /// <summary>成员取消关注</summary>
        public const String Unsubscribe = "unsubscribe";

        /// <summary>进入应用</summary>
        public const String EnterAgent = "enter_agent";

        /// <summary>上报地理位置。注意大小写与文档一致为 LOCATION</summary>
        public const String Location = "LOCATION";

        /// <summary>异步任务完成</summary>
        public const String BatchJobResult = "batch_job_result";

        /// <summary>通讯录变更。细类看 ChangeType</summary>
        public const String ChangeContact = "change_contact";

        /// <summary>菜单点击</summary>
        public const String Click = "click";

        /// <summary>菜单跳转</summary>
        public const String View = "view";

        /// <summary>扫码推事件</summary>
        public const String ScanCodePush = "scancode_push";

        /// <summary>扫码推事件且弹出等待</summary>
        public const String ScanCodeWait = "scancode_waitmsg";

        /// <summary>弹出系统拍照</summary>
        public const String PicSysPhoto = "pic_sysphoto";

        /// <summary>弹出拍照或相册</summary>
        public const String PicPhotoOrAlbum = "pic_photo_or_album";

        /// <summary>弹出企微相册</summary>
        public const String PicWeixin = "pic_weixin";

        /// <summary>弹出地理位置选择</summary>
        public const String LocationSelect = "location_select";

        /// <summary>模板卡片点击</summary>
        public const String TemplateCard = "template_card_event";

        /// <summary>模板卡片右上角菜单</summary>
        public const String TemplateCardMenu = "template_card_menu_event";

        /// <summary>旧任务卡片点击</summary>
        public const String TaskCardClick = "taskcard_click";

        /// <summary>审批状态变化</summary>
        public const String OpenApprovalChange = "open_approval_change";

        /// <summary>应用共享变更</summary>
        public const String ShareAgentChange = "share_agent_change";
    }

    /// <summary>change_contact 的 ChangeType</summary>
    public static class ChangeType
    {
        /// <summary>新增成员</summary>
        public const String CreateUser = "create_user";

        /// <summary>更新成员</summary>
        public const String UpdateUser = "update_user";

        /// <summary>删除成员</summary>
        public const String DeleteUser = "delete_user";

        /// <summary>新增部门</summary>
        public const String CreateParty = "create_party";

        /// <summary>更新部门</summary>
        public const String UpdateParty = "update_party";

        /// <summary>删除部门</summary>
        public const String DeleteParty = "delete_party";

        /// <summary>标签成员变更</summary>
        public const String UpdateTag = "update_tag";
    }
}
