using CMS.DataEngine;
using CMS.FormEngine;
using CMS.Modules;

using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFinding;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelFindingAck;
using RefinedElement.Kentico.Sentinel.XbyK.InfoModels.SentinelScanRun;

namespace RefinedElement.Kentico.Sentinel.XbyK;

/// <summary>
/// Creates the Sentinel module's resource + data-class rows on every app startup. Each
/// <c>Install*</c> method is idempotent — <c>Get ?? New</c> + <c>if (HasChanged) Set</c> — so
/// running on every cold start is a no-op once the tables exist.
/// </summary>
public class SentinelModuleInstaller(IInfoProvider<ResourceInfo> resourceProvider)
{
    private const string ResourceName = "RefinedElement.Sentinel";

    private readonly IInfoProvider<ResourceInfo> resourceProvider = resourceProvider;

    public void Install()
    {
        var resource = InitializeResource(resourceProvider.Get(ResourceName) ?? new ResourceInfo());
        InstallScanRun(resource);
        InstallFinding(resource);
        InstallFindingAck(resource);
    }

    private ResourceInfo InitializeResource(ResourceInfo resource)
    {
        resource.ResourceDisplayName = "Refined Element - Sentinel";
        // "RefinedElement." prefix (as opposed to "CMS.") keeps Kentico from generating C# code
        // for these classes — we own the Info types in the package, not the app.
        resource.ResourceName = ResourceName;
        resource.ResourceDescription = "Kentico Sentinel scan history, findings, and acknowledgments.";
        resource.ResourceIsInDevelopment = false;
        if (resource.HasChanged)
        {
            resourceProvider.Set(resource);
        }
        return resource;
    }

