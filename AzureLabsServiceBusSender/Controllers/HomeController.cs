using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Web.Mvc;
using AzureLabsServiceBusSender.Models;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureLabsServiceBusSender.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            //create containers
            var acct = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var client = acct.CreateCloudBlobClient();
            var uploadContainer = client.GetContainerReference("upload");
            var uploadThumbContainer = client.GetContainerReference("uploadthumb");
            uploadContainer.CreateIfNotExists();
            uploadContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            uploadThumbContainer.CreateIfNotExists();
            uploadThumbContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            var savedContainer = client.GetContainerReference("saved");
            var savedThumbContainer = client.GetContainerReference("savedthumb");
            savedContainer.CreateIfNotExists();
            savedContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            savedThumbContainer.CreateIfNotExists();
            savedThumbContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            //create queue, topic and subs
            //subs need to be recreated because functions create them without filters
            var nsm = NamespaceManager.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"]);
            if (!nsm.QueueExists("resize"))
            {
                nsm.CreateQueue("resize");
            }
            if (!nsm.TopicExists("process"))
            {
                nsm.CreateTopic("process");
            }
            if (nsm.SubscriptionExists("process", "save")) nsm.DeleteSubscription("process", "save");
            var saveFilter = new SqlFilter("Action = 0");
            nsm.CreateSubscription("process", "save", saveFilter);
            if(nsm.SubscriptionExists("process", "delete")) nsm.DeleteSubscription("process", "delete");
            var deleteFilter = new SqlFilter("Action = 1");
            nsm.CreateSubscription("process", "delete", deleteFilter);

            return View();
        }

        [HttpGet]
        public ActionResult Upload()
        {
            var model = new ImageUploadViewModel();
            return View(model);
        }

        [HttpPost]
        public ActionResult Upload(ImageUploadViewModel model)
        {
            if(!ModelState.IsValid) return View(model);

            //upload to storage
            var fName = Path.GetFileName(model.File.FileName);

            var acct = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var client = acct.CreateCloudBlobClient();
            var container = client.GetContainerReference("upload");

            //save to azure storage
            var blob = container.GetBlockBlobReference(fName);
            using (var stream = model.File.InputStream)
            {
                stream.Position = 0;
                blob.Properties.ContentType = "image/jpeg";
                blob.UploadFromStream(stream);
            }

            //add msg to queue
            var queueClient =
                QueueClient.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"], "resize");
            var msg = new BrokeredMessage
            {
                Label = fName
            };
            queueClient.Send(msg);

            return RedirectToAction("Manage");
        }

        public ActionResult Manage()
        {
            var model = new List<UploadedImageViewModel>();

            //access storage; create containers if they don't exist
            var acct = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var client = acct.CreateCloudBlobClient();
            var container = client.GetContainerReference("upload");

            //get all the blobs in the upload container
            foreach (var item in container.ListBlobs())
            {
                var blob = (CloudBlockBlob) item;
                var tmp = new UploadedImageViewModel
                {
                    ImageName = blob.Name,
                    ImageUrl = blob.StorageUri.PrimaryUri.ToString(),
                    ThumbnailUrl = blob.StorageUri.PrimaryUri.ToString().Replace("upload", "uploadthumb")
                };

                //use "no image" placeholder if there's no thumbnail yet
                if (!client.GetContainerReference("uploadthumb").GetBlockBlobReference(tmp.ImageName).Exists())
                    tmp.ThumbnailUrl = "/img/noimage.jpg";
                model.Add(tmp);
            }

            return View(model);
        }

        public ActionResult Save(string id)
        {
            //add message to save topic
            var topicClient =
                TopicClient.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"], "process");
            var msg = new BrokeredMessage
            {
                Label = id
            };
            msg.Properties.Add("Action", 0);
            topicClient.Send(msg);

            Thread.Sleep(2000);
            return RedirectToAction("Saved");
        }

        public ActionResult Delete(string id)
        {
            //add message to delete subscription on topic
            var topicClient =
                TopicClient.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"], "process");
            var msg = new BrokeredMessage
            {
                Label = id
            };
            msg.Properties.Add("Action", 1);
            topicClient.Send(msg);

            Thread.Sleep(2000);
            return RedirectToAction("Manage");
        }

        public ActionResult Saved()
        {
            var model = new List<UploadedImageViewModel>();

            var acct = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var client = acct.CreateCloudBlobClient();
            var container = client.GetContainerReference("saved");

            foreach (var item in container.ListBlobs())
            {
                var blob = (CloudBlockBlob)item;
                var tmp = new UploadedImageViewModel
                {
                    ImageName = blob.Name,
                    ImageUrl = blob.StorageUri.PrimaryUri.ToString(),
                    ThumbnailUrl = blob.StorageUri.PrimaryUri.ToString().Replace("saved", "savedthumb")
                };

                if (!client.GetContainerReference("savedthumb").GetBlockBlobReference(tmp.ImageName).Exists())
                    tmp.ThumbnailUrl = "/img/noimage.jpg";
                model.Add(tmp);
            }

            return View(model);
        }
    }
}