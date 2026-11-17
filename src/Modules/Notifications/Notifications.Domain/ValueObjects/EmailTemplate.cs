using System;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Domain.ValueObjects;

/// <summary>
	/// Describes a reusable e‑mail template.
	/// </summary>
	public sealed record EmailTemplate
	{
	    /// <summary>
	    /// Human‑readable name of the template.
	    /// </summary>
	    public required string Name { get; init; }

	    /// <summary>
	    /// Subject line with placeholders.
	    /// </summary>
	    public required string SubjectTemplate { get; init; }

	    /// <summary>
	    /// Body content with placeholders.
	    /// </summary>
	    public required string BodyTemplate { get; init; }

	    private EmailTemplate() { }

    /// <summary>
    /// Creates an <see cref="EmailTemplate"/> after basic validation.
    /// </summary>
    /// <param name="name">Template name.</param>
    /// <param name="subjectTemplate">Subject pattern.</param>
    /// <param name="bodyTemplate">Body pattern.</param>
    /// <returns>A <see cref="Result{EmailTemplate}"/> indicating success or failure.</returns>
    public static Result<EmailTemplate> Create(string name, string subjectTemplate, string bodyTemplate)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<EmailTemplate>.Failure(Error.Validation("Template name is required."));

        if (subjectTemplate is null)
            return Result<EmailTemplate>.Failure(Error.Validation("Subject template cannot be null."));

        if (bodyTemplate is null)
            return Result<EmailTemplate>.Failure(Error.Validation("Body template cannot be null."));

        var template = new EmailTemplate
        {
            Name = name.Trim(),
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate
        };

        return Result<EmailTemplate>.Success(template);
    }
}
