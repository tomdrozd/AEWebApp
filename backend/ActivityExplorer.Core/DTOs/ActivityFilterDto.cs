using System;
using System.Collections.Generic;

namespace ActivityExplorer.Core.DTOs
{
    public class ActivityFilterDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string>? Workloads { get; set; }
        public List<string>? Operations { get; set; }
        public string? UserSearch { get; set; }
        public string? ResultStatus { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}