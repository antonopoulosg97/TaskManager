using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaskManager.Migrations
{
    /// <inheritdoc />
    public partial class SeedRolesCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Capabilities",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "View projects", "VIEW_PROJECTS" },
                    { 2, "Create projects", "CREATE_PROJECT" },
                    { 3, "Edit projects", "EDIT_PROJECT" },
                    { 4, "Delete projects", "DELETE_PROJECT" },
                    { 5, "Create, edit and delete tasks", "MANAGE_TASKS" },
                    { 6, "View tasks", "VIEW_TASKS" },
                    { 7, "Manage users and roles", "MANAGE_USERS" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Name" },
                values: new object[,]
                {
                    { 1, "Full access", "ADMIN" },
                    { 2, "Manages projects and tasks", "MANAGER" },
                    { 3, "Works on assigned tasks", "EMPLOYEE" }
                });

            migrationBuilder.InsertData(
                table: "RolesCapabilities",
                columns: new[] { "CapabilitiesId", "RolesId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 1, 2 },
                    { 1, 3 },
                    { 2, 1 },
                    { 2, 2 },
                    { 3, 1 },
                    { 3, 2 },
                    { 4, 1 },
                    { 4, 2 },
                    { 5, 1 },
                    { 5, 2 },
                    { 6, 1 },
                    { 6, 2 },
                    { 6, 3 },
                    { 7, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 1, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 1, 3 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 2, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 3, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 3, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 4, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 4, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 5, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 5, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 6, 2 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 6, 3 });

            migrationBuilder.DeleteData(
                table: "RolesCapabilities",
                keyColumns: new[] { "CapabilitiesId", "RolesId" },
                keyValues: new object[] { 7, 1 });

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Capabilities",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
