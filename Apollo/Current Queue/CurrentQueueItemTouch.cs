// <copyright file="CurrentQueueItemTouch.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System;
using Android.Graphics;
using AndroidX.RecyclerView.Widget;

namespace Apollo
{
    /// <summary>
    /// Class to manage swiping and reordering of current queue items.
    /// </summary>
    internal class CurrentQueueItemTouch
    {
        /// <summary>
        /// Interface to listen for a move or dismissal event from an <see cref="ItemTouchHelper.Callback"/>.
        /// </summary>
        public interface IItemTouchAdapter
        {
            /// <summary>
            /// Called when an item is dragged far enough to trigger a move event.
            /// </summary>
            /// <param name="fromPosition">The integer position an item was moved from.</param>
            /// <param name="toPosition">The integer position an item was moved to.</param>
            /// <returns>True if the item was moved to a new position.</returns>
            public bool OnItemMove(int fromPosition, int toPosition);

            /// <summary>
            /// Called when an item is swiped and dismissed.
            /// </summary>
            /// <param name="position">The integer position an item was removed from.</param>
            public void OnItemDismiss(int position);
        }

        /// <summary>
        /// Interface to notify an item view holder of relevant callbacks from an <see cref="ItemTouchHelper.Callback"/>.
        /// </summary>
        public interface IItemTouchViewHolder
        {
            /// <summary>
            /// Called when the <see cref="ItemTouchHelper"/> first registers an item as being moved or swiped.
            /// </summary>
            public void OnItemSelected();

            /// <summary>
            /// Called when the <see cref="ItemTouchHelper"/> has completed the move or swipe.
            /// </summary>
            public void OnItemClear();
        }

        /// <summary>
        /// Interface for a listener for manual start of a drag action.
        /// </summary>
        public interface IOnStartDragListener
        {
            /// <summary>
            /// Called when a drag is first requested by the view.
            /// </summary>
            /// <param name="viewHolder">The holder of the view to drag.</param>
            public void OnStartDrag(RecyclerView.ViewHolder viewHolder);
        }

        public class ItemTouchCallback : ItemTouchHelper.Callback
        {
            private readonly IItemTouchAdapter itemTouchAdapter;

            /// <summary>
            /// Initializes a new instance of the <see cref="ItemTouchCallback"/> class.
            /// </summary>
            /// <param name="itemTouchAdapter">An object that implements the <see cref="itemTouchAdapter"/> interface.</param>
            public ItemTouchCallback(IItemTouchAdapter itemTouchAdapter)
            {
                // Set item touch adapter and initialize enable flag to true
                this.itemTouchAdapter = itemTouchAdapter;
                EnableTouch = true;
            }

            public bool EnableTouch { get; set; }

            public override bool IsLongPressDragEnabled => true;

            public override bool IsItemViewSwipeEnabled => true;

