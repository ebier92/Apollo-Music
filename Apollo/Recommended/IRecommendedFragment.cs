// <copyright file="IRecommendedFragment.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Threading;
using Android.OS;
using Android.Views;

namespace Apollo
{
    internal interface IRecommendedFragment
    {
        CancellationToken CancellationToken { get; }

        void AddRecommendedContentItem(YouTube.ContentItem recommendedContentItem);

        void ClearRecommendedContentItems();

        void NotifyNetworkLost();

        View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState);

        void OnHiddenChanged(bool hidden);

        void ScrollToPosition(int position);

        void UpdateContentLoadErrorElementsVisibility(ViewStates viewState);

        void UpdateContentLoadingIconVisibility(ViewStates viewState);

        void UpdateEmptyPageElementsVisibility(ViewStates viewState);

        void UpdateRecyclerViewAndRefreshButtonVisibility(ViewStates viewState);
    }
}