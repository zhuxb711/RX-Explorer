using System;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class SearchOptions
    {
        public FileSystemStorageFolder SearchFolder { get; set; }

        public string SearchText { get; set; }

        public bool IgnoreCase { get; set; }

        public bool UseRegexExpression { get; set; }

        public bool DeepSearch { get; set; }

        public bool UseIndexerOnly { get; set; }

        public bool UseAQSExpression { get; set; }

        public SearchCategory EngineCategory { get; set; }

        public static SearchOptions LoadSavedConfiguration(SearchCategory Category)
        {
            SearchOptions Options = new SearchOptions
            {
                EngineCategory = Category
            };

            switch (Category)
            {
                case SearchCategory.BuiltInEngine:
                    {
                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIgnoreCase", out object BuiltInIgnoreCase))
                        {
                            Options.IgnoreCase = Convert.ToBoolean(BuiltInIgnoreCase);
                        }
                        else
                        {
                            Options.IgnoreCase = true;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIncludeRegex", out object BuiltInUseRegex))
                        {
                            Options.UseRegexExpression = Convert.ToBoolean(BuiltInUseRegex);
                        }
                        else
                        {
                            Options.UseRegexExpression = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInSearchAllSubFolders", out object BuiltInSearchSubFolders))
                        {
                            Options.DeepSearch = Convert.ToBoolean(BuiltInSearchSubFolders);
                        }
                        else
                        {
                            Options.DeepSearch = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIncludeAQS", out object BuiltInUseAQS))
                        {
                            Options.UseAQSExpression = Convert.ToBoolean(BuiltInUseAQS);
                        }
                        else
                        {
                            Options.UseAQSExpression = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInSearchUseIndexer", out object BuiltInUseIndexer))
                        {
                            Options.UseIndexerOnly = Convert.ToBoolean(BuiltInUseIndexer);
                        }
                        else
                        {
                            Options.UseIndexerOnly = false;
                        }

                        break;
                    }
                case SearchCategory.EverythingEngine:
                    {
                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIgnoreCase", out object EverythingIgnoreCase))
                        {
                            Options.IgnoreCase = Convert.ToBoolean(EverythingIgnoreCase);
                        }
                        else
                        {
                            Options.IgnoreCase = true;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIncludeRegex", out object EverythingIncludeRegex))
                        {
                            Options.UseRegexExpression = Convert.ToBoolean(EverythingIncludeRegex);
                        }
                        else
                        {
                            Options.UseRegexExpression = false;
                        }

                        if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineSearchGloble", out object EverythingSearchGloble))
                        {
                            Options.DeepSearch = Convert.ToBoolean(EverythingSearchGloble);
                        }
                        else
                        {
                            Options.DeepSearch = false;
                        }

                        break;
                    }
            }

            return Options;
        }
    }
}