            /// <summary>
            /// Updates the directions of movement for drag and swipe actions.
            /// </summary>
            /// <param name="recyclerView">The <see cref="RecyclerView"/> to set movement flags for.</param>
            /// <param name="viewHolder">The holder of the view where the movement will take place.</param>
            /// <returns>Integer movement flags.</returns>
            public override int GetMovementFlags(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
            {
                var dragFlags = EnableTouch ? ItemTouchHelper.Up | ItemTouchHelper.Down : 0;
                var swipeFlags = EnableTouch ? ItemTouchHelper.Start | ItemTouchHelper.End : 0;

                return MakeMovementFlags(dragFlags, swipeFlags);
            }

            /// <summary>
            /// Called when movement action occurs and sends movement notifications to the <see cref="itemTouchAdapter"/> if applicable.
            /// </summary>
            /// <param name="recyclerView">The <see cref="RecyclerView"/> where the movement occured.</param>
            /// <param name="viewHolder">The holder of the view where the movement originated from.</param>
            /// <param name="target">The holder of the view where the movement sent the item to.</param>
            /// <returns>True if the move was successful.</returns>
            public override bool OnMove(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, RecyclerView.ViewHolder target)
            {
                // If the source view holder is the same type as the target view holder, notify the adapter of the move and return true
                if (viewHolder.ItemViewType == target.ItemViewType)
                {
                    itemTouchAdapter.OnItemMove(viewHolder.AbsoluteAdapterPosition, target.AbsoluteAdapterPosition);

                    return true;
                } // If the view types do not match, return false
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Called when a swipe action occurs and sends a dismissal notification to the <see cref="itemTouchAdapter"/>.
            /// </summary>
            /// <param name="viewHolder">The holder of the view where the swipe occured.</param>
            /// <param name="direction">The integer move flag representing the swipe direction.</param>
            public override void OnSwiped(RecyclerView.ViewHolder viewHolder, int direction)
            {
                itemTouchAdapter.OnItemDismiss(viewHolder.AbsoluteAdapterPosition);
            }

            /// <summary>
            /// Controls the animation for an item during a swipe action.
            /// </summary>
            /// <param name="c"><see cref="Canvas"/> object.</param>
            /// <param name="recyclerView"><see cref="RecyclerView"/> where a drag action is occuring.</param>
            /// <param name="viewHolder">The holder of the view where the drag is occuring.</param>
            /// <param name="dX">X directional offset.</param>
            /// <param name="dY">Y directional offset.</param>
            /// <param name="actionState">Integer flag representing the type of action taking place.</param>
            /// <param name="isCurrentlyActive">Bool representing whether the item is the active item.</param>
            public override void OnChildDraw(Canvas c, RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder, float dX, float dY, int actionState, bool isCurrentlyActive)
            {
                // Run a fade out animation if the item is being swiped
                if (actionState == ItemTouchHelper.ActionStateSwipe)
                {
                    var alpha = 1.0f - (Math.Abs(dX) / (float)viewHolder.ItemView.Width);
                    viewHolder.ItemView.Alpha = alpha;
                    viewHolder.ItemView.TranslationX = dX;
                }
                else
                {
                    base.OnChildDraw(c, recyclerView, viewHolder, dX, dY, actionState, isCurrentlyActive);
                }
            }

            /// <summary>
            /// Called when an item is selected and notifies the <see cref="itemTouchAdapter"/> if applicable.
            /// </summary>
            /// <param name="viewHolder">The holder of the view where the selection has occured.</param>
            /// <param name="actionState">Integer flag representing the type of action taking place.</param>
            public override void OnSelectedChanged(RecyclerView.ViewHolder viewHolder, int actionState)
            {
                // Check if the item is not idle is implements the view holder interface
                if (actionState != ItemTouchHelper.ActionStateIdle && viewHolder is IItemTouchViewHolder itemTouchViewHolder)
                {
                    // Call the OnItemSelected action
                    itemTouchViewHolder.OnItemSelected();
                } // Call the base method otherwise
                else
                {
                    base.OnSelectedChanged(viewHolder, actionState);
                }
            }

            /// <summary>
            /// Called when the view is cleared and notifies the <see cref="itemTouchAdapter"/> if applicable.
            /// </summary>
            /// <param name="recyclerView"><see cref="RecyclerView"/> where a clear view action is occuring.</param>
            /// <param name="viewHolder">The holder of the view where the clear view action is occuring.</param>
            public override void ClearView(RecyclerView recyclerView, RecyclerView.ViewHolder viewHolder)
            {
                base.ClearView(recyclerView, viewHolder);

                // Set item alpha back to full
                viewHolder.ItemView.Alpha = 1.0f;

                // Check if the item implements the view holder interface
                if (viewHolder is IItemTouchViewHolder itemTouchViewHolder)
                {
                    // Call the OnItemClear action to restore the idle state
                    itemTouchViewHolder.OnItemClear();
                }
            }
        }
    }
}