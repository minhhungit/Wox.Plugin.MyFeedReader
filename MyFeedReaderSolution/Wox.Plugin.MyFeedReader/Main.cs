using Wox.Plugin.MyFeedReader.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Wox.Plugin.MyFeedReader
{
    public class Main : IPlugin
    {
        private PluginInitContext _context;
        private static readonly string ImagePathPrefix = "Images\\";
        private readonly List<SiteEntity> _allSites;

        public Main()
        {
            _allSites = LoadSites();
        }

        public static string SerializeObject<T>(T toSerialize)
        {
            var xmlSerializer = new XmlSerializer(toSerialize.GetType());

            using (StringWriter textWriter = new StringWriter())
            {
                xmlSerializer.Serialize(textWriter, toSerialize);
                return textWriter.ToString();
            }
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (string.IsNullOrWhiteSpace(query.Search))
            {
                // load main site
                foreach (var site in _allSites)
                {
                    results.Add(ResultForSitesCommandAutoComplete(query.ActionKeyword, site));
                }
                return results;
            }

            var mainSite = GetSiteByCommand(query.FirstSearch);
            if (mainSite == null)
            {
                // load main site suggestion items
                var suggestions = _allSites.Where(x => x.Name.ToLower().Contains(query.FirstSearch.ToLower()) || x.CommandKey.ToLower().Contains(query.FirstSearch.ToLower())).ToList();
                foreach (var suggestItem in suggestions)
                {
                    results.Add(ResultForSitesCommandAutoComplete(query.ActionKeyword, suggestItem));
                }

                return results;
            }

            if (!string.IsNullOrWhiteSpace(query.FirstSearch) && string.IsNullOrWhiteSpace(query.SecondSearch))
            {
                //MessageBox.Show($"1{query.FirstSearch}-{query.SecondSearch}-{query.ThirdSearch}");

                // load child sites for main
                results.Add(ResultForOpenSiteCommandAutoComplete(mainSite));
                if (mainSite.SubItems != null)
                {
                    foreach (var subSite in mainSite.SubItems)
                    {
                        results.Add(ResultForSubSitesCommandAutoComplete(query.ActionKeyword, mainSite.CommandKey, subSite));
                    }

                    return results;
                }

                results.AddRange(LoadRssItems(mainSite));
                return results;
            }

            if (!string.IsNullOrWhiteSpace(query.FirstSearch) && !string.IsNullOrWhiteSpace(query.SecondSearch) && string.IsNullOrWhiteSpace(query.ThirdSearch))
            {
                //MessageBox.Show($"2{query.FirstSearch}-{query.SecondSearch}-{query.ThirdSearch}");

                if (mainSite.SubItems != null)
                {
                    var subSite = mainSite.SubItems.FirstOrDefault(x => x.CommandKey == query.SecondSearch);
                    if (subSite == null)
                    {
                        results.Add(ResultForOpenSiteCommandAutoComplete(mainSite));

                        // return child site suggestions
                        var suggestions = mainSite.SubItems.Where(x =>
                            x.Name.ToLower().Contains(query.SecondSearch.ToLower()) ||
                            x.CommandKey.ToLower().Contains(query.SecondSearch.ToLower())).ToList();

                        foreach (var suggestItem in suggestions)
                        {
                            results.Add(ResultForSubSitesCommandAutoComplete(query.ActionKeyword, mainSite.CommandKey,
                                suggestItem));
                        }

                        return results;
                    }
                    else
                    {
                        results.Add(ResultForOpenSiteCommandAutoComplete(subSite));
                        results.AddRange(LoadRssItems(subSite));
                        return results;
                    }
                }
                else
                {
                    results.Add(ResultForOpenSiteCommandAutoComplete(mainSite));
                    return results;
                }
            }

            if (!string.IsNullOrWhiteSpace(query.FirstSearch) && !string.IsNullOrWhiteSpace(query.SecondSearch) && !string.IsNullOrWhiteSpace(query.ThirdSearch))
            {
                // MessageBox.Show($"3{query.FirstSearch}-{query.SecondSearch}-{query.ThirdSearch}");

                var subSite = mainSite.SubItems.FirstOrDefault(x => x.CommandKey == query.SecondSearch);
                if (subSite != null)
                {
                    results.Add(ResultForOpenSiteCommandAutoComplete(subSite));
                    return results;
                }
                else
                {
                    results.Add(ResultForOpenSiteCommandAutoComplete(mainSite));
                    return results;
                }
            }

            return results;
        }

        public List<Result> Search(Query query, string searchKey)
        {
            var results = new List<Result>();

            var subSiteSelected = GetSubSiteByCommand(query.FirstSearch, searchKey);
            if (subSiteSelected != null)
            {
                return LoadRssItems(subSiteSelected);
            }

            var mainSite = _allSites.FirstOrDefault(x => x.CommandKey == query.FirstSearch);

            if (mainSite == null)
            {
                var suggestions = _allSites.Where(x => x.Name.ToLower().Contains(query.FirstSearch.ToLower()) || x.CommandKey.ToLower().Contains(query.FirstSearch.ToLower())).ToList();
                foreach (var suggestItem in suggestions)
                {
                    results.Add(ResultForSitesCommandAutoComplete(query.ActionKeyword, suggestItem));
                }

                return results;
            }

            var baseSuggestions = mainSite.SubItems
                .Where(x => x.Name.ToLower().Contains(searchKey.ToLower()) || x.CommandKey.ToLower().Contains(searchKey.ToLower()))
                .ToList();

            if (baseSuggestions.Any())
            {
                foreach (var suggestItem in baseSuggestions)
                {
                    results.Add(ResultForSubSitesCommandAutoComplete(query.ActionKeyword, mainSite.CommandKey, suggestItem));
                }

                return results;
            }
            return new List<Result> { new Result { Title = "No items for " + searchKey } };
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        private List<SiteEntity> LoadSites()
        {
            var loc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            using (var r = new StreamReader($"{loc}\\sites.json"))
            {
                var json = r.ReadToEnd();
                var items = JsonConvert.DeserializeObject<List<SiteEntity>>(json);
                return items;
            }
        }

        public SiteEntity GetSiteByCommand(string mainSiteCommand)
        {
            return _allSites.FirstOrDefault(x => x.CommandKey.Trim().Equals(mainSiteCommand.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public SubSiteEntity GetSubSiteByCommand(string mainSiteCommand, string subSiteCommand)
        {
            var mainSite = GetSiteByCommand(mainSiteCommand);
            return mainSite?.SubItems?.FirstOrDefault(x => x.CommandKey.Trim().Equals(subSiteCommand.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private Result ResultForSitesCommandAutoComplete(string actionKeyword, SiteEntity site)
        {
            const string seperater = Plugin.Query.TermSeperater;
            var result = new Result
            {
                Title = site.Name,
                IcoPath = site.IconPath,
                SubTitle = string.IsNullOrWhiteSpace(site.Description) ? $"{site.CommandKey}" : $"{site.CommandKey} - {site.Description}",
                Action = e =>
                {
                    _context.API.ChangeQuery($"{actionKeyword}{seperater}{site.CommandKey}{seperater}");
                    return false;
                }
            };
            return result;
        }

        private Result ResultForSubSitesCommandAutoComplete(string actionKeyword, string mainCommand, SubSiteEntity subSite)
        {
            const string seperater = Plugin.Query.TermSeperater;
            var result = new Result
            {
                Title = subSite.Name,
                IcoPath = subSite.IconPath,
                SubTitle = string.IsNullOrWhiteSpace(subSite.Description) ? $"{subSite.CommandKey}" : $"{subSite.CommandKey} - {subSite.Description}",
                Action = e =>
                {
                    _context.API.ChangeQuery($"{actionKeyword}{seperater}{mainCommand}{seperater}{subSite.CommandKey}{seperater}");
                    return false;
                }
            };
            return result;
        }

        private Result ResultForOpenSiteCommandAutoComplete(BaseSiteEntity baseSiteEntity)
        {
            if (baseSiteEntity != null)
            {
                var result = new Result
                {
                    Title = baseSiteEntity.Name,
                    SubTitle = "Open site " + baseSiteEntity.Name,
                    IcoPath = $"{ImagePathPrefix}open.png",
                    Score = 99999,
                    Action = (x) =>
                    {
                        try
                        {
                            Process.Start(baseSiteEntity.Url);
                            return true;
                        }
                        catch
                        {
                            _context.API.ShowMsg($"Can not open url {baseSiteEntity.Url}");
                            return false;
                        }
                    }
                };

                return result;
            }

            return new Result{Title = "No items..."};
        }

        private List<Result> LoadRssItems(BaseSiteEntity site)
        {
            var results = new List<Result>();

            if (!string.IsNullOrWhiteSpace(site.RssUrl))
            {
                var feed = CodeHollow.FeedReader.FeedReader.ReadAsync(site.RssUrl).Result;
                foreach (var item in feed.Items)
                {
                    var tmp = new Result
                    {
                        Title = PluginHelper.StripHtml(item.Title),
                        IcoPath = site.IconPath,
                        Action = x =>
                        {
                            try
                            {
                                Process.Start(item.Link);
                                return true;
                            }
                            catch
                            {
                                _context.API.ShowMsg($"Can not open url {item.Link}");
                                return false;
                            }
                        }
                    };

                    var date = item.PublishingDate?.ToString("yyyyMMdd hh:mm tt");
                    tmp.SubTitle = string.IsNullOrWhiteSpace(item.Author) ? date : $"{item.Author} - {date}";

                    results.Add(tmp);
                }
            }
            return results;
        }
    }
}
