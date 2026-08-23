using System;
using System.Collections.Generic;

namespace FormGen.Domain.Entities
{
    public class Form
    {
        public int FormId { get; set; }
        public string FormCode { get; set; } = null!;
        public string FormName { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = "Draft";
        public int? CurrentVersionId { get; set; }
        public FormVersion? CurrentVersion { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }

        public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
        public ICollection<FormSubmission> Submissions { get; set; } = new List<FormSubmission>();
    }

    public class FormVersion
    {
        public int FormVersionId { get; set; }
        public int FormId { get; set; }
        public Form Form { get; set; } = null!;
        public int VersionNo { get; set; }
        public string? VersionName { get; set; }
        public string Status { get; set; } = "Draft";

        /// <summary>Full canvas snapshot: controls + layout tree, as designed in the builder.</summary>
        public string FormDefinitionJson { get; set; } = "{}";

        /// <summary>Layout-only snapshot (sections/rows/columns), used by the Layout Designer screen.</summary>
        public string? LayoutDefinitionJson { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedDate { get; set; }

        public ICollection<FormControl> Controls { get; set; } = new List<FormControl>();
        public ICollection<FormLayout> Layouts { get; set; } = new List<FormLayout>();
        public ICollection<FormRule> Rules { get; set; } = new List<FormRule>();
    }
}
