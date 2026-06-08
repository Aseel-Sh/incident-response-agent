using System.Text.RegularExpressions;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal static class RunbookTextAnalysis
{
	private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public static HashSet<string> Tokenize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		}

		return TokenRegex.Matches(value.ToLowerInvariant())
			.Select(match => match.Value)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	public static double CosineSimilarity(float[] left, float[] right)
	{
		var length = Math.Min(left.Length, right.Length);
		if (length == 0)
		{
			return 0;
		}

		double dot = 0;
		double leftMagnitude = 0;
		double rightMagnitude = 0;

		for (var index = 0; index < length; index++)
		{
			var leftValue = left[index];
			var rightValue = right[index];
			dot += leftValue * rightValue;
			leftMagnitude += leftValue * leftValue;
			rightMagnitude += rightValue * rightValue;
		}

		if (leftMagnitude <= 0 || rightMagnitude <= 0)
		{
			return 0;
		}

		return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
	}
}
