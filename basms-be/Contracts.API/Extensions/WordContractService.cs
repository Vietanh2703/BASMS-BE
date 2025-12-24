using Xceed.Words.NET;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Contracts.API.Extensions;
public class WordContractService : IWordContractService
{
    private readonly ILogger<WordContractService> _logger;

    public WordContractService(ILogger<WordContractService> logger)
    {
        _logger = logger;
    }
    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractTemplateAsync(
        Stream templateStream,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Filling labor contract template with {Count} placeholders", placeholders.Count);
            var templateMemoryStream = new MemoryStream();
            await templateStream.CopyToAsync(templateMemoryStream, cancellationToken);
            templateMemoryStream.Position = 0;
            var doc = DocX.Load(templateMemoryStream);

            try
            {
                _logger.LogInformation("Original document has {Length} characters", doc.Text.Length);
                _logger.LogDebug("First 200 chars: {Text}", doc.Text.Length > 200 ? doc.Text.Substring(0, 200) : doc.Text);
                
                int replacementCount = 0;
                foreach (var placeholder in placeholders)
                {
                    var key = $"{{{{{placeholder.Key}}}}}"; 
                    var value = placeholder.Value;

                    _logger.LogInformation("Attempting to replace '{Key}' with '{Value}'", key, value);
                    
                    if (doc.Text.Contains(key))
                    {
                        doc.ReplaceText(key, value);
                        replacementCount++;
                        _logger.LogInformation("Replaced '{Key}'", key);
                    }
                    else
                    {
                        _logger.LogWarning("Placeholder '{Key}' not found in document", key);
                    }
                }

                _logger.LogInformation("Total replacements made: {Count} out of {Total}", replacementCount, placeholders.Count);

                var outputStream = new MemoryStream();
                doc.SaveAs(outputStream);
                outputStream.Position = 0;

                _logger.LogInformation("Successfully filled labor contract template. Output size: {Size} bytes", outputStream.Length);

                return (true, outputStream, null);
            }
            finally
            {
                doc.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill labor contract template");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> FillLaborContractByLabelAsync(
        Stream templateStream,
        Dictionary<string, string> labelValues,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Filling labor contract by labels with {Count} values", labelValues.Count);
            
            var templateMemoryStream = new MemoryStream();
            await templateStream.CopyToAsync(templateMemoryStream, cancellationToken);
            templateMemoryStream.Position = 0;

            var doc = DocX.Load(templateMemoryStream);

            try
            {
                int replacementCount = 0;
                foreach (var paragraph in doc.Paragraphs)
                {
                    var text = paragraph.Text;

                    foreach (var labelValue in labelValues)
                    {
                        var label = labelValue.Key;
                        var value = labelValue.Value;
                        
                        var patterns = new[]
                        {
                            $"{label}:\\s*[…\\.]+",  
                            $"{label}:[…\\.]+",     
                        };

                        foreach (var pattern in patterns)
                        {
                            if (Regex.IsMatch(text, pattern))
                            {
                                var newText = Regex.Replace(
                                    text,
                                    pattern,
                                    $"{label}: {value}");
                                
                                paragraph.ReplaceText(text, newText);
                                replacementCount++;

                                _logger.LogInformation("Replaced label '{Label}' with '{Value}'", label, value);
                                break;
                            }
                        }
                    }
                }

                _logger.LogInformation("Total label replacements made: {Count} out of {Total}", replacementCount, labelValues.Count);
                
                var outputStream = new MemoryStream();
                doc.SaveAs(outputStream);
                outputStream.Position = 0;

                _logger.LogInformation("Successfully filled contract by labels. Output size: {Size} bytes", outputStream.Length);

                return (true, outputStream, null);
            }
            finally
            {
                doc.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill contract by labels");
            return (false, null, ex.Message);
        }
    }


    public Task<(bool Success, string? Text, string? ErrorMessage)> ExtractTextFromWordAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting text from Word document with full Unicode support");

            using var doc = DocX.Load(documentStream);
            
            var textBuilder = new StringBuilder();
            
            if (doc.Headers?.Odd?.Paragraphs != null)
            {
                foreach (var header in doc.Headers.Odd.Paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(header.Text))
                    {
                        textBuilder.AppendLine(header.Text);
                        _logger.LogDebug("Extracted from header: {Text}", header.Text.Substring(0, Math.Min(50, header.Text.Length)));
                    }
                }
            }

