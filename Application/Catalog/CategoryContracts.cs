using System.ComponentModel.DataAnnotations;

namespace ILLiasu_Local_Mall.Application.Catalog;

// Shared form metadata; the service evaluates these rules for every command caller.
public class CategoryDetailsInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(160), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and single hyphens between words.")]
    public string Slug { get; set; } = "";
}

public sealed record CategoryPage(IReadOnlyList<CategorySnapshot> Items, string Search, string Status, int Page, int Pages, int Total, int PageSize);
public enum CategoryWriteOutcome { Succeeded, ValidationFailed, NotFound, Conflict, AlreadyApplied }
public sealed record CategoryValidationError(string Field, string Message);
public sealed record CategoryWriteResult(CategoryWriteOutcome Outcome, string Name, string Slug,
    IReadOnlyList<CategoryValidationError> Errors, CategorySnapshot? Current = null);
