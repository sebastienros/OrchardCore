using System.ComponentModel;
using OrchardCore.ContentManagement;

namespace OrchardCore.Contents.AuditTrail.Models;

public class AuditTrailPart : ContentPart
{
    [Description("The audit comment recorded for the content item operation.")]
    public string Comment { get; set; }

    public bool ShowComment { get; set; }
}
