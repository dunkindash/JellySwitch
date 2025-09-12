using System.Collections.Generic;
using Jellyfin.Plugin;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;

namespace TechBrew.UserSwitcher;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths appPaths) : base(appPaths)
    {
        Instance = this;
    }

    public override string Name => "User Switcher";
    public override string Description => "Admin-only user impersonation via Quick Connect and QC code authorization.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var asm = GetType().Assembly.GetName().Name;

        yield return new PluginPageInfo
        {
            Name = "userswitcher",
            EmbeddedResourcePath = $"{asm}.web.config.html"
        };
        yield return new PluginPageInfo
        {
            Name = "userswitcher.js",
            EmbeddedResourcePath = $"{asm}.web.userswitcher.js"
        };
    }
}