    private static void InstallScanRun(ResourceInfo resource)
    {
        var info = DataClassInfoProvider.GetDataClassInfo(SentinelScanRunInfo.OBJECT_TYPE)
            ?? DataClassInfo.New(SentinelScanRunInfo.OBJECT_TYPE);
        info.ClassName = SentinelScanRunInfo.TYPEINFO.ObjectClassName;
        info.ClassTableName = SentinelScanRunInfo.TYPEINFO.ObjectClassName.Replace(".", "_");
        info.ClassDisplayName = "Sentinel scan run";
        info.ClassType = ClassType.OTHER;
        info.ClassResourceID = resource.ResourceID;

        var form = FormHelper.GetBasicFormDefinition(nameof(SentinelScanRunInfo.SentinelScanRunID));
        form.AddFormItem(TextField(nameof(SentinelScanRunInfo.SentinelScanRunGuid), "guid", allowEmpty: false));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunStartedAt), "datetime", allowEmpty: false));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunCompletedAt), "datetime", allowEmpty: true));
        form.AddFormItem(TextField(nameof(SentinelScanRunInfo.SentinelScanRunTrigger), "text", size: 32));
        form.AddFormItem(TextField(nameof(SentinelScanRunInfo.SentinelScanRunSentinelVersion), "text", size: 64));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunTotalFindings), "integer"));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunErrorCount), "integer"));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunWarningCount), "integer"));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunInfoCount), "integer"));
        form.AddFormItem(Field(nameof(SentinelScanRunInfo.SentinelScanRunDurationSeconds), "decimal", precision: 10, scale: 3));
        form.AddFormItem(TextField(nameof(SentinelScanRunInfo.SentinelScanRunStatus), "text", size: 32));
        form.AddFormItem(TextField(nameof(SentinelScanRunInfo.SentinelScanRunErrorMessage), "longtext", allowEmpty: true));

        SetFormDefinition(info, form);
        if (info.HasChanged) DataClassInfoProvider.SetDataClassInfo(info);
    }

    private static void InstallFinding(ResourceInfo resource)
    {
        var info = DataClassInfoProvider.GetDataClassInfo(SentinelFindingInfo.OBJECT_TYPE)
            ?? DataClassInfo.New(SentinelFindingInfo.OBJECT_TYPE);
        info.ClassName = SentinelFindingInfo.TYPEINFO.ObjectClassName;
        info.ClassTableName = SentinelFindingInfo.TYPEINFO.ObjectClassName.Replace(".", "_");
        info.ClassDisplayName = "Sentinel finding";
        info.ClassType = ClassType.OTHER;
        info.ClassResourceID = resource.ResourceID;

        var form = FormHelper.GetBasicFormDefinition(nameof(SentinelFindingInfo.SentinelFindingID));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingGuid), "guid", allowEmpty: false));
        form.AddFormItem(new FormFieldInfo
        {
            Name = nameof(SentinelFindingInfo.SentinelFindingScanRunID),
            AllowEmpty = false,
            Visible = true,
            Enabled = true,
            DataType = "integer",
            ReferenceToObjectType = SentinelScanRunInfo.OBJECT_TYPE,
            ReferenceType = ObjectDependencyEnum.Required,
        });
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingRuleID), "text", size: 32));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingRuleTitle), "text", size: 200));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingCategory), "text", size: 100));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingSeverity), "text", size: 16));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingMessage), "longtext", allowEmpty: true));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingLocation), "longtext", allowEmpty: true));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingRemediation), "longtext", allowEmpty: true));
        form.AddFormItem(Field(nameof(SentinelFindingInfo.SentinelFindingQuoteEligible), "boolean"));
        form.AddFormItem(TextField(nameof(SentinelFindingInfo.SentinelFindingFingerprintHash), "text", size: 64));

        SetFormDefinition(info, form);
        if (info.HasChanged) DataClassInfoProvider.SetDataClassInfo(info);
    }

    private static void InstallFindingAck(ResourceInfo resource)
    {
        var info = DataClassInfoProvider.GetDataClassInfo(SentinelFindingAckInfo.OBJECT_TYPE)
            ?? DataClassInfo.New(SentinelFindingAckInfo.OBJECT_TYPE);
        info.ClassName = SentinelFindingAckInfo.TYPEINFO.ObjectClassName;
        info.ClassTableName = SentinelFindingAckInfo.TYPEINFO.ObjectClassName.Replace(".", "_");
        info.ClassDisplayName = "Sentinel finding acknowledgment";
        info.ClassType = ClassType.OTHER;
        info.ClassResourceID = resource.ResourceID;

        var form = FormHelper.GetBasicFormDefinition(nameof(SentinelFindingAckInfo.SentinelFindingAckID));
        form.AddFormItem(TextField(nameof(SentinelFindingAckInfo.SentinelFindingAckGuid), "guid", allowEmpty: false));
        form.AddFormItem(TextField(nameof(SentinelFindingAckInfo.SentinelFindingAckFingerprintHash), "text", size: 64));
        form.AddFormItem(TextField(nameof(SentinelFindingAckInfo.SentinelFindingAckState), "text", size: 16));
        form.AddFormItem(Field(nameof(SentinelFindingAckInfo.SentinelFindingAckSnoozeUntil), "datetime", allowEmpty: true));
        form.AddFormItem(Field(nameof(SentinelFindingAckInfo.SentinelFindingAckUserID), "integer"));
        form.AddFormItem(Field(nameof(SentinelFindingAckInfo.SentinelFindingAckAckedAt), "datetime"));
        form.AddFormItem(TextField(nameof(SentinelFindingAckInfo.SentinelFindingAckNote), "longtext", allowEmpty: true));

        SetFormDefinition(info, form);
        if (info.HasChanged) DataClassInfoProvider.SetDataClassInfo(info);
    }

    private static FormFieldInfo Field(string name, string dataType, bool allowEmpty = false, int precision = 0, int scale = 0) => new()
    {
        Name = name,
        DataType = dataType,
        AllowEmpty = allowEmpty,
        Visible = true,
        Enabled = true,
        // Kentico's FormFieldInfo -> SQL DDL mapping for decimals is counterintuitive:
        //   FormFieldInfo.Size      -> SQL precision (total digit count)
        //   FormFieldInfo.Precision -> SQL scale     (digits after decimal)
        // Previously swapped, which produced ALTER TABLE … decimal(3,10) and SQL rejected
        // the install transaction ("scale must be within 0..precision"). No tables got
        // created on v0.2.0 / v0.2.1 despite the module loading.
        Size = precision,
        Precision = scale,
    };

    private static FormFieldInfo TextField(string name, string dataType, int size = 0, bool allowEmpty = false) => new()
    {
        Name = name,
        DataType = dataType,
        AllowEmpty = allowEmpty,
        Visible = true,
        Enabled = true,
        Size = size,
    };

    private static void SetFormDefinition(DataClassInfo info, FormInfo form)
    {
        if (info.ClassID > 0)
        {
            var existing = new FormInfo(info.ClassFormDefinition);
            existing.CombineWithForm(form, new());
            info.ClassFormDefinition = existing.GetXmlDefinition();
        }
        else
        {
            info.ClassFormDefinition = form.GetXmlDefinition();
        }
    }
}
