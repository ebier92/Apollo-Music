// <copyright file="ISearchFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Threading;
using Android.OS;
using Android.Views;

namespace Apollo
{
    internal interface ISearchFragment
    {
        CancellationToken CancellationToken { get; }

        string ContinuationToken { get; set; }

        void AddSearchResultContentItem(YouTube.ContentItem searchResultContentItem);

        void NotifyNetworkLost();

        View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState);

        void OnHiddenChanged(bool hidden);

        void OnQuery(string query);

        void OnScrollToBottom();

        void RequestBackgroundFocus();

        void RequestSearchViewFocus();

        void UpdateAdditionalSearchResultsBufferingIconVisibility(ViewStates viewState);

        void UpdateFilterButtonState(int activeButtonId);

        void UpdateInitialSearchResultsBufferingIconVisibility(ViewStates viewState);

        void UpdateSearchBackgroundVisibility(ViewStates viewState);

        void UpdateSearchQueryText(string query);
    }
}