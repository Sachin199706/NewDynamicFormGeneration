
namespace NewDynamicFormGenAPI.Models.Entities;

public class FormPublishHistory
{
    public int PublishHistoryId { get; set; }
    public int FormId { get; set; }
    public int FormVersionId { get; set; }
    public DateTime PublishedOn { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}