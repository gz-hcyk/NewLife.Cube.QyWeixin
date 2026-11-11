using NewLife;

namespace NewLife.Cube.QyWeixin;

/// <summary>临时素材类型。对应 media/upload 的 type</summary>
public static class QyWeixinMediaType
{
    /// <summary>图片，JPG/PNG，最大 10MB</summary>
    public const String Image = "image";

    /// <summary>语音，AMR，最大 2MB，不超过 60 秒</summary>
    public const String Voice = "voice";

    /// <summary>视频，MP4，最大 10MB</summary>
    public const String Video = "video";

    /// <summary>普通文件，最大 20MB</summary>
    public const String File = "file";
}

/// <summary>素材大小限制，单位字节。与文档一致，便于发送前本地拒绝</summary>
public static class QyWeixinMediaLimits
{
    /// <summary>所有文件必须大于 5 字节</summary>
    public const Int32 MinBytes = 5;

    /// <summary>图片上限 10MB</summary>
    public const Int32 ImageMax = 10 * 1024 * 1024;

    /// <summary>语音上限 2MB</summary>
    public const Int32 VoiceMax = 2 * 1024 * 1024;

    /// <summary>视频上限 10MB</summary>
    public const Int32 VideoMax = 10 * 1024 * 1024;

    /// <summary>普通文件上限 20MB</summary>
    public const Int32 FileMax = 20 * 1024 * 1024;

    /// <summary>上传图片（永久 URL）上限 2MB</summary>
    public const Int32 UploadImageMax = 2 * 1024 * 1024;

    /// <summary>按类型检查长度。不满足时抛 ArgumentException</summary>
    /// <param name="type">image/voice/video/file</param>
    /// <param name="length">字节数</param>
    public static void Ensure(String type, Int32 length)
    {
        if (length < MinBytes) throw new ArgumentException($"素材必须大于 {MinBytes} 字节", nameof(length));
        var max = type switch
        {
            QyWeixinMediaType.Image => ImageMax,
            QyWeixinMediaType.Voice => VoiceMax,
            QyWeixinMediaType.Video => VideoMax,
            QyWeixinMediaType.File => FileMax,
            _ => throw new ArgumentException("type 仅支持 image/voice/video/file", nameof(type)),
        };
        if (length > max) throw new ArgumentException($"{type} 超过大小限制 {max} 字节", nameof(length));
    }
}

/// <summary>media/upload 或 media/uploadimg 的结果</summary>
public class QyWeixinMediaUpload
{
    /// <summary>媒体类型。uploadimg 无此字段</summary>
    public String Type { get; set; }

    /// <summary>临时素材 id，3 天内有效，同一企业内应用可共享</summary>
    public String MediaId { get; set; }

    /// <summary>上传时间，Unix 秒</summary>
    public Int64 CreatedAt { get; set; }

    /// <summary>uploadimg 返回的永久图片 URL</summary>
    public String Url { get; set; }
}

/// <summary>media/get 下载到的文件</summary>
public class QyWeixinMediaFile
{
    /// <summary>文件字节</summary>
    public Byte[] Content { get; set; }

    /// <summary>Content-Disposition 中的文件名，可能为空</summary>
    public String FileName { get; set; }

    /// <summary>Content-Type</summary>
    public String ContentType { get; set; }
}

public partial class QyWeixinClient
{
    /// <summary>
    /// 上传临时素材。文档 https://developer.work.weixin.qq.com/document/path/90253
    /// media_id 三天有效。
    /// </summary>
    /// <param name="type">image、voice、video、file</param>
    /// <param name="data">文件内容，大于 5 字节且不超过该类型上限</param>
    /// <param name="fileName">展示文件名</param>
    /// <param name="contentType">内容类型，空则 application/octet-stream</param>
    /// <returns>media_id 与创建时间</returns>
    public async Task<QyWeixinMediaUpload> UploadMediaAsync(String type, Byte[] data, String fileName, String contentType = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        QyWeixinMediaLimits.Ensure(type, data.Length);
        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostMultipartAsync($"media/upload?access_token={token}&type={type}", data, fileName, contentType).ConfigureAwait(false);
        return new QyWeixinMediaUpload
        {
            Type = QyWeixinValues.Pick(raw, "type") ?? type,
            MediaId = QyWeixinValues.Pick(raw, "media_id"),
            CreatedAt = QyWeixinValues.PickLong(raw, "created_at"),
        };
    }

    /// <summary>
    /// 上传图片并得到永久 URL，用于图文正文。文档 https://developer.work.weixin.qq.com/document/path/90256
    /// 不返回 media_id。
    /// </summary>
    /// <param name="data">JPG/PNG，5 字节到 2MB</param>
    /// <param name="fileName">文件名</param>
    /// <param name="contentType">内容类型，默认 image/jpeg</param>
    /// <returns>图片 URL</returns>
    public async Task<QyWeixinMediaUpload> UploadImageAsync(Byte[] data, String fileName, String contentType = "image/jpeg")
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length < QyWeixinMediaLimits.MinBytes || data.Length > QyWeixinMediaLimits.UploadImageMax)
            throw new ArgumentException($"图片需在 {QyWeixinMediaLimits.MinBytes} 字节到 {QyWeixinMediaLimits.UploadImageMax} 字节之间", nameof(data));

        var token = await TokenAsync().ConfigureAwait(false);
        var raw = await PostMultipartAsync($"media/uploadimg?access_token={token}", data, fileName, contentType).ConfigureAwait(false);
        return new QyWeixinMediaUpload { Url = QyWeixinValues.Pick(raw, "url") };
    }

    /// <summary>
    /// 获取临时素材。文档 https://developer.work.weixin.qq.com/document/path/90254
    /// 视频不支持此接口下载。
    /// </summary>
    /// <param name="mediaId">media_id</param>
    /// <returns>文件字节与文件名</returns>
    public async Task<QyWeixinMediaFile> GetMediaAsync(String mediaId)
    {
        if (mediaId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(mediaId));
        var token = await TokenAsync().ConfigureAwait(false);
        return await DownloadAsync($"media/get?access_token={token}&media_id={Uri.EscapeDataString(mediaId)}").ConfigureAwait(false);
    }
}
