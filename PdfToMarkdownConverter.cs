using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfToMarkdown
{
    public class TextChunk
    {
        public required string Text;
        public float X;
        public float Y;
        public float FontSize;
        public bool IsBold;
        public bool IsItalic;
    }

    public class StructuredMarkdownExtractor : IEventListener
    {
        private readonly List<TextChunk> chunks = new();
        private readonly List<float> fontSizes = new();

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_TEXT) return;

            var renderInfo = (TextRenderInfo)data;
            string text = renderInfo.GetText();
            if (string.IsNullOrWhiteSpace(text)) return;

            var font = renderInfo.GetFont();
            var fontName = font.GetFontProgram().GetFontNames().GetFontName().ToLower();
            float fontSize = renderInfo.GetFontSize();
            float x = renderInfo.GetBaseline().GetStartPoint().Get(0);
            float y = renderInfo.GetBaseline().GetStartPoint().Get(1);

            bool isBold = fontName.Contains("bold");
            bool isItalic = fontName.Contains("italic");

            if (!fontSizes.Contains(fontSize))
            {
                fontSizes.Add(fontSize);
                fontSizes.Sort((a, b) => b.CompareTo(a));
            }

            chunks.Add(new TextChunk
            {
                Text = text,
                X = x,
                Y = y,
                FontSize = fontSize,
                IsBold = isBold,
                IsItalic = isItalic
            });
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new HashSet<EventType> { EventType.RENDER_TEXT };
        }

        public string GetMarkdown()
        {
            var groupedByLine = chunks
                .GroupBy(c => c.Y)
                .OrderByDescending(g => g.Key) // top to bottom
                .Select(g => g.OrderBy(c => c.X).ToList())
                .ToList();

            StringBuilder sb = new();
            float indentUnit = 20f; 
            float bodyFontSize = fontSizes.Min(); 
             
            foreach (var line in groupedByLine)
            {
                var lineText = new StringBuilder();
                bool isHeading = true;
                foreach (var chunk in line)
                {
                    string styled = chunk.Text;
                    if (chunk.IsBold)
                    {
                        styled = $"**{styled}**";
                    }else isHeading = false;
                    if (chunk.IsItalic) styled = $"_{styled}_";

                    lineText.Append(styled + " ");
                }

                float lineX = line.First().X;
                float fontSize = line.First().FontSize;
                int headingLevel = fontSizes.IndexOf(fontSize) + 1;
                //Debug.WriteLine($"LineX : {lineX}");
                int indentVal = (int)(lineX / indentUnit);
                //if (indentVal>=4) indentVal = 3;
                string indentation = new string(' ', indentVal) ?? "";
                //bool isHeading = fontSize > bodyFontSize && headingLevel <= 3 && lineText.Length > 10;

                if (isHeading)
                    sb.AppendLine($"{(indentVal >= 4 ? "\n" : indentation)}{new string('#', headingLevel)} {lineText.ToString().Trim()}");
                else
                    sb.AppendLine($"{(indentVal >= 4 ? "\n" : indentation)}{lineText.ToString().Trim()}");
            }

            return sb.ToString();
        }
    }

    public class PdfToMarkdownConverterClass
    {
        public String ConvertPdfToMarkdown(Stream inputStream)
        {
            using var reader = new PdfReader(inputStream);
            using var pdfDoc = new PdfDocument(reader);

            StringBuilder sb = new();

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var strategy = new StructuredMarkdownExtractor();
                var processor = new PdfCanvasProcessor(strategy);
                processor.ProcessPageContent(pdfDoc.GetPage(i));
                

                sb.AppendLine($"## Page {i}\n");
                sb.AppendLine(strategy.GetMarkdown());
                sb.AppendLine("\n---\n");
            }
            return sb.ToString();
        }
        public void ConvertPdfToMarkdown(string inputPath, string outputPath)
        {
            using var reader = new PdfReader(inputPath);
            using var pdfDoc = new PdfDocument(reader);
            using var writer = new StreamWriter(outputPath);

            StringBuilder sb = new();

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var strategy = new StructuredMarkdownExtractor();
                var processor = new PdfCanvasProcessor(strategy);
                processor.ProcessPageContent(pdfDoc.GetPage(i));


                writer.WriteLine($"## Page {i}\n");
                writer.WriteLine(strategy.GetMarkdown());
                writer.WriteLine("\n---\n");
            }
        }
    }
}
