using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetSocial.Migrations
{
    /// <inheritdoc />
    public partial class AddPetMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SenderPetId = table.Column<int>(type: "int", nullable: false),
                    ReceiverPetId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetMatches_Pets_ReceiverPetId",
                        column: x => x.ReceiverPetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PetMatches_Pets_SenderPetId",
                        column: x => x.SenderPetId,
                        principalTable: "Pets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PetMatches_ReceiverPetId",
                table: "PetMatches",
                column: "ReceiverPetId");

            migrationBuilder.CreateIndex(
                name: "IX_PetMatches_SenderPetId",
                table: "PetMatches",
                column: "SenderPetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetMatches");
        }
    }
}
