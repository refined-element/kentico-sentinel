using CMS;
using CMS.Base;
using CMS.Core;
using CMS.DataEngine;

using Microsoft.Extensions.DependencyInjection;

[assembly: RegisterModule(typeof(RefinedElement.Kentico.Sentinel.XbyK.SentinelModule))]

namespace RefinedElement.Kentico.Sentinel.XbyK;

/// <summary>
/// Entry point Kentico discovers at startup. Hooks <see cref="ApplicationEvents.Initialized"/>
/// and asks the installer to upsert the module's resource + data-class rows. The installer is
/// idempotent, so repeat runs are no-ops once the tables exist.
/// </summary>
internal sealed class SentinelModule : Module
{
    private SentinelModuleInstaller installer = null!;

    public SentinelModule() : base(nameof(SentinelModule)) { }

    protected override void OnInit(ModuleInitParameters parameters)
    {
        base.OnInit(parameters);

        installer = parameters.Services.GetRequiredService<SentinelModuleInstaller>();
        ApplicationEvents.Initialized.Execute += OnInitialized;
    }

    private void OnInitialized(object? sender, EventArgs e) => installer.Install();
}
