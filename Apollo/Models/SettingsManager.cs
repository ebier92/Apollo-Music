// <copyright file="SettingsManager.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Text;
using Android.App;
using Android.Provider;
using Android.Widget;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Apollo
{
    /// <summary>
    /// Interface to access saved JSON settings data.
    /// </summary>
    internal static class SettingsManager
    {
        public const string DefaultAppDataFileName = "Apollo_Data.json";
        public const int ExportAppDataFileRequestCode = 1;
        public const int ImportAppDataFileRequestCode = 2;
        private const string SettingsRoot = "Settings";
        private const string RecommendationsRoot = "Recommendations";
        private const string ContentRoot = "Content";
        private const string SearchSettingName = "SearchPreference";
        private const string StreamQualitySettingName = "StreamQuality";
        private const string PlaylistSourceSettingName = "PlaylistSource";

        /// <summary>
        /// Content type choices for searching.
        /// </summary>
        public enum SearchSettingOptions
        {
            /// <summary>
            /// Search general YouTube content.
            /// </summary>
            General,

            /// <summary>
            /// Search songs from YouTube Music.
            /// </summary>
            Songs,

            /// <summary>
            /// Search albums from YouTube Music.
            /// </summary>
            Albums,

            /// <summary>
            /// Search featured playlists from YouTube Music.
            /// </summary>
            FeaturedPlaylists,

            /// <summary>
            /// Search community playlists from YouTube Music.
            /// </summary>
            CommunityPlaylists,
        }

        /// <summary>
        /// Stream quality choices.
        /// </summary>
        public enum StreamQualitySettingOptions
        {
            /// <summary>
            /// Use lowest quality stream available.
            /// </summary>
            Low,

            /// <summary>
            /// Use a medium quality stream.
            /// </summary>
            Medium,

            /// <summary>
            /// Use the highest quality stream available.
            /// </summary>
            High,
        }

        /// <summary>
        /// Playlist generation source choices.
        /// </summary>
        public enum PlaylistSourceSettingOptions
        {
            /// <summary>
            /// Use YouTube related videos to generate a playlist from a selected video.
            /// </summary>
            YouTube,

            /// <summary>
            /// Use YouTube Music watch playlist feature to generate a playlist from a selected video.
            /// </summary>
            YouTubeMusic,
        }

        /// <summary>
        /// Gets or sets the search setting value.
        /// </summary>
        public static SearchSettingOptions SearchSetting
        {
            get
            {
                var settings = ReadJson();

                return (SearchSettingOptions)Enum.Parse(typeof(SearchSettingOptions), (string)settings[SearchSettingName]);
            }

            set
            {
                // Set setting value
                var settings = ReadJson();
                settings[SearchSettingName] = value.ToString();

                // Write to file
                WriteJson(settings);
            }
        }

        /// <summary>
        /// Gets or sets the stream quality setting value.
        /// </summary>
        public static StreamQualitySettingOptions StreamQualitySetting
        {
            get
            {
                var settings = ReadJson();

                return (StreamQualitySettingOptions)Enum.Parse(typeof(StreamQualitySettingOptions), (string)settings[StreamQualitySettingName]);
            }

            set
            {
                // Set setting value
                var settings = ReadJson();
                settings[StreamQualitySettingName] = value.ToString();

                // Write to file
                WriteJson(settings);
            }
        }

        /// <summary>
        /// Gets or sets the playlist source setting value.
        /// </summary>
        public static PlaylistSourceSettingOptions PlaylistSourceSetting
        {
            get
            {
                var settings = ReadJson();

                return (PlaylistSourceSettingOptions)Enum.Parse(typeof(PlaylistSourceSettingOptions), (string)settings[PlaylistSourceSettingName]);
            }

            set
            {
                // Set setting value
                var settings = ReadJson();
                settings[PlaylistSourceSettingName] = value.ToString();

                // Write to file
                WriteJson(settings);
            }
        }

        /// <summary>
        /// Gets the file path for the settings JSON file.
        /// </summary>
        private static string JsonFilePath
        {
            get
            {
                // Get system path for JSON settings file
                return Path.Combine(Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "settings.json");
            }
        }

        /// <summary>
        /// Creates a default settings JSON file if one does not yet exist.
        /// </summary>
        public static void InitializeJsonFile()
        {
            // TODO: Remove else clause once all beta users have converted their settings
            if (!File.Exists(JsonFilePath))
            {
                var settings = new JObject
                {
                    [SearchSettingName] = nameof(SearchSettingOptions.Songs),
                    [StreamQualitySettingName] = nameof(StreamQualitySettingOptions.Medium),
                    [PlaylistSourceSettingName] = nameof(PlaylistSourceSettingOptions.YouTubeMusic),
                };

                WriteJson(settings);
            } // Convert old settings JSON to a new structure that does not use the settings root for settings.json
            else
            {
                var settings = ReadJson();

                if (settings.ContainsKey(SettingsRoot))
                {
                    var newSettings = new JObject
                    {
                        [SearchSettingName] = settings[SettingsRoot][SearchSettingName],
                        [StreamQualitySettingName] = settings[SettingsRoot][StreamQualitySettingName],
                        [PlaylistSourceSettingName] = settings[SettingsRoot][PlaylistSourceSettingName],
                    };

                    WriteJson(newSettings);
                }
            }
        }

        /// <summary>
        /// Clears all user search history.
        /// </summary>
        public static void ClearSearchHistory()
        {
            var searchRecentSuggestions = new SearchRecentSuggestions(
                    Application.Context,
                    SearchSuggestionsProvider.Authority,
                    SearchSuggestionsProvider.Mode);

            searchRecentSuggestions.ClearHistory();
        }

        /// <summary>
        /// Clears all user recomendation data.
        /// </summary>
        public static void ClearRecommendations()
        {
            RecommendationsManager.ClearRecommendations();
        }

        /// <summary>
        /// Clears all user playlists.
        /// </summary>
        public static void ClearPlaylists()
        {
            ContentManager.ClearContent();
        }

        /// <summary>
        /// Exports app data, including settings, listening history, and saved playlists to a user defined JSON file for backup.
        /// </summary>
        /// <param name="mainActivity">A reference to the app <see cref="MainActivity"/>.</param>
        /// <param name="uri">The <see cref="Android.Net.Uri"/> of the JSON file.</param>
        public static void ExportAppData(Activity mainActivity, Android.Net.Uri uri)
        {
            // Get JObject representations of settings data, recommendations data, and content data
            var settingsData = ReadJson();
            var recommendationsData = RecommendationsManager.ExportJson();
            var contentData = ContentManager.ExportJson();

            // Create a new JSON object containing all app data
            var apolloData = new JObject
            {
                [SettingsRoot] = settingsData,
                [RecommendationsRoot] = recommendationsData,
                [ContentRoot] = contentData,
            };

            try
            {
                var parcelFileDescriptor = mainActivity.ContentResolver.OpenFileDescriptor(uri, "w");
                var fileOutputStream = new Java.IO.FileOutputStream(parcelFileDescriptor.FileDescriptor);

                fileOutputStream.Write(Encoding.UTF8.GetBytes(apolloData.ToString()));
                fileOutputStream.Close();

                // Show toast if export was successful
                Toast.MakeText(Application.Context, Resource.String.export_app_data_success, ToastLength.Short).Show();
            }
            catch
            {
                // Show a toast in case of error
                Toast.MakeText(Application.Context, Resource.String.error_export_app_data, ToastLength.Short).Show();
            }
        }

        /// <summary>
        /// Imports app data, including settings, listening history, and saved playlists from a user defined JSON file for backup.
        /// </summary>
        /// <param name="mainActivity">A reference to the app <see cref="MainActivity"/>.</param>
        /// <param name="uri">The <see cref="Android.Net.Uri"/> of the JSON file.</param>
        public static void ImportAppData(Activity mainActivity, Android.Net.Uri uri)
        {
            var stringBuilder = new StringBuilder();

            try
            {
                // Set up stream reader to get data from selected file
                var inputStream = mainActivity.ContentResolver.OpenInputStream(uri);
                var bufferedReader = new Java.IO.BufferedReader(new Java.IO.InputStreamReader(inputStream, "UTF-8"));

                // Get line by line file data
                string line;

                do
                {
                    line = bufferedReader.ReadLine();

                    if (line != null)
                        stringBuilder.Append(line);
                }
                while (line != null);

                // Extract JObjects for settings, recommendations, and content
                var appData = JObject.Parse(stringBuilder.ToString());
                var settingsData = (JObject)appData[SettingsRoot];
                var recommendationsData = (JObject)appData[RecommendationsRoot];
                var contentData = (JObject)appData[ContentRoot];

                // Write JSON data to each file location to be used by the app
                WriteJson(settingsData);
                RecommendationsManager.ImportJson(recommendationsData);
                ContentManager.ImportJson(contentData);

                // Show toast if import was successful
                Toast.MakeText(Application.Context, Resource.String.import_app_data_success, ToastLength.Short).Show();
            }
            catch
            {
                // Show toast in case or error
                Toast.MakeText(Application.Context, Resource.String.error_import_app_data, ToastLength.Short).Show();
            }
        }

        /// <summary>
        /// Reads saved JSON settings data from the settings file.
        /// </summary>
        /// <returns><see cref="JObject"/>Settings JSON data.</returns>
        private static JObject ReadJson()
        {
            string json;

            using (StreamReader streamReader = new StreamReader(JsonFilePath))
            {
                json = streamReader.ReadToEnd();
            }

            return JObject.Parse(json);
        }

        /// <summary>
        /// Writes a <see cref="JObject"/> to the JSON file.
        /// </summary>
        /// <param name="jsonObject"><see cref="JObject"/> to write to the file.</param>
        private static void WriteJson(JObject jsonObject)
        {
            using StreamWriter jsonFile = File.CreateText(JsonFilePath);
            using JsonTextWriter jsonWriter = new JsonTextWriter(jsonFile);
            jsonObject.WriteTo(jsonWriter);
        }
    }
}