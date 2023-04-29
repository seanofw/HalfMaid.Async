using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

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
		private long _frame = 0;

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
					return _yieldedContinuations.Count;
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
					if (!_yieldedContinuations.TryDequeue(out continuation, out long frame))
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
	}
}
