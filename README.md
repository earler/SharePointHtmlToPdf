# SharePointHtmlToPdf
SharePoint provider-hosted app solution which allows creation of accessible PDFs using iText and iText7.pdfhtml
This solution was created from the Visual Studio "SharePoint Provider-hosted App" template, and I've left files in there that were included as default even if I've not used them.  

To use it, you need to deploy the code, package the app, add it to your catalogue, and then add it to your SharePoint site.  On installing, it will create a list called HTMLToPDF and attached an ItemAdded Remote Event Receiver. Whenever an item is added to the list, an accessible PDF will be created using the HTML you have provided, and this PDF will be added to a document library.  If it succeeds, the list item in HtmlToPDF will be deleted.  If it fails, it will add the error message to the item in the ErrorMessage column.  Here's some example code you can use in your own provider-hosted app.  This is assuming you're using C# / Web Forms and a button click event:
```
 protected void btnGenerateReport_Click(object sender, EventArgs e)
        {

            try
            {
                string reportPage =  "Report.aspx";
                
                string url = reportPage + "?" + m_urlTokens;
                
                string fileName = "MyReport.pdf";
                //Get the HTML from a page in this project
                HttpContext Context = HttpContext.Current;
                StringWriter sw = new StringWriter();
                Context.Server.Execute(url, sw);

                //Create new list item.  PDF creation handled by app.

                SPC.List listHtml = m_clientContext.Web.Lists.GetByTitle("HtmlToPDF");
                SPC.ListItemCreationInformation itemCreateInfo = new SPC.ListItemCreationInformation();
                var newPdf = listHtml.AddItem(itemCreateInfo);
                newPdf["Title"] = "Doc title";
                newPdf["HtmlToConvert"] = sw.ToString();
                newPdf["DocFileName"] = fileName;
                newPdf["DocLibraryName"] = "My document library";
                newPdf["FolderName"] = "Folder for document";
                //If the doc lib has extra meta data columns, the values can be entered here. The pair values are separated by ~ and the items by #, e.g. "DocType~My Doc Type#LookupId~13"
                newPdf["DocMetaData"] = "DocumentType~Public report";
                //Options for the PDF.  Bitwise flag, so add up the ones you want
                //None = 0,
                //DisplayTitle = 1,
                //AddHeaderPageOne = 2,
                //AddHeaderAllPages = 4,
                //AddLineBottomEachPage = 8
                newPdf["ConversionOptions"] = 11;
                newPdf.Update();
                m_clientContext.ExecuteQuery();

            }
            catch (Exception ex)
            {
                errorMessage.Text = "There was an error generating the report. " + ex.Message;
                return;
            }
            


        }

//In your page init, this is where you can instantiate the client context and url Tokens if needed:
        protected string m_urlTokens=string.Empty;
        protected ClientContext m_clientContext;
        protected SharePointContext m_spContext;
        protected string m_appWebUrl = string.Empty;
        protected string m_hostWebUrl = string.Empty;
        protected void Page_InitComplete(object sender, EventArgs e)
        {
            m_spContext = SharePointContextProvider.Current.GetSharePointContext(Context);
            m_clientContext = m_spContext.CreateUserClientContextForSPHost();
           
            //Get URL tokens
            foreach(var param in Request.QueryString.AllKeys)
            {
                if (!string.IsNullOrEmpty(param) && param.StartsWith("SP"))
                {
                    m_urlTokens += param + "=" + HttpUtility.UrlEncode(Request.QueryString[param]) + "&";
                }

            }
            //remove last ampersand
            if (m_urlTokens != string.Empty && m_urlTokens.EndsWith("&"))
                m_urlTokens = m_urlTokens.Substring(0, m_urlTokens.Length - 1);

            m_appWebUrl = SharePointConfig.Instance.RootAppURL;
            m_hostWebUrl = SharePointConfig.Instance.RootWebURL;
        }

```