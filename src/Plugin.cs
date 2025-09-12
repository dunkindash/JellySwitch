using System.Collections.Generic;
using Jellyfin.Plugin;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using System;

namespace TechBrew.UserSwitcher;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }
    public override Guid Id => Guid.Parse("e0a4a1a2-5b6b-4e9d-88b1-1c71f0b9b0ab");

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
