using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyGamingMap.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    logo_image_id = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "franchises",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_franchises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_engines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    logo_image_id = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_engines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_modes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_modes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_perspectives",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_perspectives", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "themes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_themes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    igdb_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    game_type_id = table.Column<long>(type: "bigint", nullable: true),
                    cover_id = table.Column<string>(type: "text", nullable: true),
                    esrb_rating = table.Column<string>(type: "text", nullable: true),
                    pegi_rating = table.Column<int>(type: "integer", nullable: true),
                    review_rating = table.Column<double>(type: "double precision", nullable: true),
                    review_count = table.Column<int>(type: "integer", nullable: true),
                    storyline = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.igdb_id);
                    table.ForeignKey(
                        name: "fk_games_game_types_game_type_id",
                        column: x => x.game_type_id,
                        principalTable: "game_types",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "collection_game",
                columns: table => new
                {
                    collections_id = table.Column<long>(type: "bigint", nullable: false),
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_game", x => new { x.collections_id, x.games_igdb_id });
                    table.ForeignKey(
                        name: "fk_collection_game_collections_collections_id",
                        column: x => x.collections_id,
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_game_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "franchise_game",
                columns: table => new
                {
                    franchises_id = table.Column<long>(type: "bigint", nullable: false),
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_franchise_game", x => new { x.franchises_id, x.games_igdb_id });
                    table.ForeignKey(
                        name: "fk_franchise_game_franchises_franchises_id",
                        column: x => x.franchises_id,
                        principalTable: "franchises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_franchise_game_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_game_engine",
                columns: table => new
                {
                    game_engines_id = table.Column<long>(type: "bigint", nullable: false),
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_game_engine", x => new { x.game_engines_id, x.games_igdb_id });
                    table.ForeignKey(
                        name: "fk_game_game_engine_game_engines_game_engines_id",
                        column: x => x.game_engines_id,
                        principalTable: "game_engines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_game_engine_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_game_mode",
                columns: table => new
                {
                    game_modes_id = table.Column<long>(type: "bigint", nullable: false),
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_game_mode", x => new { x.game_modes_id, x.games_igdb_id });
                    table.ForeignKey(
                        name: "fk_game_game_mode_game_modes_game_modes_id",
                        column: x => x.game_modes_id,
                        principalTable: "game_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_game_mode_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_genre",
                columns: table => new
                {
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    genres_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_genre", x => new { x.games_igdb_id, x.genres_id });
                    table.ForeignKey(
                        name: "fk_game_genre_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_genre_genres_genres_id",
                        column: x => x.genres_id,
                        principalTable: "genres",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_mappings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    concept_id = table.Column<int>(type: "integer", nullable: true),
                    np_communication_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_mappings_games_igdb_id",
                        column: x => x.igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_player_perspective",
                columns: table => new
                {
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    player_perspectives_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_player_perspective", x => new { x.games_igdb_id, x.player_perspectives_id });
                    table.ForeignKey(
                        name: "fk_game_player_perspective_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_player_perspective_player_perspectives_player_perspect",
                        column: x => x.player_perspectives_id,
                        principalTable: "player_perspectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_theme",
                columns: table => new
                {
                    games_igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    themes_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_theme", x => new { x.games_igdb_id, x.themes_id });
                    table.ForeignKey(
                        name: "fk_game_theme_games_games_igdb_id",
                        column: x => x.games_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_theme_themes_themes_id",
                        column: x => x.themes_id,
                        principalTable: "themes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "involved_companies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<long>(type: "bigint", nullable: false),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    developer = table.Column<bool>(type: "boolean", nullable: false),
                    publisher = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_involved_companies", x => x.id);
                    table.ForeignKey(
                        name: "fk_involved_companies_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_involved_companies_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_dates",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_igdb_id = table.Column<long>(type: "bigint", nullable: false),
                    platform = table.Column<string>(type: "text", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    region_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release_dates", x => x.id);
                    table.ForeignKey(
                        name: "fk_release_dates_games_game_igdb_id",
                        column: x => x.game_igdb_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_release_dates_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "screenshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    image_id = table.Column<string>(type: "text", nullable: false),
                    game_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_screenshots_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "igdb_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_game_games_igdb_id",
                table: "collection_game",
                column: "games_igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_collections_name",
                table: "collections",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies_name",
                table: "companies",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_franchise_game_games_igdb_id",
                table: "franchise_game",
                column: "games_igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_franchises_name",
                table: "franchises",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_engines_name",
                table: "game_engines",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_game_game_engine_games_igdb_id",
                table: "game_game_engine",
                column: "games_igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_game_mode_games_igdb_id",
                table: "game_game_mode",
                column: "games_igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_genre_genres_id",
                table: "game_genre",
                column: "genres_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_mappings_concept_id",
                table: "game_mappings",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_mappings_igdb_id",
                table: "game_mappings",
                column: "igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_mappings_np_communication_id",
                table: "game_mappings",
                column: "np_communication_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_modes_name",
                table: "game_modes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_player_perspective_player_perspectives_id",
                table: "game_player_perspective",
                column: "player_perspectives_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_theme_themes_id",
                table: "game_theme",
                column: "themes_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_types_name",
                table: "game_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_games_game_type_id",
                table: "games",
                column: "game_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_genres_name",
                table: "genres",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_involved_companies_company_id",
                table: "involved_companies",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_involved_companies_game_id",
                table: "involved_companies",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_perspectives_name",
                table: "player_perspectives",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_release_dates_game_igdb_id",
                table: "release_dates",
                column: "game_igdb_id");

            migrationBuilder.CreateIndex(
                name: "ix_release_dates_region_id",
                table: "release_dates",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "ix_screenshots_game_id",
                table: "screenshots",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_themes_name",
                table: "themes",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_game");

            migrationBuilder.DropTable(
                name: "franchise_game");

            migrationBuilder.DropTable(
                name: "game_game_engine");

            migrationBuilder.DropTable(
                name: "game_game_mode");

            migrationBuilder.DropTable(
                name: "game_genre");

            migrationBuilder.DropTable(
                name: "game_mappings");

            migrationBuilder.DropTable(
                name: "game_player_perspective");

            migrationBuilder.DropTable(
                name: "game_theme");

            migrationBuilder.DropTable(
                name: "involved_companies");

            migrationBuilder.DropTable(
                name: "release_dates");

            migrationBuilder.DropTable(
                name: "screenshots");

            migrationBuilder.DropTable(
                name: "collections");

            migrationBuilder.DropTable(
                name: "franchises");

            migrationBuilder.DropTable(
                name: "game_engines");

            migrationBuilder.DropTable(
                name: "game_modes");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropTable(
                name: "player_perspectives");

            migrationBuilder.DropTable(
                name: "themes");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "game_types");
        }
    }
}
