using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Cube.QyWeixin;
using Xunit;

namespace XUnitTest;

/// <summary>确认 Web SDK 宿主直接引用本库时，回调控制器经 ApplicationPartAttribute 进入路由。</summary>
public class QyWeixinCallbackDiscoveryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QyWeixinCallbackDiscoveryTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    [DisplayName("Web宿主直接引用本库_回调控制器进入路由")]
    public void Host_ReferencesLibrary_DiscoversCallbackController()
    {
        var parts = typeof(Program).Assembly.GetCustomAttributes<ApplicationPartAttribute>();
        Assert.Contains(parts, part => part.AssemblyName == typeof(QyWeixinCallbackController).Assembly.GetName().Name);

        using var client = _factory.CreateClient();
        var provider = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        var actions = provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Where(d => d.ControllerTypeInfo == typeof(QyWeixinCallbackController).GetTypeInfo())
            .ToList();

        Assert.Equal(2, actions.Count);
        Assert.All(actions, d => Assert.Equal("/QyWeixin/Callback", d.AttributeRouteInfo?.Template));
        Assert.Contains(actions, d => d.ActionName == "Verify");
        Assert.Contains(actions, d => d.ActionName == "Event");
    }
}
