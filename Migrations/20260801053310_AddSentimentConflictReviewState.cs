using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fruitables.Migrations
{
    /// <inheritdoc />
    public partial class AddSentimentConflictReviewState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisVersion",
                table: "ReviewSentiments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommentSentiment",
                table: "ReviewSentiments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasRatingCommentConflict",
                table: "ReviewSentiments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSafetyRisk",
                table: "ReviewSentiments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsManualReview",
                table: "ReviewSentiments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RatingSentiment",
                table: "ReviewSentiments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_CommentSentiment",
                table: "ReviewSentiments",
                column: "CommentSentiment");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_HasRatingCommentConflict",
                table: "ReviewSentiments",
                column: "HasRatingCommentConflict");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_HasSafetyRisk",
                table: "ReviewSentiments",
                column: "HasSafetyRisk");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_NeedsManualReview",
                table: "ReviewSentiments",
                column: "NeedsManualReview");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSentiments_RatingSentiment",
                table: "ReviewSentiments",
                column: "RatingSentiment");

            // Existing AI results were produced before rating/comment conflict
            // metadata existed. Re-run reviews with comments under sentiment-v2
            // instead of pretending the old blended label is CommentSentiment.
            migrationBuilder.Sql(@"
UPDATE s
SET s.RatingSentiment = CASE
        WHEN r.Rating >= 4 THEN 0
        WHEN r.Rating = 3 THEN 1
        ELSE 2
    END,
    s.CommentSentiment = NULL,
    s.HasRatingCommentConflict = 0,
    s.HasSafetyRisk = 0,
    s.AnalysisVersion = NULL,
    s.NeedsManualReview = CASE
        WHEN NULLIF(LTRIM(RTRIM(r.Comment)), '') IS NOT NULL AND s.Source <> 2 THEN 1
        ELSE 0
    END
FROM ReviewSentiments s
INNER JOIN Reviews r ON r.Id = s.ReviewId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReviewSentiments_CommentSentiment",
                table: "ReviewSentiments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewSentiments_HasRatingCommentConflict",
                table: "ReviewSentiments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewSentiments_HasSafetyRisk",
                table: "ReviewSentiments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewSentiments_NeedsManualReview",
                table: "ReviewSentiments");

            migrationBuilder.DropIndex(
                name: "IX_ReviewSentiments_RatingSentiment",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "AnalysisVersion",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "CommentSentiment",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "HasRatingCommentConflict",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "HasSafetyRisk",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "NeedsManualReview",
                table: "ReviewSentiments");

            migrationBuilder.DropColumn(
                name: "RatingSentiment",
                table: "ReviewSentiments");
        }
    }
}
