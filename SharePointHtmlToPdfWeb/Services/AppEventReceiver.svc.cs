using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.EventReceivers;

namespace SharePointHtmlToPdfWeb.Services
{
    public class AppEventReceiver : IRemoteEventService
    {
        const string ListHtmlToPdf =  "HtmlToPDF";
        /// <summary>
        /// Handles app events that occur after the app is installed or upgraded, or when app is being uninstalled.
        /// </summary>
        /// <param name="properties">Holds information about the app event.</param>
        /// <returns>Holds information returned from the app event.</returns>
        public SPRemoteEventResult ProcessEvent(SPRemoteEventProperties properties)
        {
            SPRemoteEventResult result = new SPRemoteEventResult();
            switch (properties.EventType)
            {
                case SPRemoteEventType.AppInstalled:
                    HandleAppInstalled(properties);
                    break;
                case SPRemoteEventType.AppUninstalling:
                    HandleAppUninstalling(properties);
                    break;

                default:
                    return result;
            }


            return result;
        }

        /// <summary>
        /// This method is a required placeholder, but is not used by app events.
        /// </summary>
        /// <param name="properties">Unused.</param>
        public void ProcessOneWayEvent(SPRemoteEventProperties properties)
        {
            switch (properties.EventType)
            {
                case SPRemoteEventType.ItemAdded:
                    switch (properties.ItemEventProperties.ListTitle)
                    {
                        case "HtmlToPDF":
                            HtmlToPdfItemAdded(properties);
                            break;

                    }

                    break;
               
            }

        }

