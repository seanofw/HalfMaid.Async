using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HalfMaid.Async
{
	/// <summary>
	/// The GameTaskRunner runs GameTasks, as its name implies, one frame at a time.
	/// This class is thread-safe, and all public methods may be invoked from any thread.
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
		private long _externalTasks = 0;

		/// <summary>
		/// Future pending work, stored as a priority queue for efficient access,
		/// keyed by the frame on which to perform it.
		/// </summary>
		private readonly PriorityQueue<Action, long> _yieldedContinuations = new PriorityQueue<Action, long>();

		/// <summary>
		/// Enqueue work to perform on some future frame.
		/// </summary>
		/// <param name="action">The action to enqueue.</param>
		/// <param name="frames">How far in the future to perform it.</param>
		public void EnqueueFuture(Action action, int frames)
		{
			lock (_yieldedContinuations)
			{
				_yieldedContinuations.Enqueue(action, _frame + frames);
			}
		}

		/// <summary>
		/// Wait until the next frame before continuing.  This constructs a "yield awaitable"
		/// for one frame in the future and returns it.
		/// </summary>
		/// <returns>A "yield awaitable," which you should await immediately.</returns>
		public GameTaskYieldAwaitable Next()
			=> new GameTaskYieldAwaitable(this, 1);

		/// <summary>
		/// Wait for several frames before continuing.  This constructs a "yield awaitable"
		/// for one frame in the future and returns it.
		/// </summary>
		/// <param name="frameCount">How many frames to wait.  This must be 1 or more.</param>
		/// <returns>A "yield awaitable," which you should await immediately.</returns>
		/// <exception cref="ArgumentException">Thrown if the number of frames to wait is zero
		/// or negative.</exception>
		public GameTaskYieldAwaitable Delay(int frameCount)
		{
			if (frameCount <= 0)
				throw new ArgumentException(nameof(frameCount), "Frame count for Delay() must be greater than zero.");

			return new GameTaskYieldAwaitable(this, frameCount);
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
			=> EnqueueFuture(() => action(), 1);

		/// <summary>
		/// How many tasks are currently active in the runner.
		/// </summary>
		public int TaskCount
		{
			get
			{
				lock (_yieldedContinuations)
				{
					return _yieldedContinuations.Count + (int)Interlocked.Read(ref _externalTasks);
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
				Action? continuation;
				lock (_yieldedContinuations)
				{
					if (!_yieldedContinuations.TryDequeue(out continuation, out long frame)
						&& Interlocked.Read(ref _externalTasks) == 0)
						break;
					Interlocked.Exchange(ref _frame, frame);
				}

				continuation!();
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
				Action? continuation;
				lock (_yieldedContinuations)
				{
					if (!_yieldedContinuations.TryPeek(out continuation, out long frame)
						|| frame > currentFrame)
						break;

					_yieldedContinuations.Dequeue();
				}

				continuation!();
			}
		}

		/// <summary>
		/// Run an external task that performs I/O or similar as a GameTask.  The external
		/// task will be run on the thread pool.  When the external task completes, the
		/// calling GameTask will continue on the next frame following its completion.
		/// This is typically used to perform I/O easily, but without blocking the main
		/// game loop.
		/// </summary>
		/// <param name="task">The task to run.</param>
		public ExternalTaskAwaitable RunTask(Func<Task> task)
		{
			Interlocked.Increment(ref _externalTasks);

			ExternalTaskAwaitable awaitable = new ExternalTaskAwaitable(this);

			Task.Run(async () => {
				await task();
				awaitable.Trigger();
				Interlocked.Decrement(ref _externalTasks);
			});

			return awaitable;
		}
	}
}
