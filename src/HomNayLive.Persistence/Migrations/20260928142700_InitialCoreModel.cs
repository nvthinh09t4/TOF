using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomNayLive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PublicationAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingJobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    FinishAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DiagnosticsReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentityReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingJobAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessingJobId = table.Column<long>(type: "bigint", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: true),
                    DiagnosticsReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingJobAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingJobAttempts_ProcessingJobs_ProcessingJobId",
                        column: x => x.ProcessingJobId,
                        principalTable: "ProcessingJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArticleSourceLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DiscoveredAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleSourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleSourceLinks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArticleSourceLinks_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSummaries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScopeTopicId = table.Column<long>(type: "bigint", nullable: true),
                    ScopeArticleId = table.Column<long>(type: "bigint", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSummaries", x => x.Id);
                    table.CheckConstraint("CK_AiSummaries_ScopeXor", "(ScopeTopicId IS NULL AND ScopeArticleId IS NOT NULL) OR (ScopeTopicId IS NOT NULL AND ScopeArticleId IS NULL)");
                    table.ForeignKey(
                        name: "FK_AiSummaries_Articles_ScopeArticleId",
                        column: x => x.ScopeArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiSummaries_Topics_ScopeTopicId",
                        column: x => x.ScopeTopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicAliases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicAliases_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicArticleLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<long>(type: "bigint", nullable: false),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicArticleLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicArticleLinks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TopicArticleLinks_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TopicTrendObservations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<long>(type: "bigint", nullable: false),
                    ProviderKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WindowType = table.Column<int>(type: "int", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(9,4)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicTrendObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicTrendObservations_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSummarySourceLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AiSummaryId = table.Column<long>(type: "bigint", nullable: false),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSummarySourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiSummarySourceLinks_AiSummaries_AiSummaryId",
                        column: x => x.AiSummaryId,
                        principalTable: "AiSummaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiSummarySourceLinks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSummaries_ScopeArticleId",
                table: "AiSummaries",
                column: "ScopeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSummaries_ScopeTopicId",
                table: "AiSummaries",
                column: "ScopeTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSummarySourceLinks_ArticleId",
                table: "AiSummarySourceLinks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "UX_AiSummarySourceLinks_AiSummaryId_ArticleId",
                table: "AiSummarySourceLinks",
                columns: new[] { "AiSummaryId", "ArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ContentHash",
                table: "Articles",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "UX_Articles_CanonicalUrl",
                table: "Articles",
                column: "CanonicalUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleSourceLinks_ArticleId",
                table: "ArticleSourceLinks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleSourceLinks_SourceId",
                table: "ArticleSourceLinks",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "UX_ArticleSourceLinks_ArticleId_SourceId",
                table: "ArticleSourceLinks",
                columns: new[] { "ArticleId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProcessingJobAttempts_ProcessingJobId_AttemptNumber",
                table: "ProcessingJobAttempts",
                columns: new[] { "ProcessingJobId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingJobs_JobType_State_CreatedAtUtc",
                table: "ProcessingJobs",
                columns: new[] { "JobType", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Sources_SourceKey",
                table: "Sources",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicAliases_TopicId",
                table: "TopicAliases",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "UX_TopicAliases_NormalizedValue",
                table: "TopicAliases",
                column: "NormalizedValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicArticleLinks_ArticleId",
                table: "TopicArticleLinks",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "UX_TopicArticleLinks_TopicId_ArticleId",
                table: "TopicArticleLinks",
                columns: new[] { "TopicId", "ArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_FirstSeenAtUtc",
                table: "Topics",
                column: "FirstSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_Topics_NormalizedName",
                table: "Topics",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrendObservations_ProviderKind_ProviderKey_ObservedAtUtc",
                table: "TopicTrendObservations",
                columns: new[] { "ProviderKind", "ProviderKey", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicTrendObservations_TopicId_WindowStartUtc",
                table: "TopicTrendObservations",
                columns: new[] { "TopicId", "WindowStartUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TopicTrendObservations_WindowInstance",
                table: "TopicTrendObservations",
                columns: new[] { "TopicId", "ProviderKind", "ProviderKey", "WindowType", "WindowStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_IdentityReference",
                table: "Users",
                column: "IdentityReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSummarySourceLinks");

            migrationBuilder.DropTable(
                name: "ArticleSourceLinks");

            migrationBuilder.DropTable(
                name: "ProcessingJobAttempts");

            migrationBuilder.DropTable(
                name: "TopicAliases");

            migrationBuilder.DropTable(
                name: "TopicArticleLinks");

            migrationBuilder.DropTable(
                name: "TopicTrendObservations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AiSummaries");

            migrationBuilder.DropTable(
                name: "Sources");

            migrationBuilder.DropTable(
                name: "ProcessingJobs");

            migrationBuilder.DropTable(
                name: "Articles");

            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}