        private void HandleAppInstalled(SPRemoteEventProperties properties)
        {
            using (ClientContext clientContext =
                TokenHelper.CreateAppEventClientContext(properties, false))
            {
                if (clientContext != null)
                {
                    //Check for fields that need adding
                    Web rootWeb = clientContext.Site.RootWeb;
                    clientContext.Load(rootWeb, w => w.Fields);
                    clientContext.ExecuteQuery();
                    ListCreationInformation listCreationInfo = new ListCreationInformation();
                    listCreationInfo.Title = ListHtmlToPdf;
                    listCreationInfo.TemplateType = (int)ListTemplateType.GenericList;

                    List oList = rootWeb.Lists.Add(listCreationInfo);
                    clientContext.ExecuteQuery();
                    //Title included by default.
                    //Field for HTML
                    Field oField = oList.Fields.AddFieldAsXml("<Field DisplayName='HtmlToConvert' Name='HtmlToConvert' Type='Note' RichText='FALSE' />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for FileName for resulting PDF
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='DocFileName' Name='DocFileName' Type='Text'  />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for doc library name for resulting PDF
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='DocLibraryName' Name='DocLibraryName' Type='Text'  />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for folder name for resulting PDF
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='FolderName' Name='FolderName' Type='Text'  />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for metadata
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='DocMetaData' Name='DocMetaData' Description='Name value pairs' Type='Note' RichText='FALSE' />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for PDF options
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='ConversionOptions' Name='ConversionOptions' Description='Bitwise numeric value' Type='Number' />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    //Field for error if conversion fails
                    oField = oList.Fields.AddFieldAsXml("<Field DisplayName='ErrorMessage' Name='ErrorMessage' Type='Note' RichText='FALSE' />",
                        true, AddFieldOptions.AddFieldInternalNameHint);
                    clientContext.ExecuteQuery();

                    //Add event receiver

                    EventReceiverDefinitionCreationInformation receiver = null;
                    receiver = new EventReceiverDefinitionCreationInformation
                    {
                        EventType = EventReceiverType.ItemAdded,
                        Synchronization = EventReceiverSynchronization.Asynchronous

                    };
                    OperationContext op = OperationContext.Current;
                    Message msg = op.RequestContext.RequestMessage;

                    receiver.ReceiverUrl = msg.Headers.To.ToString();

                    receiver.ReceiverName = ListHtmlToPdf + "-ListItemAddedEvent";
                    oList.EventReceivers.Add(receiver);

                    clientContext.ExecuteQuery();

                    System.Diagnostics.Trace.WriteLine("Added ItemAdded receiver at "
                                                       + msg.Headers.To.ToString());
                 
                }
            }
        }

        private void HandleAppUninstalling(SPRemoteEventProperties properties)
        {
            using (ClientContext clientContext =
                TokenHelper.CreateAppEventClientContext(properties, false))
            {
                if (clientContext != null)
                {
                    Web oWebsite = clientContext.Web;

                    List oList = oWebsite.Lists.GetByTitle(ListHtmlToPdf);

                    oList.DeleteObject();

                    clientContext.ExecuteQuery();
                }
            }
        }
        //Runs when item added to list
        private void HtmlToPdfItemAdded(SPRemoteEventProperties properties)
        {

            using (ClientContext clientContext =
                TokenHelper.CreateRemoteEventReceiverClientContext(properties))
            {
                if (clientContext != null)
                {
                    try
                    {

                        clientContext.Load(clientContext.Web, w => w.Url);
                        List listHtmlToPdf = clientContext.Web.Lists.GetByTitle(ListHtmlToPdf);
                        ListItem listItem = listHtmlToPdf.GetItemById(properties.ItemEventProperties.ListItemId);
                        clientContext.Load(listItem);
                        clientContext.ExecuteQuery();
                        try
                        {
                            var pdfDoc = DocConverter.ConvertToPdfWithTags(listItem["HtmlToConvert"].ToString(), listItem["Title"]+"", listItem["ConversionOptions"]+"");
                            
                            AddDocToLibrary(clientContext, pdfDoc, listItem["DocLibraryName"].ToString(),
                                listItem["FolderName"].ToString(), listItem["DocFileName"].ToString(),
                                listItem["DocMetaData"]+"");
                            listItem.DeleteObject();
                            clientContext.ExecuteQuery();
                        }
                        catch (Exception ex)
                        {
                            listItem["ErrorMessage"] = ex.ToString();
                            listItem.Update();
                            clientContext.ExecuteQuery();
                        }

                    }
                    catch (Exception oops)
                    {
                        System.Diagnostics.Trace.WriteLine(oops.Message);
                    }
                }

            }

        }
        public static string AddDocToLibrary(ClientContext clientContext, byte[] fileToAdd, string docLibraryName, string folderName, string fileName,
           string metaData)
        {
            List lstDocs = clientContext.Web.Lists.GetByTitle(docLibraryName);
            clientContext.Load(lstDocs.RootFolder);
            clientContext.ExecuteQuery();
            //Add a folder to library

            CheckFolderInList(clientContext, lstDocs, docLibraryName, folderName, true);
            string destFileUrl = lstDocs.RootFolder.ServerRelativeUrl + "/" + folderName + "/" + fileName;
            var fileCreationInformation = new FileCreationInformation();
            //Assign to content byte[] i.e. documentStream

            fileCreationInformation.Content = fileToAdd;
            //Allow owerwrite of document

            fileCreationInformation.Overwrite = true;

            fileCreationInformation.Url = destFileUrl;
            File uploadFile = lstDocs.RootFolder.Files.Add(fileCreationInformation);
            //Metadata holds name pair values.  The pair values are separated by ~ and the items by #, e.g. "DocType~My Doc Type#LookupId~13"
            if (!string.IsNullOrEmpty(metaData))
            {
                var metaPairs = metaData.Split('#');
                foreach (var metaPair in metaPairs)
                {
                    var pairArray = metaPair.Split('~');
                    if(pairArray.Length != 2)
                        continue;

                    var columnName = pairArray[0];
                    var columnValue = pairArray[1];
                    uploadFile.ListItemAllFields[columnName] = columnValue;
                }
                uploadFile.ListItemAllFields.Update();
               clientContext.ExecuteQuery();
            }

            return destFileUrl;

        }
        public static void CheckFolderInList(ClientContext clientContext, List list, string listTitle,
            string folderName, bool isDocLibrary = false)
        {
            var exists = false;
            Web web = clientContext.Web;
            try
            {
                if (!web.IsPropertyAvailable(nameof(web.ServerRelativeUrl)))
                {
                    clientContext.Load(web, w => w.ServerRelativeUrl);
                    clientContext.ExecuteQuery();
                }

                string fullFolderPath = string.Empty;
                if (isDocLibrary)
                    fullFolderPath = web.ServerRelativeUrl + "/" + listTitle + "/" + folderName;
                else
                    fullFolderPath = web.ServerRelativeUrl + "/Lists/" + listTitle + "/" + folderName;

                var file = clientContext.Web.GetFolderByServerRelativeUrl(fullFolderPath);
                clientContext.Load(file, f => f.Exists); // Only load the Exists property
                clientContext.ExecuteQuery();
                exists = file.Exists;
            }
            catch (Exception)
            {
            }

            if (!exists)
            {
                //Create
                ListItemCreationInformation itemCreateInfo = new ListItemCreationInformation();
                itemCreateInfo.UnderlyingObjectType = FileSystemObjectType.Folder;
                itemCreateInfo.LeafName = folderName;
                ListItem fNew = list.AddItem(itemCreateInfo);

                fNew["Title"] = folderName;
                fNew.Update();
                clientContext.ExecuteQuery();
            }


        }

    }
}
