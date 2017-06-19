using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
            var thumbContainer = client.GetContainerReference("uploadthumb");
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            thumbContainer.CreateIfNotExists();
            thumbContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            //save to azure storage
            var blob = container.GetBlockBlobReference(fName);
            using (var stream = model.File.InputStream)
            {
                stream.Position = 0;
                blob.UploadFromStream(stream);
            }

            //make sure queue exists
            var nsm = NamespaceManager.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"]);
            if (!nsm.QueueExists("resize"))
            {
                nsm.CreateQueue("resize");
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
            var thumbContainer = client.GetContainerReference("uploadthumb");
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            thumbContainer.CreateIfNotExists();
            thumbContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

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
            //make sure topic and sub exist
            var nsm = NamespaceManager.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"]);
            if (!nsm.TopicExists("process"))
            {
                nsm.CreateTopic("process");
            }
            if (!nsm.SubscriptionExists("process", "save"))
            {
                var saveFilter = new SqlFilter("Action = 'save'");
                nsm.CreateSubscription("process", "save", saveFilter);
            }
            if (!nsm.SubscriptionExists("process", "delete"))
            {
                var deleteFilter = new SqlFilter("Action = 'delete'");
                nsm.CreateSubscription("process", "delete", deleteFilter);
            }

            //add message to save topic
            var topicClient =
                TopicClient.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"], "process");
            var msg = new BrokeredMessage
            {
                Label = id
            };
            msg.Properties["Action"] = "save";
            topicClient.Send(msg);

            return RedirectToAction("Saved");
        }

        public ActionResult Delete(string id)
        {
            //make sure topic and sub exist
            var nsm = NamespaceManager.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"]);
            if (!nsm.TopicExists("process"))
            {
                nsm.CreateTopic("process");
            }
            if (!nsm.SubscriptionExists("process", "save"))
            {
                var saveFilter = new SqlFilter("Action = 'save'");
                nsm.CreateSubscription("process", "save", saveFilter);
            }
            if (!nsm.SubscriptionExists("process", "delete"))
            {
                var deleteFilter = new SqlFilter("Action = 'delete'");
                nsm.CreateSubscription("process", "delete", deleteFilter);
            }

            //add message to delete subscription on topic
            var topicClient =
                TopicClient.CreateFromConnectionString(ConfigurationManager.AppSettings["ServiceBusConnection"], "process");
            var msg = new BrokeredMessage
            {
                Label = id
            };
            msg.Properties["Action"] = "delete";
            topicClient.Send(msg);

            return RedirectToAction("Manage");
        }

        public ActionResult Saved()
        {
            var model = new List<UploadedImageViewModel>();

            var acct = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["AzureWebJobsStorage"]);
            var client = acct.CreateCloudBlobClient();
            var container = client.GetContainerReference("saved");
            var thumbContainer = client.GetContainerReference("savedthumb");
            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
            thumbContainer.CreateIfNotExists();
            thumbContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

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