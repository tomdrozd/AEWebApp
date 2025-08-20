using System;
using System.Collections.Generic;

namespace ActivityExplorer.Core.Models
{
    public class Activity
    {
        // Primary key
        public Guid Id { get; set; } = Guid.NewGuid();
        
        // Core fields (existing)
        public DateTime Timestamp { get; set; }
        public string? UserId { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? Operation { get; set; }
        public string? Workload { get; set; }
        public string? ResultStatus { get; set; }
        public string? ClientIP { get; set; }
        public string? ObjectId { get; set; }
        public string? TargetUser { get; set; }
        
        // New fields from Export-ActivityExplorerData
        public string? RecordIdentity { get; set; }
        public string? ActivityId { get; set; }
        public string? Application { get; set; }
        public string? ContentType { get; set; }
        public string? DataPlatform { get; set; }
        public string? DeviceName { get; set; }
        public string? FilePath { get; set; }
        public string? ItemName { get; set; }
        public long? FileSize { get; set; }
        public string? Platform { get; set; }
        public string? SourceLocationType { get; set; }
        public string? UserType { get; set; }
        public string? UserSku { get; set; }
        
        // Sensitivity and Protection fields
        public string? SensitivityLabel { get; set; }
        public string? HowApplied { get; set; }
        public string? HowAppliedDetail { get; set; }
        public string? LabelEventType { get; set; }
        public string? ProtectionEventType { get; set; }
        
        // Complex fields stored as JSON strings
        public string? EmailInfo { get; set; } // JSON serialized
        public string? PolicyMatchInfo { get; set; } // JSON serialized
        public string? SensitiveInfoTypeData { get; set; } // JSON serialized
        public string? SensitiveInfoTypeBucketsData { get; set; } // JSON serialized
        public string? SensitivityLabelIdsReferenced { get; set; } // JSON serialized
        public string? AttachmentDetails { get; set; } // JSON serialized
        
        // For any additional/future fields not explicitly mapped
        public Dictionary<string, object>? AdditionalDetails { get; set; }
        
        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}