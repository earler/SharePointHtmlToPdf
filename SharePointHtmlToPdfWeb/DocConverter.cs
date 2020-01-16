using System;
using System.IO;
using iText.Html2pdf;
using iText.Html2pdf.Attach;
using iText.Html2pdf.Attach.Impl;
using iText.Html2pdf.Attach.Impl.Tags;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Events;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Extgstate;
using iText.Kernel.Pdf.Layer;
using iText.Kernel.Pdf.Tagging;
using iText.Kernel.Pdf.Tagutils;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Font;
using iText.StyledXmlParser.Node;

namespace SharePointHtmlToPdfWeb
{
    public static class DocConverter
    {
        private const string fontWebdings = "~/content/fonts/webdings.ttf";
        private const string fontCalibri = "~/content/fonts/calibrib.ttf";
        private const string headerAllPages = "~/content/images/HeaderAllPages.jpg";
        private const string headerPage1 = "~/content/images/HeaderPage1.jpg";

        [Flags]
        public enum DocOptions
        {
            None = 0,
            DisplayTitle = 1,
            AddHeaderPageOne = 2,
            AddHeaderAllPages = 4,
            AddLineBottomEachPage = 8
        }
        
        public static byte[] ConvertToPdfWithTags(string html, string title, string docOptions)
        {
            DocOptions documentOptions = DocOptions.None;
            if (!string.IsNullOrEmpty(docOptions))
            {
                int options;
                if (int.TryParse(docOptions, out options))
                    documentOptions = (DocOptions)options;
            }
            PdfFontFactory.RegisterDirectory(System.Web.Hosting.HostingEnvironment.MapPath("~/content/fonts/"));
            ConverterProperties props = new ConverterProperties();

            FontProvider fp = new FontProvider();
            fp.AddDirectory(System.Web.Hosting.HostingEnvironment.MapPath("~/content/fonts/"));
            props.SetFontProvider(fp);
            props.SetTagWorkerFactory(new DefaultTagWorkerFactory());

            ImageData imageFirst =
                            ImageDataFactory.Create(System.Web.Hosting.HostingEnvironment.MapPath(headerPage1));

            ImageData imageAll =
                ImageDataFactory.Create(System.Web.Hosting.HostingEnvironment.MapPath(headerAllPages));
            using (var workStream = new MemoryStream())
            {
                using (var pdfWriter = new PdfWriter(workStream, new WriterProperties().AddUAXmpMetadata().SetPdfVersion
                    (PdfVersion.PDF_2_0).SetFullCompressionMode(true)))
                {
                    
                    PdfDocument pdfDoc = new PdfDocument(pdfWriter);
                    pdfDoc.GetCatalog().SetLang(new PdfString("en-GB"));


                    pdfDoc.GetCatalog().SetViewerPreferences(new PdfViewerPreferences().SetDisplayDocTitle(true));
                    if (documentOptions > 0)
                        pdfDoc.AddEventHandler(PdfDocumentEvent.END_PAGE, new PublicReportHeaderFooter(documentOptions, title,imageFirst,imageAll));
                    //Set meta tags
                    var pdfMetaData = pdfDoc.GetDocumentInfo();
                    pdfMetaData.AddCreationDate();
                    pdfMetaData.GetProducer();
                    pdfMetaData.SetCreator("iText Software");
                    //Set the document to be tagged
                    pdfDoc.SetTagged();

                    //Extra metadata tags available
                    //pdfMetaData.SetAuthor("");
                    using (var document = HtmlConverter.ConvertToDocument(html, pdfDoc, props))
                    {
                        //Can do more with document here if necessary
                    }

                    //Returns the written-to MemoryStream containing the PDF.   
                    return workStream.ToArray();
                }
            }
        }



    }
    public class PublicReportHeaderFooter : IEventHandler
    {
        private DocConverter.DocOptions _docOptions;
        private string _title;
        private ImageData _idHeaderFirst;
        private ImageData _idHeaderAll;
        public PublicReportHeaderFooter(DocConverter.DocOptions docOptions, string title, ImageData idHeaderFirst, ImageData idHeaderAll)
        {
            _docOptions = docOptions;
            _title = title;
            _idHeaderFirst = idHeaderFirst;
            _idHeaderAll = idHeaderAll;
        }

