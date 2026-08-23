using System;
using System.Collections.Generic;

namespace FormGen.Domain.Entities
{
    public class FormDataSource
    {
        public int DataSourceId { get; set; }
        public string Name { get; set; } = null!;
        public string SourceType { get; set; } = "Static";  // Static | Api | Sql
        public string? ConfigJson { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public ICollection<FormDataSourceItem> Items { get; set; } = new List<FormDataSourceItem>();
    }

    public class FormDataSourceItem
    {
        public int DataSourceItemId { get; set; }
        public int DataSourceId { get; set; }
        public FormDataSource DataSource { get; set; } = null!;
        public string ItemValue { get; set; } = null!;
        public string ItemText { get; set; } = null!;
        public int DisplayOrder { get; set; }
    }
}
