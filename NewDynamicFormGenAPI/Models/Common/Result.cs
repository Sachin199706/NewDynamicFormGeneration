
namespace NewDynamicFormGenAPI.Models.Common;

/// <summary>
/// A generic result wrapper that communicates the outcome of an operation including success status, messages, and errors.
/// </summary>
/// <typeparam name="T">The type of data returned when the operation succeeds.</typeparam>
/// <remarks>
/// <para>
/// Result&lt;T&gt; is used throughout the application to provide a consistent, strongly-typed response contract
/// for service operations and API endpoints. It enables callers to check operation success and access error details
/// without relying on exception handling alone.
/// </para>
/// <para>
/// Use the static factory methods <see cref="Ok(T, string?)"/> to create successful results
/// and <see cref="Fail(string, List{string}?)"/> to create failure results.
/// </para>
/// </remarks>
public class Result<T>
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets an optional message providing context about the result, such as a confirmation or error description.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the data returned when the operation succeeds, or <c>null</c> if the operation failed or returned no data.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors, operation errors, or other diagnostic messages.
    /// </summary>
    /// <remarks>
    /// This list is populated when specific error details need to be communicated,
    /// such as validation errors for multiple fields.
    /// </remarks>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Creates a successful result with the provided data and optional message.
    /// </summary>
    /// <param name="data">The data to return with the successful result.</param>
    /// <param name="message">An optional message describing the successful outcome.</param>
    /// <returns>A successful Result&lt;T&gt; containing the provided data and message.</returns>
    public static Result<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    /// <summary>
    /// Creates a failed result with an error message and optional list of detailed errors.
    /// </summary>
    /// <param name="message">The primary error message describing why the operation failed.</param>
    /// <param name="errors">An optional list of detailed error messages (e.g., validation errors per field).</param>
    /// <returns>A failed Result&lt;T&gt; containing the error message and error details.</returns>
    public static Result<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? new List<string>() };
}

/// <summary>
/// A generic result wrapper for paginated data collections.
/// </summary>
/// <typeparam name="T">The type of items in the paginated result set.</typeparam>
/// <remarks>
/// <para>
/// PagedResult&lt;T&gt; is used to return large collections of data in pages,
/// reducing memory usage and improving performance for API clients and list views.
/// </para>
/// <para>
/// Callers can use the <see cref="Page"/>, <see cref="PageSize"/>, <see cref="TotalPages"/>,
/// and <see cref="TotalCount"/> properties to implement pagination UI controls or fetch subsequent pages.
/// </para>
/// </remarks>
public class PagedResult<T>
{
    /// <summary>
    /// Gets or sets the items in the current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the current page number (typically 1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total count of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets the total number of pages needed to display all items given the current page size.
    /// </summary>
    /// <remarks>
    /// Returns 0 if PageSize is 0 (division by zero protection).
    /// </remarks>
    public int TotalPages => PageSize == 0 ? 0 : (int)System.Math.Ceiling(TotalCount / (double)PageSize);
}