        public virtual void HandleEvent(Event pdfEvent)
        {
            {
                PdfDocumentEvent docEvent = (PdfDocumentEvent)pdfEvent;
                PdfDocument pdfDoc = docEvent.GetDocument();
                Document document = new Document(pdfDoc);
                PdfPage page = docEvent.GetPage();
                int pageNumber = pdfDoc.GetPageNumber(page);
                Rectangle pageSize = page.GetPageSize();
                
                PdfCanvas pdfCanvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdfDoc);

                //Add header and footer
                PdfFont font = PdfFontFactory.CreateRegisteredFont("calibri-bold");

                if (pageNumber == 1)
                {
                    if (_docOptions.HasFlag(DocConverter.DocOptions.AddHeaderPageOne))
                    {
                        pdfCanvas.SaveState();
                        PdfExtGState state = new PdfExtGState();
                        state.SetFillOpacity(1.0f);
                        pdfCanvas.SetExtGState(state);

                        var imageHeight = _idHeaderFirst.GetHeight() * 72 / 96; //convert pixels to points
                        var imageToAdd = new Image(_idHeaderFirst, 0, pageSize.GetTop() - imageHeight, pageSize.GetWidth());

                        imageToAdd.GetAccessibilityProperties()
                            .SetAlternateDescription("Background header image");
                        imageToAdd.GetAccessibilityProperties().SetRole(StandardRoles.ARTIFACT);

                        PdfLayer pdflayer = new PdfLayer("main layer", pdfDoc);
                        
                        pdflayer.SetOn(true);
                        Canvas canvas = new Canvas(pdfCanvas, pdfDoc,
                            document.GetPageEffectiveArea(pdfDoc.GetDefaultPageSize()));
                        pdflayer.SetPageElement("L");
                        pdfCanvas.BeginLayer(pdflayer);
                        canvas.EnableAutoTagging(page);
                        canvas.Add(imageToAdd);
                        pdfCanvas.EndLayer();
                        
                    }

                    if (_docOptions.HasFlag(DocConverter.DocOptions.DisplayTitle))
                    {
                        //Add Title
                        Color purpleColor = new DeviceRgb(85, 60, 116);
                        TagTreePointer tagPointer = new TagTreePointer(pdfDoc);
                        tagPointer.SetPageForTagging(page);
                        tagPointer.AddTag(StandardRoles.TITLE);
                        pdfCanvas.BeginText().SetColor(purpleColor, true).SetFontAndSize(font, 28)
                            .MoveText(42, pageSize.GetTop() - 150).OpenTag(tagPointer.GetTagReference()).ShowText(_title).CloseTag().Stroke();
                    }

                }
                else if (_docOptions.HasFlag(DocConverter.DocOptions.AddHeaderAllPages))
                {

                    pdfCanvas.SaveState();
                    PdfExtGState state = new PdfExtGState();
                    state.SetFillOpacity(1.0f);
                    pdfCanvas.SetExtGState(state);
                    var imageHeight = _idHeaderAll.GetHeight() * 72 / 96; //convert pixels to points
                    var imageToAdd = new Image(_idHeaderAll, 0, pageSize.GetTop() - imageHeight, pageSize.GetWidth());
                    imageToAdd.GetAccessibilityProperties().SetRole(StandardRoles.ARTIFACT);
                    imageToAdd.GetAccessibilityProperties()
                        .SetAlternateDescription("Background header image");
                    PdfLayer pdflayer = new PdfLayer("main layer", pdfDoc);

                    pdflayer.SetOn(true);

                    Canvas canvas = new Canvas(pdfCanvas, pdfDoc,
                        document.GetPageEffectiveArea(pdfDoc.GetDefaultPageSize()));

                    pdfCanvas.BeginLayer(pdflayer);
                    canvas.EnableAutoTagging(page);
                    canvas.Add(imageToAdd);
                    pdfCanvas.EndLayer();
                    pdfCanvas.RestoreState();
                }

                if (_docOptions.HasFlag(DocConverter.DocOptions.AddLineBottomEachPage))
                {
                    //Add line to the bottom
                    Color blueColor = new DeviceCmyk(100, 25, 0, 39);

                    pdfCanvas.SetStrokeColor(blueColor)
                        .MoveTo(36, 36)
                        .LineTo(559, 36)
                        .ClosePathStroke();
                }

               
                pdfCanvas.Release();

            }

        }
         
    }
}
