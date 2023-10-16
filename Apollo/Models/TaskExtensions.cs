// <copyright file="TaskExtensions.cs" company="Erik Bierbrauer">
// Copyright (c) Erik Bierbrauer. All rights reserved.
// </copyright>

using System.Threading;
using System.Threading.Tasks;

namespace Apollo
{
    /// <summary>
    /// Task extensions class to add functionality for on-demand cancelling of any in progress task.
    /// </summary>
    internal static class TaskExtensions
    {
        /// <summary>
        /// Awaits a task and supports on-demand cancellation from the <see cref="CancellationToken"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="Task"/> return type.</typeparam>
        /// <param name="task">A <see cref="Task"/> that needs to support on demand cancellation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the <see cref="Task"/>.</param>
        /// <returns>The <see cref="Task"/> to cancel using the <see cref="CancellationToken"/>.</returns>
        public static async Task<T> WaitOrCancel<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAny(task, cancellationToken.WhenCanceled());
            cancellationToken.ThrowIfCancellationRequested();

            return await task;
        }

        /// <summary>
        /// Registers the <see cref="CancellationToken"/> for the task.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the <see cref="Task"/>.</param>
        /// <returns>The <see cref="Task"/> to cancel using the <see cref="CancellationToken"/>.</returns>
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);

            return tcs.Task;
        }
    }
}