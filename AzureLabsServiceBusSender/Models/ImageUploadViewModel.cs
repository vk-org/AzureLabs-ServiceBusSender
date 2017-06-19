using System.ComponentModel;
using System.Web;

namespace AzureLabsServiceBusSender.Models
{
    public class ImageUploadViewModel
    {
        [DisplayName("Select JPG to upload (under 5 MB)")]
        [UploadedFileValidation(ErrorMessage = "Please upload a JPG file under 5 MB in size.")]
        public HttpPostedFileBase File { get; set; }
    }
}