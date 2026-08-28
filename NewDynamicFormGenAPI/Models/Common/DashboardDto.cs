namespace NewDynamicFormGenAPI.Models.Common;

public class DashboardDto
{
    // Total number of forms matching the query (active)
    public int TotalCount { get; set; }

    // Number of forms in Draft status
    public int DraftCount { get; set; }

    // Number of forms in Published status
    public int PublishedCount { get; set; }

}
