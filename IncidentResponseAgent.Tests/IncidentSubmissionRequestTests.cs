using System.ComponentModel.DataAnnotations;
using IncidentResponseAgent.Api.Contracts.Incidents;

namespace IncidentResponseAgent.Tests;

public sealed class IncidentSubmissionRequestTests
{
	[Fact]
	public void ValidateRejectsUnknownSeverity()
	{
		var request = new IncidentSubmissionRequest
		{
			Title = "Checkout failures",
			Description = "Checkout requests are failing.",
			Severity = "Emergency"
		};

		var results = Validate(request);

		Assert.Contains(results, result => result.MemberNames.Contains(nameof(IncidentSubmissionRequest.Severity)));
	}

	[Fact]
	public void ValidateRejectsTooManyTags()
	{
		var request = new IncidentSubmissionRequest
		{
			Title = "Checkout failures",
			Description = "Checkout requests are failing.",
			Severity = "sev2",
			Tags = Enumerable.Range(1, 11).Select(index => $"tag-{index}").ToArray()
		};

		var results = Validate(request);

		Assert.Contains(results, result => result.MemberNames.Contains(nameof(IncidentSubmissionRequest.Tags)));
	}

	private static IReadOnlyList<ValidationResult> Validate(IncidentSubmissionRequest request)
	{
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
		return results;
	}
}
