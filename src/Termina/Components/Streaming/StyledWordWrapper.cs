// Copyright (c) Petabridge, LLC. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using Termina.Terminal;

namespace Termina.Components.Streaming;

/// <summary>
/// Word wrapper that preserves styling across line breaks.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the plain <see cref="WordWrapper"/>, this class operates on <see cref="StyledLine"/>
/// objects and ensures that text styles are preserved when lines are wrapped.
/// </para>
/// <para>
/// Example:
/// <code>
/// var line = new StyledLine();
/// line.Append("Hello ", Color.Green);
/// line.Append("World", Color.Red);
///
/// var wrapped = StyledWordWrapper.WrapLine(line, 8);
/// // Result: two lines, "Hello" (green) and "World" (red)
/// </code>
/// </para>
/// </remarks>
public static class StyledWordWrapper
{
    /// <summary>
    /// Wraps a styled line to fit within the specified width, preserving styling.
    /// </summary>
    /// <param name="line">The line to wrap.</param>
    /// <param name="width">Maximum width per line.</param>
    /// <returns>List of wrapped styled lines.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when width is not positive.</exception>
    public static List<StyledLine> WrapLine(StyledLine line, int width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");

        if (line.IsEmpty)
            return [new StyledLine()];

        // Fast path: line fits within width
        if (line.ColumnCount <= width)
            return [line.Clone()];

        var result = new List<StyledLine>();
        var currentLine = new StyledLine();
        var currentWidth = 0;

        var indent = Math.Max(0, Math.Min(line.HangingIndent, width - 1));

        void StartContinuationLine()
        {
            result.Add(currentLine);
            currentLine = new StyledLine();
            if (indent > 0)
            {
                currentLine.Append(new StyledSegment(new string(' ', indent), TextStyle.Default));
            }
            currentWidth = indent;
        }

        // Split into styled words
        var words = SplitIntoStyledWords(line);

        foreach (var word in words)
        {
            var wordWidth = word.ColumnCount;
            var maxLineCapacity = result.Count == 0 ? width : (width - indent);

            // If word itself is longer than max capacity for a line, break it
            if (wordWidth > maxLineCapacity)
            {
                // Flush current line if it has content beyond indent
                var hasContent = result.Count == 0 ? currentWidth > 0 : currentWidth > indent;
                if (hasContent)
                {
                    StartContinuationLine();
                }

                // Break long word into chunks while preserving styles
                var remaining = word;
                while (remaining.ColumnCount > (result.Count == 0 ? width : (width - indent)))
                {
                    var chunkLimit = result.Count == 0 ? width : (width - indent);
                    var chunk = remaining.SliceByColumns(0, chunkLimit);
                    if (chunk.IsEmpty)
                        break;

                    foreach (var segment in chunk.Segments)
                    {
                        currentLine.Append(segment);
                    }
                    StartContinuationLine();
                    remaining = remaining.SliceByColumns(chunk.ColumnCount, int.MaxValue / 2);
                }

                // Remainder becomes start of current line
                if (!remaining.IsEmpty)
                {
                    foreach (var segment in remaining.Segments)
                    {
                        currentLine.Append(segment);
                    }
                    currentWidth = currentLine.ColumnCount;
                }
            }
            else if (currentWidth == 0 || (result.Count > 0 && currentWidth == indent))
            {
                // Start of line - add word directly
                foreach (var segment in word.Segments)
                {
                    currentLine.Append(segment);
                }
                currentWidth = wordWidth;
                if (result.Count > 0)
                {
                    currentWidth = currentLine.ColumnCount;
                }
            }
            else if (currentWidth + 1 + wordWidth <= width)
            {
                // Word fits with space separator
                currentLine.Append(" ");
                foreach (var segment in word.Segments)
                {
                    currentLine.Append(segment);
                }
                currentWidth = currentLine.ColumnCount;
            }
            else
            {
                // Word doesn't fit, start new continuation line
                StartContinuationLine();
                foreach (var segment in word.Segments)
                {
                    currentLine.Append(segment);
                }
                currentWidth = currentLine.ColumnCount;
            }
        }

        // Flush remaining content if it has content beyond indent
        var hasFinalContent = result.Count == 0 ? currentWidth > 0 : currentWidth > indent;
        if (hasFinalContent)
        {
            result.Add(currentLine);
        }

        return result.Count > 0 ? result : [new StyledLine()];
    }

    /// <summary>
    /// Wraps multiple styled lines.
    /// </summary>
    /// <param name="lines">Lines to wrap.</param>
    /// <param name="width">Maximum width per line.</param>
    /// <returns>All wrapped styled lines.</returns>
    public static List<StyledLine> WrapLines(IEnumerable<StyledLine> lines, int width)
    {
        var result = new List<StyledLine>();
        foreach (var line in lines)
        {
            result.AddRange(WrapLine(line, width));
        }
        return result;
    }

    /// <summary>
    /// Calculates how many wrapped lines a styled line will produce.
    /// </summary>
    /// <param name="line">Line to measure.</param>
    /// <param name="width">Width for wrapping.</param>
    /// <returns>Number of wrapped lines.</returns>
    public static int CalculateWrappedLineCount(StyledLine line, int width)
    {
        return WrapLine(line, width).Count;
    }

    /// <summary>
    /// Calculates total wrapped line count for multiple styled lines.
    /// </summary>
    public static int CalculateTotalWrappedLineCount(IEnumerable<StyledLine> lines, int width)
    {
        return lines.Sum(line => CalculateWrappedLineCount(line, width));
    }

    /// <summary>
    /// Splits a styled line into styled words, preserving styling for each character.
    /// </summary>
    /// <remarks>
    /// This handles the complex case where a word may span multiple segments with
    /// different styles. Each resulting "word" is a StyledLine that may contain
    /// multiple segments.
    /// </remarks>
    private static List<StyledLine> SplitIntoStyledWords(StyledLine line)
    {
        var words = new List<StyledLine>();
        var currentWord = new StyledLine();

        foreach (var segment in line.Segments)
        {
            var currentSegmentStart = 0;

            for (var i = 0; i < segment.Length; i++)
            {
                var c = segment.Text[i];

                if (char.IsWhiteSpace(c))
                {
                    // Flush any accumulated text from this segment to current word
                    if (i > currentSegmentStart)
                    {
                        var text = segment.Text.Substring(currentSegmentStart, i - currentSegmentStart);
                        currentWord.Append(new StyledSegment(text, segment.Style));
                    }

                    // If we have a word, add it to results
                    if (!currentWord.IsEmpty)
                    {
                        words.Add(currentWord);
                        currentWord = new StyledLine();
                    }

                    currentSegmentStart = i + 1;
                }
            }

            // Flush remaining text from this segment
            if (currentSegmentStart < segment.Length)
            {
                var text = segment.Text.Substring(currentSegmentStart);
                currentWord.Append(new StyledSegment(text, segment.Style));
            }
        }

        // Add final word if any
        if (!currentWord.IsEmpty)
        {
            words.Add(currentWord);
        }

        return words;
    }
}
