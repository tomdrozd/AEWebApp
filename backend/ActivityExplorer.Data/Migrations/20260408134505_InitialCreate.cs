using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityExplorer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UserPrincipalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Workload = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ResultStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientIP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObjectId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetUser = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordIdentity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActivityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Application = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataPlatform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceLocationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserSku = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitivityLabel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HowApplied = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HowAppliedDetail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LabelEventType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProtectionEventType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PolicyMatchInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitiveInfoTypeData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitiveInfoTypeBucketsData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SensitivityLabelIdsReferenced = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionalDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedFilters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Operation",
                table: "Activities",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Timestamp",
                table: "Activities",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId",
                table: "Activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Workload",
                table: "Activities",
                column: "Workload");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_UserId",
                table: "SavedFilters",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "SavedFilters");
        }
    }
}
