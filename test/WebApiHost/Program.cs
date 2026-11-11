var builder = WebApplication.CreateBuilder(args);

// 与 WebApi 宿主一样只注册控制器。AddCube 不扫描 ControllerBase，发现交给 Web SDK 生成的 ApplicationPartAttribute。
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();

/// <summary>供测试宿主工厂引用的入口类型。</summary>
public partial class Program
{
}