            if (doc.Paragraphs != null)
            {
                foreach (var paragraph in doc.Paragraphs)
                {
                    var paragraphText = paragraph.Text;
                    if (!string.IsNullOrEmpty(paragraphText))
                    {
                        textBuilder.AppendLine(paragraphText);
                    }
                    else
                    {
                        textBuilder.AppendLine();
                    }
                }
            }
            else
            {
                _logger.LogWarning("Document has no paragraphs");
            }
            if (doc.Tables != null && doc.Tables.Count > 0)
            {
                _logger.LogInformation("Found {TableCount} tables in document", doc.Tables.Count);

                foreach (var table in doc.Tables)
                {
                    textBuilder.AppendLine(); 
                    textBuilder.AppendLine("=== TABLE ===");

                    foreach (var row in table.Rows)
                    {
                        var rowTexts = new List<string>();
                        foreach (var cell in row.Cells)
                        {
                            var cellText = string.Join(" ", cell.Paragraphs.Select(p => p.Text));
                            rowTexts.Add(cellText);
                        }
                        textBuilder.AppendLine(string.Join("\t", rowTexts));
                    }

                    textBuilder.AppendLine("=== END TABLE ===");
                    textBuilder.AppendLine();
                }
            }
            
            if (doc.Footers?.Odd?.Paragraphs != null)
            {
                foreach (var footer in doc.Footers.Odd.Paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(footer.Text))
                    {
                        textBuilder.AppendLine(footer.Text);
                        _logger.LogDebug("Extracted from footer: {Text}", footer.Text.Substring(0, Math.Min(50, footer.Text.Length)));
                    }
                }
            }

            var text = textBuilder.ToString();
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

            _logger.LogInformation(
                "Successfully extracted {Length} characters from Word document " +
                "({ParagraphCount} paragraphs, {TableCount} tables)",
                text.Length,
                doc.Paragraphs?.Count ?? 0,
                doc.Tables?.Count ?? 0);

            return Task.FromResult<(bool Success, string? Text, string? ErrorMessage)>((true, text, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from Word document");
            return Task.FromResult<(bool Success, string? Text, string? ErrorMessage)>((false, null, ex.Message));
        }
    }

    public async Task<(bool Success, Stream? FileStream, string? ErrorMessage)> InsertSignatureImageAsync(
        Stream documentStream,
        string contentControlTag,
        Stream imageStream,
        string imageFileName,
        CancellationToken cancellationToken = default)
    {
        MemoryStream? imageMemoryStream = null;

        try
        {
            _logger.LogInformation("Inserting signature image '{FileName}' into content control: {Tag}",
                imageFileName, contentControlTag);
            
            imageMemoryStream = new MemoryStream();
            await imageStream.CopyToAsync(imageMemoryStream, cancellationToken);
            imageMemoryStream.Position = 0;

            var imageType = DetectImageTypeFromBytes(imageMemoryStream);
            if (imageType == null)
            {
                _logger.LogWarning("Unsupported image format for file: {FileName}", imageFileName);
                imageMemoryStream?.Dispose();
                return (false, null, "Unsupported image format. Only PNG and JPEG are supported.");
            }

            _logger.LogInformation("Detected image type: {ImageType}", imageType);
            
            var docMemoryStream = new MemoryStream();
            await documentStream.CopyToAsync(docMemoryStream, cancellationToken);
            docMemoryStream.Position = 0;
            var outputStream = new MemoryStream();
            using (var wordDoc = WordprocessingDocument.Open(docMemoryStream, true))
            {
                var mainPart = wordDoc.MainDocumentPart;
                if (mainPart == null)
                {
                    docMemoryStream.Dispose();
                    outputStream.Dispose();
                    imageMemoryStream?.Dispose();
                    return (false, null, "Main document part not found");
                }
                
                var sdtElements = mainPart.Document.Body?.Descendants<SdtElement>()
                    .Where(sdt =>
                    {
                        var sdtProperties = sdt.Elements<SdtProperties>().FirstOrDefault();
                        var tag = sdtProperties?.Elements<DocumentFormat.OpenXml.Wordprocessing.Tag>().FirstOrDefault();
                        return tag?.Val?.Value == contentControlTag;
                    }).ToList();

                if (sdtElements == null || !sdtElements.Any())
                {
                    _logger.LogWarning("Content control with tag '{Tag}' not found", contentControlTag);
                    docMemoryStream.Dispose();
                    outputStream.Dispose();
                    imageMemoryStream?.Dispose();
                    return (false, null, $"Content control with tag '{contentControlTag}' not found in document");
                }

                _logger.LogInformation("Found {Count} content control(s) with tag '{Tag}'",
                    sdtElements.Count, contentControlTag);
                
                foreach (var sdtElement in sdtElements)
                {
                    imageMemoryStream.Position = 0;
                    ImagePart imagePart;

                    if (imageType == "png")
                    {
                        imagePart = mainPart.AddImagePart(ImagePartType.Png);
                    }
                    else
                    {
                        imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
                    }

                    imagePart.FeedData(imageMemoryStream);

                    var imagePartId = mainPart.GetIdOfPart(imagePart);
                    var element = CreateImageElement(imagePartId, imageFileName, 200, 80);
                    
                    OpenXmlCompositeElement? sdtContent = sdtElement.Elements<SdtContentBlock>().FirstOrDefault();
                    if (sdtContent == null)
                        sdtContent = sdtElement.Elements<SdtContentRun>().FirstOrDefault();
                    if (sdtContent == null)
                        sdtContent = sdtElement.Elements<SdtContentCell>().FirstOrDefault();

                    if (sdtContent != null)
                    {
                        sdtContent.RemoveAllChildren();
                        
                        var paragraph = new Paragraph(
                            new Run(element));
                        sdtContent.Append(paragraph);

                        _logger.LogInformation("Inserted image into content control '{Tag}'", contentControlTag);
                    }
                }
                
                _logger.LogInformation("Saving document changes...");
            } 
            
            imageMemoryStream?.Dispose();
            
            docMemoryStream.Position = 0;
            await docMemoryStream.CopyToAsync(outputStream, cancellationToken);
            docMemoryStream.Dispose();
            
            outputStream.Position = 0;

            _logger.LogInformation("Successfully inserted signature image. Output stream size: {Size} bytes", outputStream.Length);
            return (true, outputStream, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert signature image into content control '{Tag}'", contentControlTag);
            
            imageMemoryStream?.Dispose();

            return (false, null, ex.Message);
        }
    }


    private static Drawing CreateImageElement(string imagePartId, string fileName, long widthEmus, long heightEmus)
    {
        var widthInEmus = widthEmus * 9525L;
        var heightInEmus = heightEmus * 9525L;

        var element = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthInEmus, Cy = heightInEmus },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = fileName },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = fileName },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = imagePartId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthInEmus, Cy = heightInEmus }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });

        return element;
    }

    private static string? DetectImageTypeFromBytes(MemoryStream imageStream)
    {
        if (imageStream == null || imageStream.Length < 4)
        {
            return null;
        }

        var position = imageStream.Position;
        imageStream.Position = 0;

        var header = new byte[4];
        imageStream.Read(header, 0, 4);
        imageStream.Position = position; 
        
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            return "png";
        }
        
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "jpeg";
        }

        return null;
    }
}
