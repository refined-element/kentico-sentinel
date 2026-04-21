using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;

[assembly: UIApplication(
    identifier: RefinedElement.Kentico.Sentinel.XbyK.Admin.SentinelApplicationPage.IDENTIFIER,
    type: typeof(RefinedElement.Kentico.Sentinel.XbyK.Admin.SentinelApplicationPage),
    slug: "sentinel",
    name: "Sentinel",
    category: BaseApplicationCategories.CONFIGURATION,
    icon: Icons.Bug,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace RefinedElement.Kentico.Sentinel.XbyK.Admin;

/// <summary>
/// Top-level Sentinel entry in the Kentico admin left-nav. <see cref="ApplicationPage"/> is a
/// stub — Kentico auto-redirects the admin to the first registered sub-page
/// (<see cref="UIPages.ScanHistoryListingPage"/>), so this class has no body or override surface.
/// The attribute above registers the application with the admin shell; the category
/// (<see cref="BaseApplicationCategories.CONFIGURATION"/>) places it alongside other ops tools
/// like Scheduled tasks and Event log — where admins already look when something is wrong.
/// </summary>
[UIPermission(SystemPermissions.VIEW)]
public class SentinelApplicationPage : ApplicationPage
{
    public const string IDENTIFIER = "RefinedElement.Kentico.Sentinel.Admin";
}
