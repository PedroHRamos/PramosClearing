using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PramosClearing.UserService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordSaltToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordSalt",
                table: "users",
                type: "nvarchar(88)",
                maxLength: 88,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordSalt",
                table: "users");
        }
    }
}
