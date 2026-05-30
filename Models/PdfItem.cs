namespace PdfDownloader.Models
{
    public class PdfItem
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        
        public string DisplayUrl
        {
            get
            {
                try
                {
                    return new System.Uri(Url).Host;
                }
                catch
                {
                    return Url;
                }
            }
        }
    }
}
