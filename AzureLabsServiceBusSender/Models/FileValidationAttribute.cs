using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.Web;

namespace AzureLabsServiceBusSender.Models
{
    public class UploadedFileValidationAttribute : RequiredAttribute
    {
        public override bool IsValid(object value)
        {
            var file = value as HttpPostedFileBase;
            if (file == null) return false;
            if (file.ContentLength > 5 * 1024 * 1024) return false;
            try
            {
                using (var img = Image.FromStream(file.InputStream))
                {
                    return img.RawFormat.Equals(ImageFormat.Jpeg);
                }
            }
            catch
            {
                // ignored
            }
            return false;
        }
    }
}