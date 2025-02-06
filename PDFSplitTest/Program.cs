// See https://aka.ms/new-console-template for more information
using Microsoft.Graph.Models;
using PDFSplitTest.Code;

#region FOR CONNECTION
string tenantId = "70fca42d-2e4c-4809-8b84-deb215264e2e"; //https://onguarneri.sharepoint.com/
string hostName = "onguarneri.sharepoint.com";
string clientId = "caaed9d8-cf64-4ff9-833e-af17ed52330d";
string clientSecret = "Nh78Q~Jdi.AwF3f1CqK1G5fXA54MxfW2.gtRQcJ~";
#endregion

Console.WriteLine("Start split");
//string inputPdfPath = "C:\\temp\\PDFSplitter\\in\\ManualeD&D.pdf";
//string outputDirectory = "C:\\temp\\PDFSplitter\\out";

// Dimensione massima di file in uscita
long maxFileSize = 1 * 1024 * 1024; // 1 MB in byte

var splitter = new PDFSplitter();
var graphService = new GraphService(tenantId, hostName, clientId, clientSecret);

var site = await graphService.GetSiteAync("/sites/CorsoSPFX");
var libraries = await graphService.GetLibrariesAsync(site!.Id!);

Drive? lib = libraries!.Where(l => l.Name!.Equals("Examples", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

var libId = lib!.Id;

// Leggi il file PDF da SharePoint
//var driveItem = await graphService.GetDriveItemByNameAsync(libId!, "ManualeD&D.pdf");
var downloadUrl = await graphService.GetFileDownloadUrl(libId!, "ManualeD&D.pdf");

using (var fileStream = await graphService.DownloadFileAsStream(downloadUrl))
{
    await splitter.SplitPdfBySize(graphService, fileStream!, libId!, "SplitOut", maxFileSize);
}

Console.WriteLine("End split");
