// <copyright file="SearchSuggestionsProvider.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Android.Content;
using Android.Database;
using Newtonsoft.Json.Linq;
using Uri = Android.Net.Uri;

namespace Apollo
{
    /// <summary>
    /// Search suggestions provider class to store historical searches and suggestions from Google's "suggestqueries" API.
    /// </summary>
    [ContentProvider(new[] { "com.erikb.Apollo.SearchSuggestionsProvider" })]
    internal class SearchSuggestionsProvider : SearchRecentSuggestionsProvider
    {
        public const string Authority = "com.erikb.Apollo.SearchSuggestionsProvider";
        public const DatabaseMode Mode = DatabaseMode.Queries;
        private const string SuggestQueriesUrl = "https://suggestqueries.google.com/complete/search?client=youtube&ds=yt&q={0}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchSuggestionsProvider"/> class.
        /// </summary>
        public SearchSuggestionsProvider()
        {
            SetupSuggestions(Authority, Mode);
        }

        /// <summary>
        /// Called whenever any change is made to the input of the search view and returns a combined cursor containing both historical search suggestions and suggestions from Google.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> and the query.</param>
        /// <param name="projection">Projection is not used.</param>
        /// <param name="selection">Query selection pattern specified in searchable.xml.</param>
        /// <param name="selectionArgs">The query text from the search view input.</param>
        /// <param name="sortOrder">Sort order is not used.</param>
        /// <returns><see cref="ICursor"/> containing historical search suggestions and Google suggestions.</returns>
        public override ICursor Query(Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder)
        {
            // Get cursor containing historical suggestions from the base implementation
            var cursor = base.Query(uri, projection, selection, selectionArgs, sortOrder);

            // Attempt to get suggestions from the Google API
            List<string> suggestions;

            try
            {
                suggestions = GetSearchSuggestions(selectionArgs[0]).Result;
            }
            catch (Exception)
            {
                suggestions = new List<string>();
            }

            // Initialize a new matrix cursor
            var matrixCursor = new MatrixCursor(cursor.GetColumnNames());

            // Build the URI for a search icon to display next to non-historical suggestions
            var iconUri = new Uri.Builder()
                .Scheme(ContentResolver.SchemeAndroidResource)
                .Authority(Context.Resources.GetResourcePackageName(Resource.Drawable.abc_ic_search_api_material))
                .AppendPath(Context.Resources.GetResourceTypeName(Resource.Drawable.abc_ic_search_api_material))
                .AppendPath(Context.Resources.GetResourceEntryName(Resource.Drawable.abc_ic_search_api_material))
                .Build();

            // Add each Google suggestion as a row to the cursor
            for (var i = 0; i < suggestions.Count; i++)
            {
                matrixCursor.AddRow(new Java.Lang.Object[] { "0", iconUri.ToString(), suggestions[i], suggestions[i], (i + suggestions.Count).ToString() });
            }

            // Combine the historical suggestions cursor with the Google suggestions cursor
            var mergedCursor = new MergeCursor(new ICursor[] { cursor, matrixCursor });

            return mergedCursor;
        }

        /// <summary>
        /// Gets a list of search suggestions from the Google "suggestqueries" API based on an input query.
        /// </summary>
        /// <param name="query">The user's input query.</param>
        /// <returns>A list of search suggestions.</returns>
        private async Task<List<string>> GetSearchSuggestions(string query)
        {
            // Initialize suggestions list
            var suggestions = new List<string>();

            // Proceed only if there is network connectivity
            if (NetworkStatus.IsConnected)
            {
                // Get the data from the API
                var result = "";
                using (HttpClient client = new HttpClient())
                {
                    result = await client.GetStringAsync(string.Format(SuggestQueriesUrl, query));
                }

                // Remove JSONP headers and convert the data into standard JSON
                var json = JToken.Parse(result.Replace("window.google.ac.h(", "").TrimEnd(')'));

                // Iterate through the suggestions element and add to the suggestions list
                foreach (var suggestion in json[1])
                {
                    suggestions.Add((string)suggestion[0]);
                }
            }

            return suggestions;
        }
    }
}