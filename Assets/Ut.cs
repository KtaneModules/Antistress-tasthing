using System;
using System.Collections.Generic;
using System.Text;

namespace Antistress
{
    static class Ut
    {
        public static IEnumerable<string> WordWrap(this string text, Func<int, double> wrapWidth, double widthOfASpace, Func<string, double> measure, bool allowBreakingWordsApart)
        {
            var curLine = 0;
            var atStartOfLine = true;
            var x = 0.0;
            var wordPieces = new List<string>();
            var wordPiecesWidths = new List<double>();
            var wordPiecesWidthsSum = 0.0;
            var actualWidth = 0.0;
            var numSpaces = 0;

            var sb = new StringBuilder();

            Action renderSpaces = () =>
            {
                sb.Append(' ', numSpaces);
                x += numSpaces * widthOfASpace;
                actualWidth = Math.Max(actualWidth, x);
            };

            Action renderPieces = () =>
            {
                // Add a space if we are not at the beginning of the line.
                if (!atStartOfLine)
                    renderSpaces();
                for (int j = 0; j < wordPieces.Count; j++)
                    sb.Append(wordPieces[j]);
                x += wordPiecesWidthsSum;
                actualWidth = Math.Max(actualWidth, x);
                wordPieces.Clear();
                wordPiecesWidths.Clear();
                wordPiecesWidthsSum = 0;
            };

            // The parameter is not used, but it may be useful in future
            Func<bool, string> advanceToNextLine = (bool newParagraph) =>
            {
                var line = sb.ToString();
                sb = new StringBuilder();
                x = 0;
                atStartOfLine = true;
                curLine++;
                return line;
            };

            var i = 0;
            while (i < text.Length)
            {
                // Check whether we are looking at a whitespace character or not, and if not, find the end of the word.
                int lengthOfWord = 0;
                while (lengthOfWord + i < text.Length && !isWrappableAfter(text, lengthOfWord + i) && text[lengthOfWord + i] != '\n')
                    lengthOfWord++;

                if (lengthOfWord > 0)
                {
                    // We are looking at a word. (It doesn’t matter whether we’re at the beginning of the word or in the middle of one.)
                    retry1:
                    string fragment = text.Substring(i, lengthOfWord);
                    var fragmentWidth = measure(fragment);
                    retry2:

                    // If we are at the start of a line, and the word itself doesn’t fit on a line by itself, give up
                    if (atStartOfLine && x + wordPiecesWidthsSum + fragmentWidth > wrapWidth(curLine))
                    {
                        if (!allowBreakingWordsApart)
                        {
                            // Return null to signal that we encountered a word that doesn’t fit in a line.
                            yield return null;
                            yield break;
                        }

                        // We don’t know exactly where to break the word, so use binary search to discover where that is.
                        if (lengthOfWord > 1)
                        {
                            lengthOfWord /= 2;
                            goto retry1;
                        }

                        // If we get to here, ‘WordPieces’ contains as much of the word as fits into one line, and the next letter makes it too long.
                        // If ‘WordPieces’ is empty, we are at the beginning of a paragraph and the first letter already doesn’t fit.
                        if (wordPieces.Count > 0)
                        {
                            // Render the part of the word that fits on the line and then move to the next line.
                            renderPieces();
                            yield return advanceToNextLine(false);
                        }
                    }
                    else if (!atStartOfLine && x + numSpaces * widthOfASpace + wordPiecesWidthsSum + fragmentWidth > wrapWidth(curLine))
                    {
                        // We have already rendered some text on this line, but the word we’re looking at right now doesn’t
                        // fit into the rest of the line, so leave the rest of this line blank and advance to the next line.
                        yield return advanceToNextLine(false);

                        // In case the word also doesn’t fit on a line all by itself, go back to top (now that ‘AtStartOfLine’ is true)
                        // where it will check whether we need to break the word apart.
                        goto retry2;
                    }

                    // If we get to here, the current fragment fits on the current line (or it is a single character that overflows
                    // the line all by itself).
                    wordPieces.Add(fragment);
                    wordPiecesWidths.Add(fragmentWidth);
                    wordPiecesWidthsSum += fragmentWidth;
                    i += lengthOfWord;
                    continue;
                }

                // We encounter a whitespace character. All the word pieces fit on the current line, so render them.
                if (wordPieces.Count > 0)
                {
                    renderPieces();
                    atStartOfLine = false;
                }

                if (text[i] == '\n')
                {
                    // If the whitespace character is actually a newline, start a new paragraph.
                    yield return advanceToNextLine(true);
                    i++;
                }
                else
                {
                    // Discover the extent of the spaces.
                    numSpaces = 0;
                    while (numSpaces + i < text.Length && isWrappableAfter(text, numSpaces + i) && text[numSpaces + i] != '\n')
                        numSpaces++;
                    i += numSpaces;

                    if (atStartOfLine)
                    {
                        // If we are at the beginning of the line, treat these spaces as the paragraph’s indentation.
                        renderSpaces();
                    }
                }
            }

            renderPieces();
            if (sb.Length > 0)
                yield return sb.ToString();
        }

        private static bool isWrappableAfter(string txt, int index)
        {
            // Return false for all the whitespace characters that should NOT be wrappable
            switch (txt[index])
            {
                case '\u00a0':   // NO-BREAK SPACE
                case '\u202f':    // NARROW NO-BREAK SPACE
                    return false;
            }

            // Return true for all the NON-whitespace characters that SHOULD be wrappable
            switch (txt[index])
            {
                case '\u200b':   // ZERO WIDTH SPACE
                    return true;
            }

            // Apart from the above exceptions, wrap at whitespace characters.
            return char.IsWhiteSpace(txt, index);
        }
    }
}
