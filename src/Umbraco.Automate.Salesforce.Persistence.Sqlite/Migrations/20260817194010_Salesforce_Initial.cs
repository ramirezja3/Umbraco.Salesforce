using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Salesforce.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Salesforce_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateSalesforcePollingState",
                columns: table => new
                {
                    AutomationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastPollUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: true),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateSalesforcePollingState", x => x.AutomationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateSalesforcePollingState");
        }
    }
}
