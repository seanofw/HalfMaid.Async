﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HalfMaid.Async
{
	/// <summary>
	/// The GameTaskRunner runs GameTasks, as its name implies, one frame at a time.
	/// 
	/// This class is not thread-safe:  Public methods on this class should only be
	/// invoked by a single thread, and it should be the thread that created the
	/// runner instance.
	/// 
	/// Note that ExternalTaskAwaitable *is* allowed to invoke this class from another
	/// thread, but it (and only it!) understands how to do so safely.
	/// </summary>
	public class GameTaskRunner
	{
		/// <summary>
		/// The current frame, from the runner's perspective.
		/// </summary>
		public long Frame => _frame;
		private long _frame = 0;

		/// <summary>
		/// The number of active external tasks.
		/// </summary>
		private ConcurrentDictionary<ExternalTaskAwaitable, bool> _externalTasks = new ConcurrentDictionary<ExternalTaskAwaitable, bool>();

		/// <summary>
		/// Future pending work, stored as a priority queue for efficient access,
		/// keyed by the frame on which to perform it.
		/// </summary>
		private readonly PriorityQueue<(Action, ExecutionContext?), long> _yieldedContinuations = new PriorityQueue<(Action, ExecutionContext?), long>();

#if false
		/// <summary>
		/// A function that can cancel active tasks by generating an exception to
		/// raise in them.
		/// </summary>
		private Func<Exception>? _canceller = null;
#endif

		/// <summary>
		/// The currently-executing task, if any.
		/// </summary>
		internal GameTask? InternalCurrent = null;

		/// <summary>
		/// Enqueue work to perform on some future frame.
		/// </summary>
		/// <param name="action">The action to enqueue.</param>
		/// <param name="context">The execution context in which to perform this future work.</param>
		/// <param name="frames">How far in the future to perform it.</param>
		internal void EnqueueFuture(Action action, ExecutionContext? context, int frames)
		{
			lock (_yieldedContinuations)
			{
				_yieldedContinuations.Enqueue((action, context), _frame + frames);
			}
		}

		/// <summary>
		/// Enqueue work to perform on some future frame.
		/// </summary>
		/// <param name="action">The action to enqueue, an async method that returns a GameTask.</param>
		/// <param name="context">The execution context in which to perform this future work.</param>
		/// <param name="frames">How far in the future to perform it.</param>
		private void EnqueueFuture(Func<GameTask> action, ExecutionContext? context, int frames)
		{
			EnqueueFuture(() => action(), context, frames);
		}

		/// <summary>
		/// Wait until the next frame before continuing.
		/// </summary>
		/// <returns>A GameTask which you should await immediately.</returns>
		public GameTaskYieldAwaitable Next()
		{
			ExecutionContext? context = ExecutionContext.Capture();
			return new GameTaskYieldAwaitable(this, context, 1);
		}

		/// <summary>
		/// Wait for several frames before continuing.
		/// </summary>
		/// <param name="frameCount">How many frames to wait.  This must be 1 or more.</param>
		/// <returns>A GameTask which you should await immediately.</returns>
		/// <exception cref="ArgumentException">Thrown if the number of frames to wait is zero
		/// or negative.</exception>
		public GameTaskYieldAwaitable Delay(int frameCount)
		{
			if (frameCount <= 0)
				throw new ArgumentException(nameof(frameCount), "Frame count for Delay() must be greater than zero.");

			ExecutionContext? context = ExecutionContext.Capture();
			return new GameTaskYieldAwaitable(this, context, frameCount);
		}

		/// <summary>
		/// Start running the given async GameTask-returning method immediately.  The method
		/// is allowed to use async/await to delay parts of its future execution.
		/// </summary>
		/// <param name="action">The action to perform.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void StartImmediately(Func<GameTask> action)
			=> action();

		/// <summary>
		/// Start running the given async GameTask-returning method during the *next* frame of
		/// execution.  The method is allowed to use async/await to delay parts of its future
		/// execution beyond that.
		/// </summary>
		/// <param name="action">The action to schedule for the next execution frame.</param>
		public void StartYielded(Func<GameTask> action)
			=> EnqueueFuture(() => action(), ExecutionContext.Capture(), 1);

		/// <summary>
		/// How many tasks are currently active in the runner.
		/// </summary>
		public int TaskCount
		{
			get
			{
				lock (_yieldedContinuations)
				{
					return _yieldedContinuations.Count + _externalTasks.Count;
				}
			}
		}

		/// <summary>
		/// Run all tasks until the runner is completely empty.  This will execute all
		/// remaining work in order, but as fast as possiuble.  This will fast-forward the
		/// internal frame counter to the last frame referenced.
		/// </summary>
		public void RunUntilAllTasksFinish()
		{
			while (true)
			{
				(Action Action, ExecutionContext? ExecutionContext) continuation;
				lock (_yieldedContinuations)
				{
					if (!_yieldedContinuations.TryDequeue(out continuation, out long frame)
						&& _externalTasks.Count == 0)
						break;
					Interlocked.Exchange(ref _frame, frame);
				}

				if (continuation.ExecutionContext != null)
				{
					ExecutionContext.Run(continuation.ExecutionContext, ExecutionContextRunner, continuation.Action);
				}
				else continuation.Action();
			}
		}

		/// <summary>
		/// Run all tasks in the next frame, advancing the frame number.
		/// </summary>
		public void RunNextFrame()
		{
			long currentFrame = Interlocked.Increment(ref _frame);

			while (true)
			{
				(Action Action, ExecutionContext? ExecutionContext) continuation;
				lock (_yieldedContinuations)
				{
					if (!_yieldedContinuations.TryPeek(out continuation, out long frame)
						|| frame > currentFrame)
						break;

					_yieldedContinuations.Dequeue();
				}

				if (continuation.ExecutionContext != null)
				{
					ExecutionContext.Run(continuation.ExecutionContext, ExecutionContextRunner, continuation.Action);
				}
				else continuation.Action();
			}
		}

		/// <summary>
		/// Run the given provided callback inside a custom execution context.
		/// </summary>
		/// <param name="obj">The callback to invoke, which must be an Action.</param>
#if NET6_0_OR_GREATER
		[StackTraceHidden]
#endif
		[DebuggerHidden]
		private static void ExecutionContextRunner(object? obj)
		{
			((Action)obj!)();
		}

		/// <summary>
		/// Run an external task that performs I/O or similar as a GameTask.  The external
		/// task will be run on the thread pool.  When the external task completes, the
		/// calling GameTask will continue on the next frame following its completion.
		/// This is typically used to perform I/O easily, but without blocking the main
		/// game loop.
		/// </summary>
		/// <param name="task">The task to run.</param>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public ExternalTaskAwaitable RunTask(Func<Task> task)
		{
			ExecutionContext? context = ExecutionContext.Capture();
			ExternalTaskAwaitable awaitable = new ExternalTaskAwaitable(this, context);
			_externalTasks[awaitable] = true;

			var _ = (object)Task.Run(async () => {
				await task();
				if (_externalTasks.TryRemove(awaitable, out bool _))
					awaitable.Trigger();
			});

			return awaitable;
		}

#if false
		/// <summary>
		/// Sometimes, you need a way to shut down tasks cleanly.  This will cause
		/// an exception to be thrown in each *current* GameTask, thus aborting it; and it
		/// will give you a way to catch and handle that exception as well when thrown
		/// inside that task.  Note that this has no effect on any active *external* tasks;
		/// if those exist, it is your responsibility to ensure that their Task is
		/// aborted/cancelled.  (Any work to be performed by the GameTask after that
		/// Task completes will be cancelled by this, but this has no effect on the Task
		/// itself.)
		/// </summary>
		/// <param name="createException">A function that can create the type of exception
		/// you want to throw in every active task.  If omitted, a TaskCanceledException will
		/// be thrown (with no message).</param>
		/// <param name="handleUncaughtExceptions">An optional function that will handle
		/// uncaught exceptions of the given type.  Any uncaught exceptions of the
		/// cancellation type will be used to cancel tasks, but they will be silently
		/// discarded if this is null; they will not bubble outside of the CancelAllTasks()
		/// method either way.</param>
		public void CancelAllTasks(Action<Action>? handleUncaughtExceptions = null)
			=> CancelAllTasks(() => new TaskCanceledException(), handleUncaughtExceptions);

		/// <summary>
		/// Sometimes, you need a way to shut down tasks cleanly.  This will cause
		/// an exception to be thrown in each *current* GameTask, thus aborting it; and it
		/// will give you a way to catch and handle that exception as well when thrown
		/// inside that task.  Note that this has no effect on any active *external* tasks;
		/// if those exist, it is your responsibility to ensure that their Task is
		/// aborted/cancelled.  (Any work to be performed by the GameTask after that
		/// Task completes will be cancelled by this, but this has no effect on the Task
		/// itself.)
		/// </summary>
		/// <param name="createException">A function that can create the type of exception
		/// you want to throw in every active task.  If null, a TaskCanceledException will
		/// be thrown (with no message).</param>
		/// <param name="handleUncaughtExceptions">An optional function that will handle
		/// uncaught exceptions of the given type.  Any uncaught exceptions of the
		/// cancellation type will be used to cancel tasks, but they will be silently
		/// discarded if this is null; they will not bubble outside of the CancelAllTasks()
		/// method either way.</param>
		public void CancelAllTasks<TException>(Func<TException> createException,
			Action<Action>? handleUncaughtExceptions = null)
			where TException : Exception
		{
			_canceller = createException;
			_externalTasks.Clear();
			try
			{
				while (true)
				{
					(Action Action, ExecutionContext? ExecutionContext) continuation;
					lock (_yieldedContinuations)
					{
						if (!_yieldedContinuations.TryDequeue(out continuation, out long frame))
							break;
					}

					if (continuation.ExecutionContext != null)
					{
						if (handleUncaughtExceptions != null)
						{
							ExecutionContext.Run(continuation.ExecutionContext, ExecutionContextRunner,
								(Action)(() => handleUncaughtExceptions(continuation.Action)));
						}
						else
						{
							ExecutionContext.Run(continuation.ExecutionContext, ExecutionContextRunner, continuation.Action);
						}
					}
					else continuation.Action();
				}
			}
			finally
			{
				_canceller = null;
			}
		}
#endif
	}
}
