using System;
using System.Web.Mvc;
using Syncfusion.JavaScript;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO.Packaging;
using ICSharpCode.SharpZipLib.Zip;



namespace SyncfusionMvcApplication7
{
    public partial class FileExplorerController : Controller
    {
        public ActionResult FileExplorerFeatures()
        {
            return View();
        }

        
        public ActionResult FileActionDefault(AzureFileExplorerParams args)
        {
            //Please specify the path of azure blob
            string startPath = "https://filebrowsercontent.blob.core.windows.net/blob1/";

            if (args.Path != null)
                args.Path = args.Path.Replace(startPath, "");
            if (args.LocationFrom != null)
                args.LocationFrom = args.LocationFrom.Replace(startPath, "");
            if (args.LocationTo != null)
                args.LocationTo = args.LocationTo.Replace(startPath, "");

            //Here you have to specify the azure account name, key and blob name
            AzureFileOperations operation = new AzureFileOperations("filebrowsercontent", "rbAvmn82fmt7oZ7N/3SXQ9+d9MiQmW2i1FzwAtPfUJL9sb2gZ/+cC6Ei1mkwSbMA1iVSy9hzH1unWfL0fPny0A==", "blob1");

            switch (args.ActionType)
            {
                case "Read":
                    return Json(operation.Read(args.Path, args.ExtensionsAllow));
                case "CreateFolder":
                    return Json(operation.CreateFolder(args.Path, args.Name));
                case "Paste":
                    return Json(operation.Paste(args.LocationFrom, args.LocationTo, args.Names, args.Action, args.CommonFiles, args.SelectedItems));
                case "Remove":
                    return Json(operation.Remove(args.Names, args.Path, args.SelectedItems));
                case "Rename":
                    return Json(operation.Rename(args.Path, args.Name, args.NewName, args.CommonFiles, args.SelectedItems));
                case "GetDetails":
                    return Json(operation.GetDetails(args.Path, args.Names, args.SelectedItems));
                case "Download":
                    operation.Download(args.Path, args.Names);
                    break;
                case "Upload":
                    operation.Upload(args.FileUpload, args.Path);
                    break;
                case "Search":
                    return Json(operation.Search(args.Path, args.ExtensionsAllow, args.SearchString, args.CaseSensitive));
            }
            return Json("");
        }
    }
}
namespace Syncfusion.JavaScript
{
    public class AzureFileOperations : BasicFileOperations
    {
        List<FileExplorerDirectoryContent> Items = new List<FileExplorerDirectoryContent>();
        public CloudBlobContainer container;
        public AzureFileOperations(string accountName, string accountKey, string blobName)
        {
            StorageCredentials creds = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);
            CloudBlobClient client = account.CreateCloudBlobClient();
            container = client.GetContainerReference(blobName);
        }
        public override object Read(string path, string filter, IEnumerable<object> selectedItems = null)
        {
            AjaxFileExplorerResponse ReadResponse = new AjaxFileExplorerResponse();
            List<FileExplorerDirectoryContent> details = new List<FileExplorerDirectoryContent>();
            try
            {
                filter = filter.Replace(" ", "");
                var extensions = (filter ?? "*").Split(",|;".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
                IEnumerable<IListBlobItem> items = sampleDirectory.ListBlobs(false, BlobListingDetails.Metadata);

                foreach (IListBlobItem item in items)
                {
                    bool canAdd = false;
                    if (extensions[0].Equals("*.*") || extensions[0].Equals("*"))
                        canAdd = true;
                    else if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob file = (CloudBlockBlob)item;
                        var names = file.Name.ToString().Trim().Split('.');
                        if (Array.IndexOf(extensions, "*." + names[names.Count() - 1]) >= 0)
                            canAdd = true;
                        else canAdd = false;
                    }
                    else
                        canAdd = true;
                    if (canAdd)
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob file = (CloudBlockBlob)item;
                            FileExplorerDirectoryContent entry = new FileExplorerDirectoryContent();
                            entry.name = file.Name.Replace(path, "").Replace("/", "");
                            entry.type = file.Properties.ContentType;
                            entry.isFile = true;
                            entry.size = file.Properties.Length;
                            entry.dateModified = file.Properties.LastModified.Value.LocalDateTime.ToString();
                            entry.hasChild = false;
                            entry.filterPath = "";
                            details.Add(entry);
                        }
                        else if (item.GetType() == typeof(CloudBlobDirectory))
                        {
                            CloudBlobDirectory directory = (CloudBlobDirectory)item;
                            FileExplorerDirectoryContent entry = new FileExplorerDirectoryContent();
                            entry.name = directory.Prefix.Replace(path, "").Replace("/", "");
                            entry.type = "Directory";
                            entry.isFile = false;
                            entry.size = 0;
                            //entry.dateModified = directory.Properties.LastModified.ToString();
                            entry.hasChild = HasChildDirectory(directory.Prefix);
                            entry.filterPath = "";
                            details.Add(entry);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ReadResponse.error = e.GetType().FullName + ", " + e.Message;
                return ReadResponse;
            }
            ReadResponse.files = (IEnumerable<FileExplorerDirectoryContent>)details;
            return ReadResponse;
        }

        public virtual void getAllFiles(string path, AjaxFileExplorerResponse data, string filter)
        {
            AjaxFileExplorerResponse directoryList = new AjaxFileExplorerResponse();
            directoryList.files = (IEnumerable<FileExplorerDirectoryContent>)data.files.Where(item => item.isFile == false);
            for (int i = 0; i < directoryList.files.Count(); i++)
            {

                IEnumerable<FileExplorerDirectoryContent> selectedItem = new[] { directoryList.files.ElementAt(i) };
                AjaxFileExplorerResponse innerData = (AjaxFileExplorerResponse)Read(path + directoryList.files.ElementAt(i).name + "/", filter, selectedItem);
                innerData.files = innerData.files.Select(file => new FileExplorerDirectoryContent
                {
                    name = file.name,
                    type = file.type,
                    isFile = file.isFile,
                    size = file.size,
                    hasChild = file.hasChild,
                    filterPath = (directoryList.files.ElementAt(i).filterPath + directoryList.files.ElementAt(i).name + "\\")
                });
                Items.AddRange(innerData.files);
                getAllFiles(path + directoryList.files.ElementAt(i).name + "/", innerData, filter);
            }
        }


        private bool HasChildDirectory(string path)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            var items = sampleDirectory.ListBlobs(false, BlobListingDetails.None);
            foreach (var item in items)
            {
                if (item.GetType() == typeof(CloudBlobDirectory))
                {
                    return true;
                }
            }
            return false;
        }


