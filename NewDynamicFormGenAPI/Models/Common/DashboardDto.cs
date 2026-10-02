namespace NewDynamicFormGenAPI.Models.Common;

/// <summary>
/// Data transfer object for the dashboard that provides a summary of form statistics.
/// </summary>
/// <remarks>
/// <para>
/// DashboardDto aggregates key metrics about forms in the system, enabling quick overview
/// of form creation and publication status. This is typically used by dashboard or analytics screens.
/// </para>
/// <para>
/// The counts represent forms matching the current query context (e.g., filtered by date range, user, or status).
/// </para>
/// </remarks>
public class DashboardDto
{
    /// <summary>
    /// Gets or sets the total count of forms matching the query criteria.
    /// </summary>
    /// <remarks>
    /// This typically represents the count of active, non-archived forms.
    /// </remarks>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the count of forms currently in Draft status.
    /// </summary>
    /// <remarks>
    /// Draft forms are works-in-progress that have not yet been published for end-user use.
    /// </remarks>
    public int DraftCount { get; set; }

    /// <summary>
    /// Gets or sets the count of forms currently in Published status.
    /// </summary>
    /// <remarks>
    /// Published forms are active and available for end-users to fill out.
    /// </remarks>
    public int PublishedCount { get; set; }
}
