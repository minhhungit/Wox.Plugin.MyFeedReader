using System.Collections.Generic;

namespace Wox.Plugin.MyFeedReader.Models
{
    public class SiteEntity : BaseSiteEntity
    {
        public List<SubSiteEntity> SubItems { get; set; }
    }
}