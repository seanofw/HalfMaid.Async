using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HalfMaid.Async
{
	/// <summary>
	/// A simple base class for async game objects.  You don't *need* to use this,
	/// but it can make it easier to write async game code.  This maintains a single
	/// static task runner, which can then be used to execute any GameTasks produced
	/// by the methods of any child objects.
	/// </summary>
	public abstract class AsyncGameObjectBase
	{
		/// <summary>
		/// A runner that is managing and executing the GameTasks.  This is effectively
		/// part of your game's environment.
		/// </summary>
		public static GameTaskRunner Runner { get; set; } = new GameTaskRunner();

		/// <summary>
		/// Construct a new base object.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected AsyncGameObjectBase()
		{
		}

		/// <summary>
		/// Wait for the next frame to perform the actions after this.
		/// Note that `await Next()` is equivalent to `await Delay(1)`.
		/// </summary>
		/// <returns>An awaitable that waits for the next frame.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Next() => Runner.Next();

		/// <summary>
		/// Wait for the the given number of frames to perform the actions after this.
		/// Note that `await Next()` is equivalent to `await Delay(1)`.
		/// </summary>
		/// <returns>An awaitable that waits for the given number of frames.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public GameTaskYieldAwaitable Delay(int frames) => Runner.Delay(frames);

		/// <summary>
		/// Run an external task that performs I/O or similar as a GameTask.  The external
		/// task will be run on the thread pool.  When the external task completes, the
		/// calling GameTask will continue on the next frame following its completion.
		/// This is typically used to perform I/O easily, but without blocking the main
		/// game loop.
		/// </summary>
		/// <param name="task">The task to run.</param>
		public ExternalTaskAwaitable RunTask(Func<Task> task) => Runner.RunTask(task);
	}
}
