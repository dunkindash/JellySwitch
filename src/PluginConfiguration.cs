using MediaBrowser.Model.Plugins;

namespace TechBrew.UserSwitcher;

public class PluginConfiguration : BasePluginConfiguration
{
    public int ImpersonationMinutes { get; set; } = 15;
    public bool WatermarkImpersonation { get; set; } = true;
}
