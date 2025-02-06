using Microsoft.Graph;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PDFSplitTest.Code
{
    public class PDFSplitter
    {
        public async Task SplitPdfBySize(GraphService graphService, Stream inputStream, string driveId, string outputDirectoryName, long maxFileSize)
        {
            using (var inputDocument = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import))
            {
                int totalPages = inputDocument.PageCount;
                int fileCount = 1;
                var outputDocument = new PdfDocument();
                long currentSize = 0;

                for (int i = 0; i < totalPages; i++)
                {
                    var page = inputDocument.Pages[i];

                    // Stima della dimensione della pagina
                    using (var ms = new MemoryStream())
                    {
                        var tempDocument = new PdfDocument();
                        tempDocument.AddPage(page);
                        tempDocument.Save(ms, false);
                        long pageSize = ms.Length;

                        // Se la dimensione della pagina supera il limite massimo, salvala in un file separato
                        if (pageSize > maxFileSize)
                        {
                            if (outputDocument.PageCount > 0)
                            {
                                string outputFileName = $"part_{fileCount}.pdf";
                                using (var outputStream = new MemoryStream())
                                {
                                    outputDocument.Save(outputStream);
                                    outputStream.Position = 0;
                                    await graphService.UploadFileToSharePoint(driveId, outputStream, outputFileName, outputDirectoryName);
                                    Console.WriteLine($"Caricato: {outputFileName} con {outputDocument.PageCount} pagine");
                                }
                                fileCount++;
                                outputDocument = new PdfDocument();
                                currentSize = 0;
                            }

                            // Salva la pagina grande in un file separato
                            var singlePageDocument = new PdfDocument();
                            singlePageDocument.AddPage(page);
                            string singlePageFileName = $"part_{fileCount}_single_page.pdf";
                            using (var singlePageStream = new MemoryStream())
                            {
                                singlePageDocument.Save(singlePageStream);
                                singlePageStream.Position = 0;
                                await graphService.UploadFileToSharePoint(driveId, singlePageStream, $"{outputDirectoryName}/{singlePageFileName}");
                                Console.WriteLine($"Caricato: {singlePageFileName} con 1 pagina (dimensione: {pageSize} byte)");
                            }
                            fileCount++;
                        }
                        else if (currentSize + pageSize > maxFileSize)
                        {
                            // Salva il documento corrente e inizia un nuovo documento
                            string outputFileName = $"part_{fileCount}.pdf";
                            using (var outputStream = new MemoryStream())
                            {
                                outputDocument.Save(outputStream);
                                outputStream.Position = 0;
                                await graphService.UploadFileToSharePoint(driveId, outputStream, $"{outputDirectoryName}/{outputFileName}");
                                Console.WriteLine($"Caricato: {outputFileName} con {outputDocument.PageCount} pagine");
                            }
                            fileCount++;
                            outputDocument = new PdfDocument();
                            outputDocument.AddPage(page);
                            currentSize = pageSize;
                        }
                        else
                        {
                            // Aggiungi la pagina al documento corrente
                            outputDocument.AddPage(page);
                            currentSize += pageSize;
                        }
                    }
                }

                // Salva l'ultimo documento se contiene pagine
                if (outputDocument.PageCount > 0)
                {
                    string outputFileName = $"part_{fileCount}.pdf";
                    using (var outputStream = new MemoryStream())
                    {
                        outputDocument.Save(outputStream);
                        outputStream.Position = 0;
                        await graphService.UploadFileToSharePoint(driveId, outputStream, $"{outputDirectoryName}/{outputFileName}");
                        Console.WriteLine($"Caricato: {outputFileName} con {outputDocument.PageCount} pagine");
                    }
                }
            }

            Console.WriteLine("Divisione del PDF completata.");
        }
    }
}
