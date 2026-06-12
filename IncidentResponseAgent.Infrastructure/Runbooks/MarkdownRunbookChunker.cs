using System.Text.RegularExpressions;
using IncidentResponseAgent.Domain.Runbooks;

namespace IncidentResponseAgent.Infrastructure.Runbooks;

internal static class MarkdownRunbookChunker
{
	private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
	private static readonly Regex StepRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);

	public static IReadOnlyList<RunbookChunk> Chunk(RunbookDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);

		var chunks = new List<RunbookChunk>();
		var currentHeadingPath = new List<string>();
		var currentLines = new List<string>();
		var currentCodeFence = false;
		var lines = document.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

		foreach (var rawLine in lines)
		{
			var line = rawLine.TrimEnd();
			var trimmed = line.Trim();

			if (!currentCodeFence)
			{
				var headingMatch = HeadingRegex.Match(trimmed);
				if (headingMatch.Success)
				{
					FlushChunk();
					UpdateHeadingPath(currentHeadingPath, headingMatch.Groups[1].Value.Length, headingMatch.Groups[2].Value.Trim());
					continue;
				}

				if (trimmed == "---")
				{
					FlushChunk();
					continue;
				}

				if (StepRegex.IsMatch(trimmed) && currentLines.Count > 0)
				{
					FlushChunk();
				}
			}

			if (trimmed.StartsWith("```", StringComparison.Ordinal))
			{
				currentCodeFence = !currentCodeFence;
			}

			currentLines.Add(line);
		}

		FlushChunk();
		return chunks;

		void FlushChunk()
		{
			if (currentLines.Count == 0)
			{
				return;
			}

			var text = NormalizeChunkText(currentLines);
			if (string.IsNullOrWhiteSpace(text))
			{
				currentLines.Clear();
				return;
			}

			var sectionPath = currentHeadingPath.Count == 0
				? string.Empty
				: string.Join(" > ", currentHeadingPath);

			chunks.Add(new RunbookChunk(
				Ordinal: chunks.Count + 1,
				SectionPath: sectionPath,
				Text: text,
				SearchText: BuildSearchText(document, sectionPath, text)));

			currentLines.Clear();
		}
}

	private static void UpdateHeadingPath(List<string> headingPath, int headingLevel, string headingText)
	{
		if (headingLevel <= 1)
		{
			headingPath.Clear();
			return;
		}

		var targetCount = Math.Max(headingLevel - 2, 0);
		if (headingPath.Count > targetCount)
		{
			headingPath.RemoveRange(targetCount, headingPath.Count - targetCount);
		}

		if (headingText.Length > 0)
		{
			if (headingPath.Count == targetCount)
			{
				headingPath.Add(headingText);
			}
			else if (headingPath.Count > targetCount)
			{
				headingPath[targetCount] = headingText;
			}
		}
	}

	private static string NormalizeChunkText(IReadOnlyList<string> lines)
	{
		var text = string.Join(Environment.NewLine, lines).Trim();
		return Regex.Replace(text, "[ \t]+\r?\n", Environment.NewLine);
	}

	private static string BuildSearchText(RunbookDocument document, string sectionPath, string chunkText)
	{
		var builder = new List<string>
		{
			document.Title,
			document.Summary
		};

		if (!string.IsNullOrWhiteSpace(sectionPath))
		{
			builder.Add(sectionPath);
		}

		builder.Add(chunkText);
		builder.AddRange(document.Tags);
		return string.Join(Environment.NewLine, builder.Where(part => !string.IsNullOrWhiteSpace(part)));
	}
}

internal sealed record RunbookChunk(int Ordinal, string SectionPath, string Text, string SearchText);