        public override object Search(string path, string filter, string searchString, bool caseSensitive, System.Collections.Generic.IEnumerable<object> selectedItems = null)
        {
            Items.Clear();
            AjaxFileExplorerResponse data = (AjaxFileExplorerResponse)Read(path, filter, selectedItems);
            Items.AddRange(data.files);
            getAllFiles(path, data, filter);
            data.files = Items.Where(item => new Regex(WildcardToRegex(searchString), (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)).IsMatch(item.name));
            return data;
        }

        public override object CreateFolder(string path, string name, IEnumerable<object> selectedItems = null)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(path + name + "/temp.$$$");
            blob.UploadText(".");
            AjaxFileExplorerResponse CreateResponse = new AjaxFileExplorerResponse();
            FileExplorerDirectoryContent content = new FileExplorerDirectoryContent();
            content.name = name;
            var directories = new[] { content };
            CreateResponse.files = (IEnumerable<FileExplorerDirectoryContent>)directories;
            return CreateResponse;
        }

        public override void Download(string path, string[] names, IEnumerable<object> selectedItems = null)
        {
            HttpResponse response = HttpContext.Current.Response;
            if (names.Length > 1)
            {
                using (var zipOutputStream = new ZipOutputStream(response.OutputStream))
                {
                    foreach (var blobFileName in names)
                    {
                        zipOutputStream.SetLevel(0);
                        var blob = container.GetBlockBlobReference(path + blobFileName);
                        var entry = new ZipEntry(blobFileName);
                        zipOutputStream.PutNextEntry(entry);
                        blob.DownloadToStream(zipOutputStream);
                    }
                    zipOutputStream.Finish();
                    zipOutputStream.Close();
                }
                response.BufferOutput = false;
                response.AddHeader("Content-Disposition", "attachment; filename=" + "Files.zip");
                response.ContentType = "application/octet-stream";
                response.Flush();
                response.End();                
            }
            else
            {
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + names[0]);                
                System.Net.WebClient net = new System.Net.WebClient();
                string link = blockBlob.Uri.ToString();
                response.ClearHeaders();
                response.Clear();
                response.Expires = 0;
                response.Buffer = true;
                response.AddHeader("Content-Disposition", "Attachment;FileName=" + names[0]);
                response.ContentType = blockBlob.Properties.ContentType;
                response.BinaryWrite(net.DownloadData(link));
                response.End();
            }            
        }       

        public override object GetDetails(string path, string[] names, IEnumerable<object> selectedItems = null)
        {
            AjaxFileExplorerResponse DetailsResponse = new AjaxFileExplorerResponse();
            try
            {
                bool isFile = false;
                AzureFileDetails[] fDetails = new AzureFileDetails[names.Length];
                AzureFileDetails fileDetails = new AzureFileDetails();
                if (selectedItems!= null)
                {
                    foreach (FileExplorerDirectoryContent item in selectedItems)
                    {
                        isFile = item.isFile;
                        break;
                    }
                }                
                if (isFile)
                {
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + names[0]);
                    blockBlob.FetchAttributes();
                    fileDetails.Name = blockBlob.Name;
                    fileDetails.Extension = blockBlob.Name.Split('.')[1];
                    fileDetails.FullName = blockBlob.Uri.ToString();
                    fileDetails.Format = blockBlob.Properties.ContentType.ToString();
                    fileDetails.Length = blockBlob.Properties.Length;
                    fileDetails.LastWriteTime = blockBlob.Properties.LastModified.Value.LocalDateTime.ToString();
                }
                else
                {
                    CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
                    fileDetails.Name = names[0];
                    fileDetails.FullName = sampleDirectory.Uri.ToString() + names[0];
                    fileDetails.Format = sampleDirectory.GetType().ToString();
                    fileDetails.Length = 0;
                }
                fDetails[0] = fileDetails;
                DetailsResponse.details = fDetails;
                return DetailsResponse;
            }
            catch (Exception ex) { throw ex; }
        }

        public override void GetImage(string path, IEnumerable<object> selectedItems = null)
        {
            throw new NotImplementedException();
        }

        public override void GetImage(string path, bool canCompress = false, ImageSize size = null, IEnumerable<object> selectedItems = null)
        {
            throw new NotImplementedException();
        }

        public override object Paste(string sourceDir, string targetDir, string[] names, string option, IEnumerable<CommonFileDetails> commonFiles, IEnumerable<object> selectedItems = null, IEnumerable<object> targetFolder = null)
        {
            foreach (FileExplorerDirectoryContent s_item in selectedItems)
            {
                if (s_item.isFile)
                {
                    string sourceDir1 = sourceDir + s_item.name;
                    CloudBlob existBlob = container.GetBlobReference(sourceDir1);
                    CloudBlob newBlob = container.GetBlobReference(targetDir + s_item.name);
                    newBlob.StartCopy(existBlob.Uri);
                    if (option == "move")
                        existBlob.DeleteIfExists();
                }
                else
                {
                    CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(sourceDir + s_item.name);
                    var items = sampleDirectory.ListBlobs(true, BlobListingDetails.None);
                    foreach (var item in items)
                    {
                        string name = item.Uri.ToString().Replace(sampleDirectory.Uri.ToString(), "");
                        CloudBlob newBlob = container.GetBlobReference(targetDir + s_item.name + "/" + name);
                        newBlob.StartCopy(item.Uri);
                        if (option == "move")
                            container.GetBlobReference(sourceDir + s_item.name + "/" + name).Delete();
                    }
                }
            }
            return "success";
        }


        public override object Remove(string[] names, string path, IEnumerable<object> selectedItems = null)
        {
            CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path);
            foreach (FileExplorerDirectoryContent s_item in selectedItems)
            {
                if (s_item.isFile)
                {
                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + s_item.name);
                    blockBlob.Delete();
                }
                else
                {
                    CloudBlobDirectory subDirectory = container.GetDirectoryReference(path + s_item.name);
                    var items = subDirectory.ListBlobs(true, BlobListingDetails.None);
                    foreach (var item in items)
                    {
                        CloudBlockBlob blockBlob = container.GetBlockBlobReference(path + s_item.name + "/" + item.Uri.ToString().Replace(subDirectory.Uri.ToString(), ""));
                        blockBlob.Delete();
                    }
                }
            }
            return "success";
        }

        public override object Rename(string path, string oldName, string newName, IEnumerable<CommonFileDetails> commonFiles, IEnumerable<object> selectedItems = null)
        {
            bool isFile = false;
            foreach (FileExplorerDirectoryContent item in selectedItems)
            {
                isFile = item.isFile;
                break;
            }
            if (isFile)
            {
                CloudBlob existBlob = container.GetBlobReference(path + oldName);
                CloudBlob newBlob = container.GetBlobReference(path + newName);
                newBlob.StartCopy(existBlob.Uri);
                existBlob.DeleteIfExists();
            }
            else
            {
                CloudBlobDirectory sampleDirectory = container.GetDirectoryReference(path + oldName);
                var items = sampleDirectory.ListBlobs(true, BlobListingDetails.Metadata);
                foreach (var item in items)
                {
                    string name = item.Uri.AbsolutePath.Replace(sampleDirectory.Uri.AbsolutePath, "");
                    CloudBlob newBlob = container.GetBlobReference(path + newName + "/" + name);
                    newBlob.StartCopy(item.Uri);                  
                    container.GetBlobReference(path + oldName + "/" + name).Delete();
                }

            }
            return "success";
        }
        public virtual void SaveFile(string path, MemoryStream fileStream)
        {
            try
            {
                CloudBlockBlob blob = container.GetBlockBlobReference(path);                
                blob.UploadFromStream(fileStream);
            }
            catch (Exception ex) { throw ex; }
        }

        public override void Upload(IEnumerable<HttpPostedFileBase> files, string path, IEnumerable<object> selectedItems = null)
        {
            try
            {
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    CloudBlockBlob blob = container.GetBlockBlobReference(path + fileName);
                    blob.UploadFromStream(file.InputStream);
                }
            }
            catch (Exception ex) { throw ex; }
        }
    }
    public class AzureFileDetails
    {
        public string CreationTime { get; set; }
        public string Extension { get; set; }
        public string Format { get; set; }
        public string FullName { get; set; }
        public string LastAccessTime { get; set; }
        public string LastWriteTime { get; set; }
        public long Length { get; set; }
        public string Name { get; set; }
    }
    public class AjaxFileExplorerResponse
    {
        public FileExplorerDirectoryContent cwd { get; set; }
        public IEnumerable<FileExplorerDirectoryContent> files { get; set; }
        public IEnumerable<AzureFileDetails> details { get; set; }
        public object error { get; set; }
    }

    public class AzureFileExplorerParams
    {
        public string Action { get; set; }
        public string ActionType { get; set; }
        public bool CaseSensitive { get; set; }
        public IEnumerable<CommonFileDetails> CommonFiles { get; set; }
        public string ExtensionsAllow { get; set; }
        public IEnumerable<System.Web.HttpPostedFileBase> FileUpload { get; set; }
        public string LocationFrom { get; set; }
        public string LocationTo { get; set; }
        public string Name { get; set; }
        public string[] Names { get; set; }
        public string NewName { get; set; }
        public string Path { get; set; }
        public string PreviousName { get; set; }
        public string SearchString { get; set; }
        public IEnumerable<FileExplorerDirectoryContent> SelectedItems { get; set; }
        public IEnumerable<FileExplorerDirectoryContent> TargetFolder { get; set; }
    }
}